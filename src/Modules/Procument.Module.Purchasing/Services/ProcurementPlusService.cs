using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Catalog.Services;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

/// <summary>Who is asking — RFQ search follows the RFQ list's visibility (bases, assignments, own RFQs).</summary>
public sealed record PpUserScope(long UserId, bool IsSuperAdmin, int[] Bases);

public interface IProcurementPlusService
{
    // Parts
    Task<List<PpRfqOption>> SearchRfqsAsync(PpUserScope scope, string? search);
    Task<List<PpRfqItemOption>> GetRfqItemsAsync(PpUserScope scope, long rfqId);
    Task<List<PpRfqItemOption>> SearchRfqItemsAsync(PpUserScope scope, string? search);
    // userId = whose page (SuperAdmin can view another user's); null = every user (SuperAdmin "All users").
    Task<PpAddPartsResponse> AddPartsAsync(PpUserScope scope, PpAddPartsRequest request);
    Task<List<PpPartResponse>> GetPartsAsync(long? userId, bool includeHidden, string? track = null);
    Task<PpPartResponse?> UpdatePartAsync(long? userId, long actorId, long id, PpUpdatePartRequest request);
    Task<int> SetTrackAsync(long? userId, long actorId, PpSetTrackRequest request);
    Task<int> SetHiddenAsync(long? userId, List<long> ids, bool hidden);
    Task<int> DeletePartsAsync(long? userId, List<long> ids);

    // Boards (Sourcing / Project / WeChat): part → supplier lines → timeline
    Task<PpBoardPage> GetBoardAsync(long? userId, string track, bool includeHidden, string? search, string? status, string? sort, int page, int pageSize);
    Task<PpSupplierPage> GetSupplierViewAsync(long? userId, string track, string? search, int page, int pageSize);
    Task<PpRfqCoverage?> GetRfqCoverageAsync(PpUserScope scope, long? userId, long rfqId);
    Task<List<PpMyRfq>> GetMyRfqsAsync(PpUserScope scope, long userId);
    Task<bool> SaveMyRfqAsync(PpUserScope scope, long userId, long rfqId);
    Task<bool> RemoveMyRfqAsync(long userId, long rfqId);
    Task<PpQuoteLineResponse> AddLineAsync(long? userId, long actorId, long partId, PpAddLineRequest request);
    Task<PpEventResponse> AddNoteAsync(long? userId, long actorId, long partId, string text);
    Task<List<PpUserSummary>> GetUsersAsync();

    // Files
    Task<PpUploadResult> UploadAsync(long userId, IEnumerable<IFormFile> files);
    Task<bool> DeleteUploadAsync(long userId, long uploadId);
    Task<int> ClearUploadsAsync(long userId);
    Task<PpAvailabilityResponse> GetAvailabilityAsync(long userId);
    Task<PpResolveCompanyResponse> ResolveCompanyAsync(long userId, PpResolveCompanyRequest request);

    // Preferred suppliers (Preferreds table — catalog link optional)
    Task<List<PpPreferredSupplierResponse>> GetPreferredAsync();
    Task<PpPreferredSupplierResponse> SavePreferredAsync(long userId, long? id, PpSavePreferredSupplierRequest request);
    Task<bool> UpdatePreferredAsync(long id, PpUpdatePreferredRequest request);
    Task<bool> RemovePreferredAsync(long id);
    Task<PpLinkPreferredResponse?> LinkPreferredAsync(long id, PpLinkPreferredRequest request);
    Task<List<PpSupplierOption>> SearchSuppliersAsync(string? q);

    // Sourcing → Done
    Task<PpStartSourcingResponse> StartSourcingAsync(long userId, PpStartSourcingRequest request);
    Task<List<PpQuoteLineResponse>> GetSourcingAsync(long userId, bool everyone);
    Task<bool> IsLineAsync(long id);
    Task<PpQuoteLineResponse?> UpdateLineAsync(long id, UpdatePendingSupplierQuoteRequest request);
    Task<int> RemoveLinesAsync(long userId, List<long> ids);
    Task<PpDoneResponse> DoneAsync(long userId, List<long> ids);
    Task<PpDoneListResponse> GetDoneAsync(int? days, long? byUserId, string? search);
}

/// <summary>
/// Procurement Plus: a user picks parts from RFQs, uploads availability files (ILS CSV / Excel),
/// (preferred parts and suppliers live in the Preferreds table and need not be in the catalog),
/// sees which suppliers list those parts (preferred first), sources them by email / WeChat /
/// search / ILS, types the prices and clicks Done. Done = accepting the lines exactly like an
/// email quote (PendingSupplierQuote with Source = ProcurementPlus): written to every RFQ for the
/// part whose deadline is at most a week ago, and auto-filled onto new RFQs for AutoApplyDays.
/// </summary>
public class ProcurementPlusService : IProcurementPlusService
{
    /// <summary>Uploaded files feed the Suppliers Available tabs this long; older rows only show under History.</summary>
    public const int FreshDays = 7;
    private const string Pending = "Pending";
    private const string Accepted = "Accepted";
    private const string Removed = "Removed";
    private const string Pp = PendingSupplierQuote.SourceProcurementPlus;
    private const string PartKind = PreferredKinds.Part;
    private const string SupplierKind = PreferredKinds.Supplier;
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly DbContext _db;
    private readonly IPendingSupplierQuoteService _quotes;

    public ProcurementPlusService(DbContext db, IPendingSupplierQuoteService quotes)
    {
        _db = db;
        _quotes = quotes;
    }

    // ──── Part picking (from RFQs) ────

    private async Task<IQueryable<RFQHeader>> VisibleRfqsAsync(PpUserScope scope)
    {
        var query = _db.Set<RFQHeader>().AsNoTracking();
        if (scope.IsSuperAdmin) return query;
        var permitted = (await _db.Set<EntityPermission>()
                .Where(p => p.UserId == scope.UserId && p.EntityName == "RFQ")
                .Select(p => p.EntityId)
                .ToListAsync())
            .Select(id => long.TryParse(id, out var l) ? l : -1L)
            .Where(l => l > 0)
            .ToList();
        var bases = scope.Bases;
        return query.Where(r =>
            (r.Customer.Base != null && bases.Contains(r.Customer.Base.Value)) ||
            permitted.Contains(r.Id) ||
            r.UserId == scope.UserId);
    }

    public async Task<List<PpRfqOption>> SearchRfqsAsync(PpUserScope scope, string? search)
    {
        var query = await VisibleRfqsAsync(scope);
        var q = search?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            // Nothing typed: RFQs a Done quote can still reach (deadline at most a week ago).
            var earliest = DateTime.UtcNow.Date.AddDays(-PendingSupplierQuoteService.DeadlineGraceDays);
            query = query.Where(r => r.LeadTime >= earliest);
        }
        else
        {
            long.TryParse(q, out var id);
            query = query.Where(r => r.Id == id || r.Name.Contains(q)
                || (r.Customer.CustomerCode != null && r.Customer.CustomerCode.Contains(q))
                || r.RFQItems.Any(i => i.PartNumber.Name.Contains(q)));
        }
        return await query
            .OrderByDescending(r => r.LeadTime).ThenByDescending(r => r.Id)
            .Take(60)
            .Select(r => new PpRfqOption
            {
                Id = r.Id,
                Name = r.Name,
                Status = r.Status,
                Deadline = r.LeadTime,
                CustomerCode = r.Customer.CustomerCode,
                ItemCount = r.RFQItems.Count,
            })
            .ToListAsync();
    }

    public async Task<List<PpRfqItemOption>> GetRfqItemsAsync(PpUserScope scope, long rfqId)
    {
        var visible = await VisibleRfqsAsync(scope);
        if (!await visible.AnyAsync(r => r.Id == rfqId)) return new();
        return await ItemOptionsAsync(scope.UserId, _db.Set<RFQItem>().AsNoTracking().Where(i => i.RFQId == rfqId), 500);
    }

    public async Task<List<PpRfqItemOption>> SearchRfqItemsAsync(PpUserScope scope, string? search)
    {
        var q = search?.Trim();
        if (string.IsNullOrEmpty(q)) return new();
        var visible = await VisibleRfqsAsync(scope);
        var items = _db.Set<RFQItem>().AsNoTracking()
            .Where(i => visible.Any(r => r.Id == i.RFQId))
            .Where(i => i.PartNumber.Name.Contains(q)
                || (i.PartNumber.NewName != null && i.PartNumber.NewName.Contains(q))
                || (i.Alt != null && i.Alt.Contains(q)));
        return await ItemOptionsAsync(scope.UserId, items, 150);
    }

    private async Task<List<PpRfqItemOption>> ItemOptionsAsync(long userId, IQueryable<RFQItem> items, int take)
    {
        var list = await items
            .OrderByDescending(i => i.RFQ.LeadTime).ThenBy(i => i.Id)
            .Take(take)
            .Select(i => new PpRfqItemOption
            {
                RfqItemId = i.Id,
                RfqId = i.RFQId,
                RfqName = i.RFQ.Name,
                Deadline = i.RFQ.LeadTime,
                PartNumberId = i.PartNumberId,
                PartNumber = i.PartNumber.Name,
                Description = i.PartNumber.Description,
                Qty = i.Qty,
                Condition = i.Condition,
            })
            .ToListAsync();
        var listed = (await _db.Set<Preferred>().AsNoTracking()
                .Where(p => p.Kind == PartKind && p.UserId == userId)
                .Select(p => p.NameKey)
                .ToListAsync())
            .ToHashSet();
        foreach (var item in list) item.AlreadyAdded = listed.Contains(PnKey(item.PartNumber));
        return list;
    }

    public async Task<PpAddPartsResponse> AddPartsAsync(PpUserScope scope, PpAddPartsRequest request)
    {
        var result = new PpAddPartsResponse();
        var track = PreferredTracks.Normalize(request.Track);
        // The preferred part keeps its own copy of the name, description, qty, condition and alternatives —
        // it does not depend on the catalog (the catalog link is only kept as extra matching).
        var wanted = new List<(long? PartNumberId, string Name, string? Description, List<string> Alternatives, double? Qty, string? Condition, decimal? Target)>();

        var itemIds = request.Items.Where(i => i.RfqItemId != null).Select(i => i.RfqItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            var visible = await VisibleRfqsAsync(scope);
            var items = await _db.Set<RFQItem>().AsNoTracking()
                .Where(i => itemIds.Contains(i.Id) && visible.Any(r => r.Id == i.RFQId))
                .Select(i => new
                {
                    i.Id,
                    i.PartNumberId,
                    i.PartNumber.Name,
                    i.PartNumber.NewName,
                    i.PartNumber.Description,
                    i.Alt,
                    i.Qty,
                    i.Condition,
                    Alternatives = i.PartNumber.Alternatives.Select(a => a.Name).ToList(),
                })
                .ToListAsync();
            foreach (var pick in request.Items.Where(i => i.RfqItemId != null))
            {
                var i = items.FirstOrDefault(x => x.Id == pick.RfqItemId);
                if (i == null) continue;
                wanted.Add(((long?)i.PartNumberId, i.Name, NullIfBlank(pick.Description) ?? i.Description,
                    new[] { i.NewName, i.Alt }.Concat(i.Alternatives).OfType<string>().ToList(),
                    pick.Qty ?? i.Qty, NullIfBlank(pick.Condition) ?? i.Condition, pick.TargetPrice));
            }
        }

        var custom = request.Items.Where(i => i.RfqItemId == null && !string.IsNullOrWhiteSpace(i.PartNumber)).ToList();
        if (custom.Count > 0)
        {
            var typed = custom.Select(c => c.PartNumber!.Trim()).ToList();
            var catalog = await _db.Set<PartNumber>().AsNoTracking()
                .Where(p => typed.Contains(p.Name))
                .Select(p => new { p.Id, p.Name, p.NewName, p.Description, Alternatives = p.Alternatives.Select(a => a.Name).ToList() })
                .ToListAsync();
            foreach (var c in custom)
            {
                var name = c.PartNumber!.Trim();
                var match = catalog.FirstOrDefault(x => PnKey(x.Name) == PnKey(name));
                wanted.Add((match?.Id, Trunc(match?.Name ?? name, 200)!, NullIfBlank(c.Description) ?? match?.Description,
                    match == null ? new List<string>() : new[] { match.NewName }.OfType<string>().Concat(match.Alternatives).ToList(),
                    c.Qty, NullIfBlank(c.Condition), c.TargetPrice));
            }
        }

        if (PreferredTracks.NeedsTargetPrice(track))
        {
            var missing = wanted.Where(w => w.Target is not > 0).Select(w => w.Name).Distinct().ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"{track} parts need a target price: {string.Join(", ", missing)}.");
        }

        var mine = await _db.Set<Preferred>().Where(p => p.Kind == PartKind && p.UserId == scope.UserId).ToListAsync();
        var byKey = mine.GroupBy(p => p.NameKey).ToDictionary(g => g.Key, g => g.First());
        var events = new List<(Preferred Part, string Kind, string Text)>();
        foreach (var w in wanted.GroupBy(w => PnKey(w.Name)).Select(g => g.First()))
        {
            var key = PnKey(w.Name);
            if (key.Length == 0) continue;
            if (byKey.TryGetValue(key, out var existing))
            {
                if (existing.HiddenAt != null) { existing.HiddenAt = null; result.Restored++; }
                else result.AlreadyListed++;
                existing.PartNumberId ??= w.PartNumberId;
                existing.Description ??= Trunc(w.Description, 500);
                existing.Alternatives = JoinAlternatives(SplitList(existing.Alternatives).Concat(w.Alternatives), existing.NameKey);
                if (w.Qty != null) existing.Qty = w.Qty;
                if (w.Condition != null) existing.Condition = Trunc(w.Condition.ToUpperInvariant(), 50);
                if (w.Target != null) existing.TargetPrice = w.Target;
                if (existing.Track != track)
                {
                    events.Add((existing, "Track", $"Moved from {existing.Track} to {track}{TargetText(existing.TargetPrice)}"));
                    existing.Track = track;
                }
                continue;
            }
            var part = new Preferred
            {
                Kind = PartKind,
                UserId = scope.UserId,
                PartNumberId = w.PartNumberId,
                Name = Trunc(w.Name.Trim(), 300)!,
                NameKey = Trunc(key, 300)!,
                Description = Trunc(w.Description, 500),
                Alternatives = JoinAlternatives(w.Alternatives, key),
                Track = track,
                Qty = w.Qty,
                Condition = Trunc(w.Condition?.ToUpperInvariant(), 50),
                TargetPrice = w.Target,
                AddedAt = DateTime.UtcNow,
                AddedByUserId = scope.UserId,
            };
            _db.Set<Preferred>().Add(part);
            byKey[key] = part;
            events.Add((part, "Added", $"Added to {track}{TargetText(w.Target)}"));
            result.Added++;
        }
        await _db.SaveChangesAsync();
        foreach (var (part, kind, text) in events) AddEvent(part.Id, scope.UserId, kind, text);
        await _db.SaveChangesAsync();
        return result;
    }

    /// <summary>Edit a preferred part (its own copy — the catalog is not touched). Moving to Project / WeChat needs a target price.</summary>
    public async Task<PpPartResponse?> UpdatePartAsync(long? userId, long actorId, long id, PpUpdatePartRequest request)
    {
        var part = await MyParts(userId).FirstOrDefaultAsync(p => p.Id == id);
        if (part == null) return null;
        if (request.Description != null) part.Description = Trunc(NullIfBlank(request.Description), 500);
        if (request.Alternatives != null) part.Alternatives = JoinAlternatives(request.Alternatives, part.NameKey);
        if (request.Qty != null) part.Qty = request.Qty > 0 ? request.Qty : null;
        if (request.Condition != null) part.Condition = Trunc(NullIfBlank(request.Condition)?.ToUpperInvariant(), 50);
        var target = request.TargetPrice > 0 ? request.TargetPrice : null; // 0 = clear
        if (request.TargetPrice != null && target != part.TargetPrice)
        {
            part.TargetPrice = target;
            AddEvent(part.Id, actorId, "Target", part.TargetPrice == null ? "Target price cleared" : $"Target price set to {Money(part.TargetPrice)}");
        }
        if (request.Track != null)
        {
            var track = PreferredTracks.Normalize(request.Track);
            if (PreferredTracks.NeedsTargetPrice(track) && part.TargetPrice is not > 0)
                throw new InvalidOperationException($"{track} parts need a target price.");
            if (track != part.Track)
            {
                AddEvent(part.Id, actorId, "Track", $"Moved from {part.Track} to {track}");
                part.Track = track;
            }
        }
        await _db.SaveChangesAsync();
        return (await GetPartsAsync(part.UserId, true)).FirstOrDefault(p => p.Id == id);
    }

    /// <summary>Moves parts to a track (e.g. "No supplier" parts → Project), each with its target price.</summary>
    public async Task<int> SetTrackAsync(long? userId, long actorId, PpSetTrackRequest request)
    {
        var track = PreferredTracks.Normalize(request.Track);
        var ids = request.Items.Select(i => i.Id).ToList();
        var parts = await MyParts(userId).Where(p => ids.Contains(p.Id)).ToListAsync();
        foreach (var part in parts)
        {
            var target = request.Items.First(i => i.Id == part.Id).TargetPrice;
            if (target is > 0) part.TargetPrice = target;
            if (PreferredTracks.NeedsTargetPrice(track) && part.TargetPrice is not > 0)
                throw new InvalidOperationException($"{track} parts need a target price: {part.Name}.");
            if (part.Track == track) continue;
            AddEvent(part.Id, actorId, "Track", $"Moved from {part.Track} to {track}{TargetText(part.TargetPrice)}");
            part.Track = track;
            part.HiddenAt = null;
        }
        await _db.SaveChangesAsync();
        return parts.Count;
    }

    public async Task<int> SetHiddenAsync(long? userId, List<long> ids, bool hidden)
    {
        var rows = await MyParts(userId).Where(p => ids.Contains(p.Id)).ToListAsync();
        foreach (var row in rows) row.HiddenAt = hidden ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    public Task<int> DeletePartsAsync(long? userId, List<long> ids) =>
        MyParts(userId).Where(p => ids.Contains(p.Id)).ExecuteDeleteAsync();

    /// <summary>Preferred parts of one user's page, or of every user (null — SuperAdmin "All users").</summary>
    private IQueryable<Preferred> MyParts(long? userId) =>
        _db.Set<Preferred>().Where(p => p.Kind == PartKind && (userId == null || p.UserId == userId));

    // ──── Boards: Sourcing (ILS) / Project / WeChat ────

    public const int MaxBoardPageSize = 100;

    /// <summary>A light view of a Procurement Plus line — enough to filter, count and group without building it.</summary>
    private sealed record LineHead(long Id, long? PreferredPartId, long? CreatedByUserId, string PartNumber, string Status,
        decimal? Price, long? SupplierId, long? PreferredId, string? SupplierNameRaw);

    /// <summary>Lines being priced, plus lines Done in the last AutoApplyDays, of a user (null = everyone).</summary>
    private async Task<List<LineHead>> LineHeadsAsync(long? userId)
    {
        var since = DateTime.UtcNow.AddDays(-PendingSupplierQuoteService.AutoApplyDays);
        return await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(q => q.Source == Pp && (userId == null || q.CreatedByUserId == userId)
                && (q.Status == Pending || (q.Status == Accepted && q.ReviewedAt >= since)))
            .Select(q => new LineHead(q.Id, q.PreferredPartId, q.CreatedByUserId, q.PartNumber, q.Status,
                q.Price, q.SupplierId, q.PreferredId, q.SupplierNameRaw))
            .ToListAsync();
    }

    /// <summary>Line id → part id: its PreferredPartId, or (lines from before parts were linked) owner + part number.</summary>
    private static Dictionary<long, long> MapLinesToParts(IEnumerable<LineHead> lines, IEnumerable<Preferred> parts)
    {
        var list = parts.ToList();
        var ids = list.Select(p => p.Id).ToHashSet();
        var byOwnerKey = list.GroupBy(p => (p.UserId, p.NameKey)).ToDictionary(g => g.Key, g => g.First().Id);
        var result = new Dictionary<long, long>();
        foreach (var l in lines)
        {
            if (l.PreferredPartId != null) { if (ids.Contains(l.PreferredPartId.Value)) result[l.Id] = l.PreferredPartId.Value; }
            else if (byOwnerKey.TryGetValue((l.CreatedByUserId, PnKey(l.PartNumber)), out var pid)) result[l.Id] = pid;
        }
        return result;
    }

    /// <summary>none (no supplier) / sourcing / ready (all priced + linked) / done (only Done lines left).</summary>
    private static string StatusOf(List<LineHead>? lines)
    {
        if (lines == null || lines.Count == 0) return "none";
        var pending = lines.Where(l => l.Status == Pending).ToList();
        if (pending.Count == 0) return "done";
        return pending.All(l => l.Price != null && l.SupplierId != null) ? "ready" : "sourcing";
    }

    /// <summary>
    /// One page of a board (Parts = ILS, Project, WeChat): filtered by search and status, sorted, then only
    /// the page's parts are fully built (open RFQs, price stats, lines, timeline) — fast with hundreds of parts.
    /// </summary>
    public async Task<PpBoardPage> GetBoardAsync(long? userId, string track, bool includeHidden, string? search, string? status,
        string? sort, int page, int pageSize)
    {
        track = PreferredTracks.Normalize(track);
        pageSize = Math.Clamp(pageSize, 1, MaxBoardPageSize);
        var parts = await LoadPartsAsync(userId, includeHidden, track);
        var heads = await LineHeadsAsync(userId);
        var partOf = MapLinesToParts(heads, parts.Select(p => p.Part));
        var linesByPart = heads.Where(h => partOf.ContainsKey(h.Id)).GroupBy(h => partOf[h.Id]).ToDictionary(g => g.Key, g => g.ToList());
        var inFiles = track == PreferredTracks.Ils ? await PartsWithOffersAsync(userId, parts) : new HashSet<long>();

        var q = search?.Trim();
        var key = PnKey(q);
        var searched = string.IsNullOrEmpty(q)
            ? parts
            : parts.Where(p => p.Keys.Any(k => k.Contains(key))
                || (p.Part.Description ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (linesByPart.GetValueOrDefault(p.Part.Id) ?? []).Any(l => (l.SupplierNameRaw ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)))
              .ToList();

        var statusOf = searched.ToDictionary(p => p.Part.Id, p => StatusOf(linesByPart.GetValueOrDefault(p.Part.Id)));
        var counts = new PpBoardCounts
        {
            All = searched.Count,
            NoSupplier = statusOf.Values.Count(v => v == "none"),
            Sourcing = statusOf.Values.Count(v => v == "sourcing"),
            Ready = statusOf.Values.Count(v => v == "ready"),
            Done = statusOf.Values.Count(v => v == "done"),
            NotInFiles = track == PreferredTracks.Ils ? searched.Count(p => !inFiles.Contains(p.Part.Id)) : 0,
        };

        var filtered = (status ?? "all") switch
        {
            "none" or "sourcing" or "ready" or "done" => searched.Where(p => statusOf[p.Part.Id] == status).ToList(),
            "nofiles" => searched.Where(p => !inFiles.Contains(p.Part.Id)).ToList(),
            _ => searched,
        };

        IEnumerable<PartKeys> sorted = sort switch
        {
            "pn" => filtered.OrderBy(p => p.Part.Name, StringComparer.OrdinalIgnoreCase),
            "suppliers" => filtered.OrderByDescending(p => linesByPart.GetValueOrDefault(p.Part.Id)?.Count ?? 0).ThenBy(p => p.Part.Name),
            "deadline" => await SortByDeadlineAsync(filtered),
            _ => filtered.OrderByDescending(p => p.Part.AddedAt).ThenBy(p => p.Part.Name),
        };
        var ordered = sorted.ToList();
        var lastPage = Math.Max(1, (ordered.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, lastPage);
        var pageParts = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PpBoardPage
        {
            Items = await BuildBoardPartsAsync(pageParts, userId, heads, partOf),
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Counts = counts,
        };
    }

    /// <summary>Soonest open-RFQ deadline first; parts on no open RFQ last.</summary>
    private async Task<IEnumerable<PartKeys>> SortByDeadlineAsync(List<PartKeys> parts)
    {
        var items = await FindRfqItemsAsync(parts.SelectMany(p => p.Names), DateTime.UtcNow.Date.AddDays(-PendingSupplierQuoteService.DeadlineGraceDays));
        var soonest = parts.ToDictionary(p => p.Part.Id, p => items.Where(i => ItemKeys(i).Overlaps(p.Keys)).Select(i => (DateTime?)i.RFQ.LeadTime).Min());
        return parts.OrderBy(p => soonest[p.Part.Id] == null ? 1 : 0).ThenBy(p => soonest[p.Part.Id]).ThenBy(p => p.Part.Name);
    }

    /// <summary>ILS parts that have a row in their owner's current files.</summary>
    private async Task<HashSet<long>> PartsWithOffersAsync(long? userId, List<PartKeys> parts)
    {
        var allKeys = parts.SelectMany(p => p.Keys).Distinct().ToList();
        if (allKeys.Count == 0) return new();
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var offers = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Where(o => (userId == null || o.Upload.UserId == userId) && o.Upload.UploadedAt >= since
                && (allKeys.Contains(o.PartKey) || (o.AltKey != null && allKeys.Contains(o.AltKey))))
            .Select(o => new { o.Upload.UserId, o.PartKey, o.AltKey })
            .Distinct()
            .ToListAsync();
        return parts
            .Where(p => offers.Any(o => o.UserId == p.Part.UserId && (p.Keys.Contains(o.PartKey) || (o.AltKey != null && p.Keys.Contains(o.AltKey)))))
            .Select(p => p.Part.Id)
            .ToHashSet();
    }

    /// <summary>These parts, fully built, with their lines (being priced first, cheapest first) and timeline.</summary>
    private async Task<List<PpBoardPart>> BuildBoardPartsAsync(List<PartKeys> parts, long? userId, List<LineHead> heads, Dictionary<long, long> partOf)
    {
        if (parts.Count == 0) return new();
        var built = await BuildPartsAsync(parts, userId);
        var ids = parts.Select(p => p.Part.Id).ToHashSet();
        var lines = await BuildLinesAsync(heads.Where(h => partOf.TryGetValue(h.Id, out var pid) && ids.Contains(pid)).Select(h => h.Id).ToList());
        foreach (var l in lines) l.PreferredPartId ??= partOf.GetValueOrDefault(l.Line.Id);
        var events = await EventsAsync(ids.ToList());
        return built.Select(p => new PpBoardPart
        {
            Part = p,
            Lines = lines.Where(l => partOf.GetValueOrDefault(l.Line.Id) == p.Id)
                .OrderBy(l => l.Status == Pending ? 0 : 1)
                .ThenBy(l => l.Line.Price == null ? 1 : 0)
                .ThenBy(l => l.Line.Price)
                .ToList(),
            Events = events.GetValueOrDefault(p.Id) ?? new(),
        }).ToList();
    }

    /// <summary>"By supplier": every supplier with lines on this board's parts, and those lines (paged by supplier).</summary>
    public async Task<PpSupplierPage> GetSupplierViewAsync(long? userId, string track, string? search, int page, int pageSize)
    {
        track = PreferredTracks.Normalize(track);
        pageSize = Math.Clamp(pageSize, 1, MaxBoardPageSize);
        var parts = await LoadPartsAsync(userId, false, track);
        var heads = await LineHeadsAsync(userId);
        var partOf = MapLinesToParts(heads, parts.Select(p => p.Part));
        var partName = parts.ToDictionary(p => p.Part.Id, p => p.Part.Name);
        var mine = heads.Where(h => partOf.ContainsKey(h.Id)).ToList();

        var preferredIds = mine.Select(h => h.PreferredId).OfType<long>().Distinct().ToList();
        var preferreds = await _db.Set<Preferred>().AsNoTracking().Where(p => preferredIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var supplierIds = mine.Select(h => h.SupplierId).OfType<long>().Distinct().ToList();
        var catalog = await _db.Set<Supplier>().AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        (string Key, string Name) Who(LineHead h)
        {
            if (h.PreferredId != null && preferreds.TryGetValue(h.PreferredId.Value, out var pref)) return ($"p{pref.Id}", pref.Name);
            if (h.SupplierId != null && catalog.TryGetValue(h.SupplierId.Value, out var name)) return ($"s{h.SupplierId}", name);
            var raw = h.SupplierNameRaw ?? "—";
            return ($"n{SupplierNameNormalizer.Key(raw)}", raw);
        }

        var q = search?.Trim();
        var groups = mine.GroupBy(h => Who(h).Key)
            .Select(g => (Key: g.Key, Name: Who(g.First()).Name, Lines: g.ToList()))
            .Where(g => string.IsNullOrEmpty(q)
                || g.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || g.Lines.Any(l => partName[partOf[l.Id]].Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var lastPage = Math.Max(1, (groups.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, lastPage);
        var pageGroups = groups.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var lines = await BuildLinesAsync(pageGroups.SelectMany(g => g.Lines).Select(l => l.Id).ToList());
        foreach (var l in lines) l.PreferredPartId ??= partOf.GetValueOrDefault(l.Line.Id);

        return new PpSupplierPage
        {
            Items = pageGroups.Select(g =>
            {
                var first = g.Lines[0];
                var groupLines = lines.Where(l => g.Lines.Any(h => h.Id == l.Line.Id))
                    .OrderBy(l => l.Status == Pending ? 0 : 1).ThenBy(l => l.Line.PartNumber).ToList();
                return new PpSupplierGroup
                {
                    Key = g.Key,
                    Name = g.Name,
                    PreferredId = first.PreferredId,
                    SupplierId = g.Lines.Select(l => l.SupplierId).FirstOrDefault(id => id != null),
                    SourceMethod = groupLines.Select(l => l.SourceMethod).FirstOrDefault(m => m != null),
                    PendingCount = g.Lines.Count(l => l.Status == Pending),
                    PricedCount = g.Lines.Count(l => l.Status == Pending && l.Price != null),
                    DoneCount = g.Lines.Count(l => l.Status == Accepted),
                    Lines = groupLines,
                };
            }).ToList(),
            Total = groups.Count,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <summary>
    /// "By RFQ": every item of the RFQ — the supplier quotes already on it, and the part on the user's list
    /// (any track) with its lines — so the user sees which items have suppliers and fills the rest.
    /// </summary>
    public async Task<PpRfqCoverage?> GetRfqCoverageAsync(PpUserScope scope, long? userId, long rfqId)
    {
        var visible = await VisibleRfqsAsync(scope);
        var rfq = await visible
            .Where(r => r.Id == rfqId)
            .Select(r => new PpRfqOption
            {
                Id = r.Id,
                Name = r.Name,
                Status = r.Status,
                Deadline = r.LeadTime,
                CustomerCode = r.Customer.CustomerCode,
                ItemCount = r.RFQItems.Count,
            })
            .FirstOrDefaultAsync();
        if (rfq == null) return null;

        var items = await _db.Set<RFQItem>().AsNoTracking()
            .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(i => i.RFQId == rfqId)
            .OrderBy(i => i.Id)
            .ToListAsync();
        var itemIds = items.Select(i => i.Id).ToList();
        var quotes = (await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(r => itemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop")
                .Select(r => new { r.RFQItemId, SupplierName = r.Supplier.Name, r.Price, r.Condition, r.Qty, r.LeadTime, r.Alt, r.CertName, r.TagDate, When = r.UpdatedAt ?? r.CreatedAt })
                .ToListAsync())
            .GroupBy(r => r.RFQItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Price).Select(r => new PpRfqQuote
            {
                SupplierName = r.SupplierName,
                Price = r.Price,
                Condition = r.Condition,
                Qty = r.Qty,
                LeadTime = r.LeadTime,
                Alt = r.Alt,
                CertName = r.CertName,
                TagDate = r.TagDate,
                Date = r.When,
            }).ToList());
        var older = await OlderQuotesAsync(items, rfqId);

        // The user's parts (any track) — a shown one wins over a removed one.
        var parts = (await LoadPartsAsync(userId, true)).OrderBy(p => p.Part.HiddenAt == null ? 0 : 1).ToList();
        var matchOf = items.ToDictionary(i => i.Id, i => parts.FirstOrDefault(p => p.Keys.Overlaps(ItemKeys(i))));
        var matched = matchOf.Values.OfType<PartKeys>().DistinctBy(p => p.Part.Id).ToList();
        var heads = await LineHeadsAsync(userId);
        var partOf = MapLinesToParts(heads, matched.Select(p => p.Part));
        var boards = (await BuildBoardPartsAsync(matched, userId, heads, partOf)).ToDictionary(b => b.Part.Id);

        return new PpRfqCoverage
        {
            Rfq = rfq,
            Items = items.Select(i =>
            {
                var board = matchOf[i.Id] is { } m ? boards.GetValueOrDefault(m.Part.Id) : null;
                var rfqQuotes = quotes.GetValueOrDefault(i.Id) ?? new();
                return new PpRfqCoverageItem
                {
                    RfqItemId = i.Id,
                    PartNumber = i.PartNumber.Name,
                    Description = i.PartNumber.Description,
                    Qty = i.Qty,
                    Condition = i.Condition,
                    Alt = i.Alt,
                    RfqQuotes = rfqQuotes,
                    OlderQuotes = older.GetValueOrDefault(i.Id)?.Take(MaxOlderQuotes).ToList() ?? new(),
                    OlderQuoteCount = older.GetValueOrDefault(i.Id)?.Count ?? 0,
                    Board = board,
                    HasSupplier = rfqQuotes.Count > 0 || (board?.Lines.Count ?? 0) > 0,
                };
            }).ToList(),
        };
    }

    /// <summary>Older quotes listed per RFQ item (newest first).</summary>
    public const int MaxOlderQuotes = 50;

    /// <summary>
    /// Supplier quotes for each item's part on OTHER RFQs — matched by the item's part number, new name,
    /// RFQ-item alt and catalog alternatives — newest first.
    /// </summary>
    private async Task<Dictionary<long, List<PpRfqQuote>>> OlderQuotesAsync(List<RFQItem> items, long rfqId)
    {
        var keysOf = items.ToDictionary(i => i.Id, ItemKeys);
        var names = items
            .SelectMany(i => new[] { i.PartNumber.Name, i.PartNumber.NewName, i.Alt }.Concat(i.PartNumber.Alternatives.Select(a => a.Name)))
            .OfType<string>().Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim())
            .Concat(keysOf.Values.SelectMany(k => k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0) return new();

        // Catalog parts answering to any of those names, with every key they answer to.
        var catalog = (await _db.Set<PartNumber>().AsNoTracking()
                .Where(p => names.Contains(p.Name) || (p.NewName != null && names.Contains(p.NewName)) || p.Alternatives.Any(a => names.Contains(a.Name)))
                .Select(p => new { p.Id, p.Name, p.NewName, Alternatives = p.Alternatives.Select(a => a.Name).ToList() })
                .ToListAsync())
            .ToDictionary(c => c.Id, c => new[] { c.Name, c.NewName }.Concat(c.Alternatives).Select(PnKey).Where(k => k.Length > 0).ToHashSet());
        var catalogIds = catalog.Keys.ToList();
        if (catalogIds.Count == 0) return new();

        var records = await _db.Set<ProcumentRecord>().AsNoTracking()
            .Where(r => catalogIds.Contains(r.RFQItem.PartNumberId) && r.RFQItem.RFQId != rfqId && (r.Type ?? "Procument") != "Shop")
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Take(5000)
            .Select(r => new
            {
                r.RFQItem.PartNumberId,
                r.RFQItem.RFQId,
                RfqName = r.RFQItem.RFQ.Name,
                r.RFQItem.RFQ.Customer.CustomerCode,
                PartName = r.RFQItem.PartNumber.Name,
                SupplierName = r.Supplier.Name,
                r.Price,
                r.Condition,
                r.Qty,
                r.LeadTime,
                r.Alt,
                r.CertName,
                r.TagDate,
                When = r.UpdatedAt ?? r.CreatedAt,
            })
            .ToListAsync();

        return items.ToDictionary(i => i.Id, i =>
        {
            var ids = catalog.Where(c => c.Value.Overlaps(keysOf[i.Id])).Select(c => c.Key).ToHashSet();
            return records.Where(r => ids.Contains(r.PartNumberId)).Select(r => new PpRfqQuote
            {
                RfqId = r.RFQId,
                RfqName = r.RfqName,
                CustomerCode = r.CustomerCode,
                PartNumber = r.PartName,
                SupplierName = r.SupplierName,
                Price = r.Price,
                Condition = r.Condition,
                Qty = r.Qty,
                LeadTime = r.LeadTime,
                Alt = r.Alt,
                CertName = r.CertName,
                TagDate = r.TagDate,
                Date = r.When,
            }).ToList();
        });
    }

    // ──── My RFQs (saved per user, By RFQ tab) ────

    /// <summary>The user's saved RFQs, last opened first, with how many items already have a supplier.</summary>
    public async Task<List<PpMyRfq>> GetMyRfqsAsync(PpUserScope scope, long userId)
    {
        var saved = await _db.Set<Preferred>().AsNoTracking()
            .Where(p => p.Kind == PreferredKinds.Rfq && p.UserId == userId && p.RfqId != null)
            .ToListAsync();
        if (saved.Count == 0) return new();
        var ids = saved.Select(p => p.RfqId!.Value).ToList();
        var visible = await VisibleRfqsAsync(scope);
        var rfqs = await visible.Where(r => ids.Contains(r.Id))
            .Select(r => new PpMyRfq
            {
                RfqId = r.Id,
                Name = r.Name,
                CustomerCode = r.Customer.CustomerCode,
                Status = r.Status,
                Deadline = r.LeadTime,
                ItemCount = r.RFQItems.Count,
            })
            .ToListAsync();

        // Covered = a supplier quote on the item, or one of this user's lines for its part.
        var rfqIds = rfqs.Select(r => r.RfqId).ToList();
        var items = await _db.Set<RFQItem>().AsNoTracking()
            .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(i => rfqIds.Contains(i.RFQId))
            .ToListAsync();
        var itemIds = items.Select(i => i.Id).ToList();
        var quoted = (await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(r => itemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop")
                .Select(r => r.RFQItemId)
                .Distinct()
                .ToListAsync())
            .ToHashSet();
        var lineKeys = (await LineHeadsAsync(userId)).Select(h => PnKey(h.PartNumber)).ToHashSet();
        foreach (var r in rfqs)
            r.CoveredCount = items.Count(i => i.RFQId == r.RfqId && (quoted.Contains(i.Id) || ItemKeys(i).Overlaps(lineKeys)));

        var openedAt = saved.ToDictionary(p => p.RfqId!.Value, p => p.AddedAt);
        foreach (var r in rfqs) r.AddedAt = openedAt[r.RfqId];
        return rfqs.OrderByDescending(r => r.AddedAt).ToList();
    }

    /// <summary>Saves the RFQ on the user's list (opening it again moves it to the top). False when not visible.</summary>
    public async Task<bool> SaveMyRfqAsync(PpUserScope scope, long userId, long rfqId)
    {
        var visible = await VisibleRfqsAsync(scope);
        var name = await visible.Where(r => r.Id == rfqId).Select(r => r.Name).FirstOrDefaultAsync();
        if (name == null) return false;
        var row = await _db.Set<Preferred>().FirstOrDefaultAsync(p => p.Kind == PreferredKinds.Rfq && p.UserId == userId && p.RfqId == rfqId);
        if (row == null)
        {
            row = new Preferred
            {
                Kind = PreferredKinds.Rfq,
                UserId = userId,
                RfqId = rfqId,
                Name = Trunc(name, 300)!,
                NameKey = rfqId.ToString(),
                AddedByUserId = userId,
            };
            _db.Set<Preferred>().Add(row);
        }
        row.AddedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMyRfqAsync(long userId, long rfqId) =>
        await _db.Set<Preferred>()
            .Where(p => p.Kind == PreferredKinds.Rfq && p.UserId == userId && p.RfqId == rfqId)
            .ExecuteDeleteAsync() > 0;

    // ──── Price history per condition ────

    /// <summary>
    /// Average / min / max price per condition for each part: supplier quotes on any RFQ for it (older RFQs,
    /// including prices Done here or accepted from email) plus prices typed on lines still being priced.
    /// </summary>
    private async Task<Dictionary<long, List<PpPriceStat>>> PriceStatsAsync(List<PartKeys> parts)
    {
        var names = parts.SelectMany(p => p.Names).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return new();
        var index = IndexByKey(parts);
        var catalogParts = await _db.Set<PartNumber>().AsNoTracking()
            .Where(p => names.Contains(p.Name) || (p.NewName != null && names.Contains(p.NewName)) || p.Alternatives.Any(a => names.Contains(a.Name)))
            .Select(p => new { p.Id, p.Name, p.NewName, Alternatives = p.Alternatives.Select(a => a.Name).ToList() })
            .ToListAsync();
        var partsByCatalogId = catalogParts.ToDictionary(
            c => c.Id,
            c => new[] { c.Name, c.NewName }.Concat(c.Alternatives).Select(PnKey)
                .SelectMany(k => index.GetValueOrDefault(k) ?? new()).Select(p => p.Part.Id).Distinct().ToList());
        var catalogIds = catalogParts.Select(c => c.Id).ToList();

        var prices = new List<(long PartId, string Condition, decimal Price, DateTime When)>();
        if (catalogIds.Count > 0)
        {
            var records = await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(r => catalogIds.Contains(r.RFQItem.PartNumberId) && (r.Type ?? "Procument") != "Shop" && r.Price > 0)
                .Select(r => new { r.RFQItem.PartNumberId, r.Condition, r.Price, When = r.UpdatedAt ?? r.CreatedAt })
                .ToListAsync();
            foreach (var r in records)
                foreach (var partId in partsByCatalogId.GetValueOrDefault(r.PartNumberId) ?? new())
                    prices.Add((partId, CondKey(r.Condition), r.Price, r.When));
        }

        // Prices typed here but not Done yet (Done ones are already supplier quotes above).
        var partIds = parts.Select(p => p.Part.Id).ToList();
        var typed = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(q => q.Source == Pp && q.Status == Pending && q.PreferredPartId != null && partIds.Contains(q.PreferredPartId.Value) && q.Price > 0)
            .Select(q => new { q.PreferredPartId, q.Condition, q.Price, q.CreatedAt })
            .ToListAsync();
        prices.AddRange(typed.Select(t => (t.PreferredPartId!.Value, CondKey(t.Condition), t.Price!.Value, t.CreatedAt)));

        return prices.GroupBy(x => x.PartId).ToDictionary(
            g => g.Key,
            g => g.GroupBy(x => x.Condition)
                .Select(c => new PpPriceStat
                {
                    Condition = c.Key,
                    Average = Math.Round(c.Average(x => x.Price), 2),
                    Min = c.Min(x => x.Price),
                    Max = c.Max(x => x.Price),
                    Count = c.Count(),
                    LastDate = c.Max(x => x.When),
                })
                .OrderByDescending(c => c.Count)
                .ToList());
    }

    /// <summary>
    /// A supplier added by hand under a part: a preferred supplier, a catalog supplier, or just a name (it then
    /// needs a catalog supplier picked on the line before Done). Starts with the part's qty and condition.
    /// </summary>
    public async Task<PpQuoteLineResponse> AddLineAsync(long? userId, long actorId, long partId, PpAddLineRequest request)
    {
        var part = await MyParts(userId).AsNoTracking().FirstOrDefaultAsync(p => p.Id == partId)
            ?? throw new InvalidOperationException("Part not found.");
        Preferred? pref = null;
        Supplier? catalog = null;
        if (request.PreferredId is > 0)
            pref = await _db.Set<Preferred>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PreferredId && p.Kind == SupplierKind)
                ?? throw new InvalidOperationException("Preferred supplier not found.");
        var supplierId = request.SupplierId is > 0 ? request.SupplierId : pref?.SupplierId;
        if (supplierId != null)
            catalog = await _db.Set<Supplier>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId)
                ?? throw new InvalidOperationException("Supplier not found.");
        var name = pref?.Name ?? catalog?.Name ?? SupplierNameNormalizer.Clean(request.SupplierName);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Pick or type a supplier.");

        var line = new PendingSupplierQuote
        {
            Source = Pp,
            SourceMethod = part.Track == PreferredTracks.Ils ? PreferredSupplierMethods.Normalize(pref?.SourceMethod) : part.Track,
            CreatedByUserId = part.UserId,
            PreferredId = pref?.Id,
            PreferredPartId = part.Id,
            SupplierId = catalog?.Id,
            SupplierNameRaw = Trunc(name, 300),
            SupplierEmail = Trunc((pref != null ? EmailsOf(pref).FirstOrDefault() : null) ?? SupplierEmails.Normalize(catalog?.Email), 320),
            EmailSubject = Trunc($"Procurement Plus · {part.Track}", 500),
            PartNumber = Trunc(part.Name, 200)!,
            Qty = part.Qty,
            Condition = Trunc(part.Condition, 100),
            Status = Pending,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<PendingSupplierQuote>().Add(line);
        AddEvent(part.Id, actorId, "Supplier", $"Supplier {name} added");
        await _db.SaveChangesAsync();
        return (await BuildLinesAsync([line.Id])).First();
    }

    public async Task<PpEventResponse> AddNoteAsync(long? userId, long actorId, long partId, string text)
    {
        var note = NullIfBlank(text) ?? throw new InvalidOperationException("Write something first.");
        if (!await MyParts(userId).AnyAsync(p => p.Id == partId)) throw new InvalidOperationException("Part not found.");
        var ev = AddEvent(partId, actorId, "Note", note);
        await _db.SaveChangesAsync();
        var names = await UserNamesAsync([actorId]);
        return new PpEventResponse { Id = ev.Id, Kind = ev.Kind, Text = ev.Text, CreatedAt = ev.CreatedAt, UserName = names.GetValueOrDefault(actorId) };
    }

    /// <summary>SuperAdmin user filter: everyone with a Procurement Plus page, and how much they have on it.</summary>
    public async Task<List<PpUserSummary>> GetUsersAsync()
    {
        var parts = await _db.Set<Preferred>().AsNoTracking()
            .Where(p => p.Kind == PartKind && p.UserId != null && p.HiddenAt == null)
            .GroupBy(p => p.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();
        var sourcing = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(q => q.Source == Pp && q.Status == Pending && q.CreatedByUserId != null)
            .GroupBy(q => q.CreatedByUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();
        var names = await UserNamesAsync(parts.Select(p => (long?)p.UserId).Concat(sourcing.Select(s => (long?)s.UserId)));
        return names.Select(n => new PpUserSummary
            {
                Id = n.Key,
                Name = n.Value,
                PartCount = parts.FirstOrDefault(p => p.UserId == n.Key)?.Count ?? 0,
                SourcingCount = sourcing.FirstOrDefault(s => s.UserId == n.Key)?.Count ?? 0,
            })
            .OrderBy(u => u.Name)
            .ToList();
    }

    // ──── Tracing timeline ────

    private PreferredEvent AddEvent(long partId, long? userId, string kind, string text)
    {
        var ev = new PreferredEvent { PreferredId = partId, UserId = userId, Kind = kind, Text = Trunc(text, 1000)!, CreatedAt = DateTime.UtcNow };
        _db.Set<PreferredEvent>().Add(ev);
        return ev;
    }

    private async Task<Dictionary<long, List<PpEventResponse>>> EventsAsync(List<long> partIds)
    {
        var rows = await _db.Set<PreferredEvent>().AsNoTracking()
            .Where(e => partIds.Contains(e.PreferredId))
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .ToListAsync();
        var names = await UserNamesAsync(rows.Select(r => r.UserId));
        return rows.GroupBy(r => r.PreferredId).ToDictionary(g => g.Key, g => g.Select(e => new PpEventResponse
        {
            Id = e.Id,
            Kind = e.Kind,
            Text = e.Text,
            CreatedAt = e.CreatedAt,
            UserName = e.UserId != null ? names.GetValueOrDefault(e.UserId.Value) : null,
        }).ToList());
    }

    private static string Money(decimal? v) => v == null ? "" : "$" + v.Value.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture);
    private static string TargetText(decimal? target) => target is > 0 ? $" · target {Money(target)}" : "";

    // ──── Parts list ────

    /// <summary>A user's preferred part with every key it answers to (name, its alternatives, linked catalog part's names).</summary>
    private sealed record PartKeys(Preferred Part, List<string> Alternatives, HashSet<string> Keys, List<string> Names);

    /// <summary>Preferred parts of a user (null = every user), optionally of one track (ILS / Project / WeChat).</summary>
    private async Task<List<PartKeys>> LoadPartsAsync(long? userId, bool includeHidden, string? track = null)
    {
        var parts = await MyParts(userId).AsNoTracking()
            .Include(p => p.CatalogPart).ThenInclude(c => c!.Alternatives)
            .Where(p => (includeHidden || p.HiddenAt == null) && (track == null || p.Track == track))
            .OrderByDescending(p => p.AddedAt).ThenBy(p => p.Name)
            .ToListAsync();
        return parts.Select(p =>
        {
            var alternatives = SplitList(p.Alternatives)
                .Concat(new[] { p.CatalogPart?.NewName }.OfType<string>())
                .Concat(p.CatalogPart?.Alternatives.Select(a => a.Name) ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a) && PnKey(a) != p.NameKey)
                .Select(a => a.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var raw = new[] { p.Name, p.CatalogPart?.Name }.OfType<string>().Concat(alternatives)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();
            var keys = raw.Select(PnKey).Where(k => k.Length > 0).ToHashSet();
            return new PartKeys(p, alternatives, keys, raw.Concat(keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }).ToList();
    }

    /// <summary>The user's parts a file row answers (by its P/N or its Alt P/N).</summary>
    private static List<PartKeys> PartsFor(Dictionary<string, List<PartKeys>> index, string partKey, string? altKey)
    {
        var found = index.GetValueOrDefault(partKey) ?? new List<PartKeys>();
        if (altKey != null && index.TryGetValue(altKey, out var byAlt)) found = found.Concat(byAlt).ToList();
        return found.DistinctBy(p => p.Part.Id).ToList();
    }

    private static Dictionary<string, List<PartKeys>> IndexByKey(IEnumerable<PartKeys> parts)
    {
        var index = new Dictionary<string, List<PartKeys>>();
        foreach (var p in parts)
            foreach (var key in p.Keys)
            {
                if (!index.TryGetValue(key, out var list)) index[key] = list = new();
                list.Add(p);
            }
        return index;
    }

    public async Task<List<PpPartResponse>> GetPartsAsync(long? userId, bool includeHidden, string? track = null) =>
        await BuildPartsAsync(await LoadPartsAsync(userId, includeHidden, track == null ? null : PreferredTracks.Normalize(track)), userId);

    /// <summary>Full part rows (open RFQs, file counts, live prices, price stats) for these parts only.</summary>
    private async Task<List<PpPartResponse>> BuildPartsAsync(List<PartKeys> parts, long? userId)
    {
        if (parts.Count == 0) return new();
        var index = IndexByKey(parts);
        var allKeys = index.Keys.ToList();

        // Open RFQs per part.
        var openItems = await FindRfqItemsAsync(parts.SelectMany(p => p.Names), DateTime.UtcNow.Date.AddDays(-PendingSupplierQuoteService.DeadlineGraceDays));

        // Fresh file rows per part.
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var offers = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Where(o => (userId == null || o.Upload.UserId == userId) && o.Upload.UploadedAt >= since
                && (allKeys.Contains(o.PartKey) || (o.AltKey != null && allKeys.Contains(o.AltKey))))
            .Select(o => new { o.PartKey, o.AltKey, o.SupplierId, o.CompanyKey, o.Email, o.Upload.UserId })
            .ToListAsync();
        var matcher = await LoadPreferredMatcherAsync();
        var preferredByCompany = offers.GroupBy(o => o.CompanyKey)
            .ToDictionary(g => g.Key, g => g.Select(o => matcher.Match(o.SupplierId, o.Email, o.CompanyKey)).FirstOrDefault(m => m != null));

        // Lines being sourced here.
        var sourcing = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => p.Source == Pp && p.Status == Pending && (userId == null || p.CreatedByUserId == userId))
            .Select(p => new { p.PartNumber, p.Alt, p.CreatedByUserId, p.PreferredPartId })
            .ToListAsync();

        var live = await LiveQuotesAsync(parts);
        var priceStats = await PriceStatsAsync(parts);
        var owners = await UserNamesAsync(parts.Select(p => p.Part.UserId));

        return parts.Select(p =>
        {
            // Files are per user: only the part owner's files count for it.
            var partOffers = offers.Where(o => o.UserId == p.Part.UserId
                && (p.Keys.Contains(o.PartKey) || (o.AltKey != null && p.Keys.Contains(o.AltKey)))).ToList();
            var preferredCount = partOffers.Select(o => preferredByCompany[o.CompanyKey]?.Id).OfType<long>().Distinct().Count();
            var otherCount = partOffers.Where(o => preferredByCompany[o.CompanyKey] == null)
                .Select(o => o.SupplierId?.ToString() ?? "c:" + o.CompanyKey).Distinct().Count();
            return new PpPartResponse
            {
                Id = p.Part.Id,
                PartNumberId = p.Part.PartNumberId,
                PartNumber = p.Part.Name,
                Description = p.Part.Description ?? p.Part.CatalogPart?.Description,
                Alternatives = p.Alternatives,
                OwnAlternatives = SplitList(p.Part.Alternatives),
                AddedAt = p.Part.AddedAt,
                Hidden = p.Part.HiddenAt != null,
                Track = p.Part.Track,
                Qty = p.Part.Qty,
                Condition = p.Part.Condition,
                TargetPrice = p.Part.TargetPrice,
                OwnerUserId = p.Part.UserId,
                OwnerName = p.Part.UserId != null ? owners.GetValueOrDefault(p.Part.UserId.Value) : null,
                OpenRfqs = openItems
                    .Where(i => ItemKeys(i).Overlaps(p.Keys))
                    .GroupBy(i => i.RFQId)
                    .Select(g => new PpRfqRef
                    {
                        RfqId = g.Key,
                        RfqName = g.First().RFQ.Name,
                        Deadline = g.First().RFQ.LeadTime,
                        CustomerCode = g.First().RFQ.Customer?.CustomerCode,
                        Qty = g.Sum(i => i.Qty),
                    })
                    .OrderBy(r => r.Deadline)
                    .ToList(),
                OfferCount = partOffers.Count,
                PreferredSupplierCount = preferredCount,
                OtherSupplierCount = otherCount,
                SourcingCount = sourcing.Count(s => s.PreferredPartId == p.Part.Id
                    || (s.PreferredPartId == null && s.CreatedByUserId == p.Part.UserId
                        && (p.Keys.Contains(PnKey(s.PartNumber)) || (s.Alt != null && p.Keys.Contains(PnKey(s.Alt)))))),
                LiveQuotes = live.TryGetValue(p.Part.Id, out var l) ? l : new(),
                PriceStats = priceStats.GetValueOrDefault(p.Part.Id) ?? new(),
            };
        }).ToList();
    }

    /// <summary>
    /// Accepted quotes (Email Quotes or Done here) for these parts that still auto-fill new RFQs,
    /// with the RFQs they were written to.
    /// </summary>
    private async Task<Dictionary<long, List<PpLiveQuote>>> LiveQuotesAsync(List<PartKeys> parts)
    {
        var since = DateTime.UtcNow.AddDays(-PendingSupplierQuoteService.AutoApplyDays);
        var names = parts.SelectMany(p => p.Names).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return new();
        var rows = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.Status == Accepted && !p.AutoApplyDisabled && p.ReviewedAt >= since && p.SupplierId != null
                && (names.Contains(p.PartNumber) || (p.Alt != null && names.Contains(p.Alt))))
            .OrderByDescending(p => p.ReviewedAt)
            .ToListAsync();
        if (rows.Count == 0) return new();

        var written = await WrittenRfqsAsync(rows);
        var userNames = await UserNamesAsync(rows.Select(r => r.ReviewedByUserId));
        var result = new Dictionary<long, List<PpLiveQuote>>();
        foreach (var p in parts)
        {
            var mine = rows.Where(r => p.Keys.Contains(PnKey(r.PartNumber)) || (r.Alt != null && p.Keys.Contains(PnKey(r.Alt)))).ToList();
            if (mine.Count == 0) continue;
            result[p.Part.Id] = mine.Select(r => new PpLiveQuote
            {
                QuoteId = r.Id,
                Source = r.Source == Pp ? Pp : "Email",
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier?.Name,
                Price = r.Price,
                Condition = r.Condition,
                AcceptedAt = r.ReviewedAt,
                AcceptedBy = r.ReviewedByUserId != null ? userNames.GetValueOrDefault(r.ReviewedByUserId.Value) : null,
                AutoFillUntil = r.ReviewedAt?.AddDays(PendingSupplierQuoteService.AutoApplyDays),
                Rfqs = written.GetValueOrDefault(r.Id) ?? new(),
            }).ToList();
        }
        return result;
    }

    /// <summary>
    /// The RFQs each accepted line was written to: that supplier's quotes on an RFQ item for the
    /// line's part (or offered alt), created or updated since the line was accepted.
    /// </summary>
    private async Task<Dictionary<long, List<PpRfqRef>>> WrittenRfqsAsync(List<PendingSupplierQuote> lines)
    {
        var accepted = lines.Where(l => l.Status == Accepted && l.ReviewedAt != null && l.SupplierId != null).ToList();
        if (accepted.Count == 0) return new();
        var supplierIds = accepted.Select(l => l.SupplierId!.Value).Distinct().ToList();
        var since = accepted.Min(l => l.ReviewedAt!.Value).AddMinutes(-2);
        var records = await _db.Set<ProcumentRecord>().AsNoTracking()
            .Where(r => supplierIds.Contains(r.SupplierId) && (r.UpdatedAt ?? r.CreatedAt) >= since && (r.Type ?? "Procument") != "Shop")
            .Select(r => new
            {
                r.SupplierId,
                r.Price,
                r.Qty,
                When = r.UpdatedAt ?? r.CreatedAt,
                r.RFQItem.RFQId,
                RfqName = r.RFQItem.RFQ.Name,
                Deadline = r.RFQItem.RFQ.LeadTime,
                CustomerCode = r.RFQItem.RFQ.Customer.CustomerCode,
                ItemAlt = r.RFQItem.Alt,
                r.RFQItem.PartNumber.Name,
                r.RFQItem.PartNumber.NewName,
                Alternatives = r.RFQItem.PartNumber.Alternatives.Select(a => a.Name).ToList(),
            })
            .ToListAsync();

        var result = new Dictionary<long, List<PpRfqRef>>();
        foreach (var line in accepted)
        {
            var keys = new[] { line.PartNumber, line.Alt }.Select(PnKey).Where(k => k.Length > 0).ToHashSet();
            var from = line.ReviewedAt!.Value.AddMinutes(-2);
            result[line.Id] = records
                .Where(r => r.SupplierId == line.SupplierId && r.When >= from
                    && new[] { r.Name, r.NewName, r.ItemAlt }.Concat(r.Alternatives).Any(n => keys.Contains(PnKey(n))))
                .GroupBy(r => r.RFQId)
                .Select(g => g.OrderByDescending(r => r.When).First())
                .Select(r => new PpRfqRef
                {
                    RfqId = r.RFQId,
                    RfqName = r.RfqName,
                    Deadline = r.Deadline,
                    CustomerCode = r.CustomerCode,
                    Qty = r.Qty,
                    Price = r.Price,
                })
                .OrderBy(r => r.Deadline)
                .ToList();
        }
        return result;
    }

    // ──── Files ────

    public async Task<PpUploadResult> UploadAsync(long userId, IEnumerable<IFormFile> files)
    {
        var result = new PpUploadResult();
        foreach (var file in files.Where(f => f is { Length: > 0 }))
        {
            var name = Trunc(Path.GetFileName(file.FileName), 255) ?? "file.csv";
            List<PpParsedRow> rows;
            try
            {
                using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                rows = ProcurementPlusFileParser.Parse(await reader.ReadToEndAsync());
            }
            catch (InvalidOperationException ex)
            {
                result.Errors.Add(new PpUploadError(name, ex.Message));
                continue;
            }
            if (rows.Count == 0)
            {
                result.Errors.Add(new PpUploadError(name, "The file has a header row but no part rows."));
                continue;
            }

            var upload = new ProcurementPlusUpload { UserId = userId, FileName = name, UploadedAt = DateTime.UtcNow, RowCount = rows.Count };
            foreach (var r in rows)
            {
                var company = Trunc(SupplierNameNormalizer.Clean(r.Company), 300)!;
                upload.Offers.Add(new ProcurementPlusOffer
                {
                    PartNo = Trunc(r.PartNo, 200)!,
                    PartKey = Trunc(PnKey(r.PartNo), 200)!,
                    AltPartNo = Trunc(r.AltPartNo, 200),
                    AltKey = r.AltPartNo == null ? null : Trunc(PnKey(r.AltPartNo), 200),
                    CompanyRaw = company,
                    CompanyKey = Trunc(SupplierNameNormalizer.Key(company), 300)!,
                    SupplierComment = Trunc(r.Comment, 1000),
                    QtyRaw = Trunc(r.Qty, 50),
                    Qty = ProcurementPlusFileParser.ParseQty(r.Qty),
                    Condition = Trunc(r.Condition?.ToUpperInvariant(), 50),
                    Description = Trunc(r.Description, 500),
                    PriceRaw = Trunc(r.Price, 100),
                    Price = ProcurementPlusFileParser.ParsePrice(r.Price),
                    Phone = Trunc(r.Phone, 100),
                    Fax = Trunc(r.Fax, 100),
                    Email = Trunc(SupplierEmails.Normalize(r.Email), 320),
                    Distance = Trunc(r.Distance, 50),
                    Cage = Trunc(r.Cage, 50),
                    Exch = Trunc(r.Exch, 50),
                });
            }
            await MatchSuppliersAsync(upload.Offers);
            _db.Set<ProcurementPlusUpload>().Add(upload);
            await _db.SaveChangesAsync();
            result.Uploads.Add(new PpUploadResponse { Id = upload.Id, FileName = upload.FileName, UploadedAt = upload.UploadedAt, RowCount = upload.RowCount });
        }

        // Matched rows per file, against the user's shown parts.
        var keys = (await LoadPartsAsync(userId, false, PreferredTracks.Ils)).SelectMany(p => p.Keys).ToHashSet();
        var ids = result.Uploads.Select(u => u.Id).ToList();
        var counts = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Where(o => ids.Contains(o.UploadId))
            .Select(o => new { o.UploadId, o.PartKey, o.AltKey })
            .ToListAsync();
        foreach (var u in result.Uploads)
            u.MatchedRows = counts.Count(o => o.UploadId == u.Id && (keys.Contains(o.PartKey) || (o.AltKey != null && keys.Contains(o.AltKey))));
        return result;
    }

    /// <summary>
    /// Catalog supplier per company: a name the user linked before (alias), else an email in the row
    /// (exact address, else the same company domain - that address is then added to the supplier's
    /// catalog contacts), else the same supplier name.
    /// </summary>
    private async Task MatchSuppliersAsync(ICollection<ProcurementPlusOffer> offers)
    {
        var companyKeys = offers.Select(o => o.CompanyKey).Distinct().ToList();
        var aliases = await _db.Set<SupplierAlias>().AsNoTracking()
            .Where(a => companyKeys.Contains(a.AliasKey))
            .ToDictionaryAsync(a => a.AliasKey, a => a.SupplierId);
        var byEmail = await _quotes.MatchSuppliersByEmailAsync(
            offers.Where(o => !aliases.ContainsKey(o.CompanyKey)).Select(o => o.Email), addDomainContacts: true);

        var names = offers.Select(o => o.CompanyRaw).Distinct().ToList();
        var byName = (await _db.Set<Supplier>().AsNoTracking()
                .Where(s => s.IsActive && s.Status != "Rejected" && s.Dependency != "Disabled" && names.Contains(s.Name))
                .Select(s => new { s.Id, s.Name, s.Status })
                .ToListAsync())
            .GroupBy(s => SupplierNameNormalizer.Key(s.Name))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Status == "Approved" ? 0 : 1).ThenBy(s => s.Id).First().Id);

        foreach (var o in offers)
        {
            if (aliases.TryGetValue(o.CompanyKey, out var aliased)) o.SupplierId = aliased;
            else if (o.Email != null && byEmail.TryGetValue(o.Email, out var emailed)) o.SupplierId = emailed;
            else if (byName.TryGetValue(o.CompanyKey, out var named)) o.SupplierId = named;
        }

        // One company = one supplier: rows of a company without an email get what its other rows found.
        foreach (var company in offers.GroupBy(o => o.CompanyKey))
        {
            var found = company.Select(o => o.SupplierId).FirstOrDefault(id => id != null);
            if (found == null) continue;
            foreach (var o in company.Where(o => o.SupplierId == null)) o.SupplierId = found;
        }
    }

    public async Task<bool> DeleteUploadAsync(long userId, long uploadId) =>
        await _db.Set<ProcurementPlusUpload>().Where(u => u.Id == uploadId && u.UserId == userId).ExecuteDeleteAsync() > 0;

    /// <summary>Removes the user's current files (the fresh ones); older files stay for History.</summary>
    public Task<int> ClearUploadsAsync(long userId)
    {
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        return _db.Set<ProcurementPlusUpload>().Where(u => u.UserId == userId && u.UploadedAt >= since).ExecuteDeleteAsync();
    }

    public async Task<PpResolveCompanyResponse> ResolveCompanyAsync(long userId, PpResolveCompanyRequest request)
    {
        var company = SupplierNameNormalizer.Clean(request.CompanyRaw);
        var key = Trunc(SupplierNameNormalizer.Key(company), 300)!;
        if (key.Length == 0) throw new InvalidOperationException("Company name is required.");
        if (!await _db.Set<Supplier>().AnyAsync(s => s.Id == request.SupplierId))
            throw new InvalidOperationException("Supplier not found.");

        // Remembered for every user and every future file.
        var alias = await _db.Set<SupplierAlias>().FirstOrDefaultAsync(a => a.AliasKey == key);
        if (alias == null)
            _db.Set<SupplierAlias>().Add(new SupplierAlias
            {
                AliasKey = key,
                Alias = Trunc(company, 300)!,
                SupplierId = request.SupplierId,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
            });
        else alias.SupplierId = request.SupplierId;
        await _db.SaveChangesAsync();

        var response = new PpResolveCompanyResponse
        {
            Updated = await _db.Set<ProcurementPlusOffer>()
                .Where(o => o.CompanyKey == key)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.SupplierId, (long?)request.SupplierId)),
        };

        // The company's addresses in the files join the supplier's catalog contacts, so the next file matches by email.
        var emails = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Where(o => o.CompanyKey == key && o.Email != null)
            .Select(o => o.Email!)
            .Distinct()
            .ToListAsync();
        foreach (var email in emails)
        {
            var (added, heldBy) = await _quotes.AddEmailToSupplierAsync(request.SupplierId, email);
            if (added) response.EmailsAdded.Add(email);
            else if (heldBy != null) response.EmailsOnOtherSupplier.Add($"{email} ({heldBy})");
        }

        // A preferred supplier for this company with no catalog link gets this one (and its open lines).
        var matcher = await LoadPreferredMatcherAsync();
        var preferred = emails.Select(e => matcher.Match(null, e, key)).FirstOrDefault(m => m != null) ?? matcher.Match(null, null, key);
        if (preferred is { SupplierId: null })
            await SetPreferredSupplierAsync(preferred.Id, request.SupplierId);
        return response;
    }

    // ──── Suppliers Available (4 tabs) ────

    public async Task<PpAvailabilityResponse> GetAvailabilityAsync(long userId)
    {
        var response = new PpAvailabilityResponse { FreshDays = FreshDays };
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var parts = await LoadPartsAsync(userId, false, PreferredTracks.Ils);
        var index = IndexByKey(parts);
        var allKeys = index.Keys.ToList();

        var uploads = await _db.Set<ProcurementPlusUpload>().AsNoTracking()
            .Where(u => u.UserId == userId && u.UploadedAt >= since)
            .OrderByDescending(u => u.UploadedAt)
            .ToListAsync();
        var uploadIds = uploads.Select(u => u.Id).ToList();

        var offers = allKeys.Count == 0 || uploadIds.Count == 0
            ? new List<ProcurementPlusOffer>()
            : await _db.Set<ProcurementPlusOffer>().AsNoTracking()
                .Include(o => o.Supplier)
                .Where(o => uploadIds.Contains(o.UploadId)
                    && (allKeys.Contains(o.PartKey) || (o.AltKey != null && allKeys.Contains(o.AltKey))))
                .OrderBy(o => o.Id)
                .ToListAsync();

        response.Uploads = uploads.Select(u => new PpUploadResponse
        {
            Id = u.Id,
            FileName = u.FileName,
            UploadedAt = u.UploadedAt,
            RowCount = u.RowCount,
            MatchedRows = offers.Count(o => o.UploadId == u.Id),
        }).ToList();
        var uploadById = uploads.ToDictionary(u => u.Id);

        var matcher = await LoadPreferredMatcherAsync();
        // One company = one preferred supplier: the first of its rows that matches decides.
        var preferredByCompany = offers.GroupBy(o => o.CompanyKey)
            .ToDictionary(g => g.Key, g => g.Select(o => matcher.Match(o.SupplierId, o.Email, o.CompanyKey)).FirstOrDefault(m => m != null));
        var state = await SourcingStateAsync(userId);

        // One row per (file row, part) — a row normally answers one part.
        var rows = new List<PpOfferRow>();
        foreach (var o in offers)
        {
            var pref = preferredByCompany[o.CompanyKey];
            foreach (var p in PartsFor(index, o.PartKey, o.AltKey))
            {
                var upload = uploadById[o.UploadId];
                rows.Add(new PpOfferRow
                {
                    OfferId = o.Id,
                    PartId = p.Part.Id,
                    PartNumber = p.Part.Name,
                    FilePartNo = o.PartNo,
                    AltPartNo = o.AltPartNo,
                    SupplierId = o.SupplierId,
                    SupplierName = o.Supplier?.Name,
                    SupplierStatus = o.Supplier?.Status,
                    SupplierEmail = o.Supplier?.Email,
                    PreferredId = pref?.Id,
                    PreferredName = pref?.Name,
                    CompanyRaw = o.CompanyRaw,
                    CompanyKey = o.CompanyKey,
                    SupplierComment = o.SupplierComment,
                    QtyRaw = o.QtyRaw,
                    Qty = o.Qty,
                    Condition = o.Condition,
                    Description = o.Description,
                    Price = o.Price,
                    PriceRaw = o.PriceRaw,
                    Email = o.Email,
                    Phone = o.Phone,
                    Distance = o.Distance,
                    Cage = o.Cage,
                    FileName = upload.FileName,
                    UploadedAt = upload.UploadedAt,
                    SourceMethod = pref != null ? PreferredSupplierMethods.Normalize(pref.SourceMethod) : null,
                    SourcingState = state.GetValueOrDefault((pref != null ? $"p{pref.Id}" : $"s{o.SupplierId}", p.Part.NameKey)),
                });
            }
        }

        response.Preferred = rows.Where(r => r.PreferredId != null).ToList();
        response.Others = rows.Where(r => r.PreferredId == null).ToList();

        var listed = rows.Select(r => r.PartId).ToHashSet();
        var openItems = await FindRfqItemsAsync(parts.Where(p => !listed.Contains(p.Part.Id)).SelectMany(p => p.Names),
            DateTime.UtcNow.Date.AddDays(-PendingSupplierQuoteService.DeadlineGraceDays));
        response.Missing = parts.Where(p => !listed.Contains(p.Part.Id)).Select(p => new PpMissingPart
        {
            PartId = p.Part.Id,
            PartNumber = p.Part.Name,
            Description = p.Part.Description ?? p.Part.CatalogPart?.Description,
            Alternatives = p.Alternatives,
            OpenRfqCount = openItems.Where(i => ItemKeys(i).Overlaps(p.Keys)).Select(i => i.RFQId).Distinct().Count(),
        }).ToList();

        response.Unmatched = rows.Where(r => r.SupplierId == null)
            .GroupBy(r => r.CompanyKey)
            .Select(g => new PpUnmatchedCompany
            {
                CompanyKey = g.Key,
                CompanyRaw = g.First().CompanyRaw,
                Emails = g.Select(r => r.Email).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Phones = g.Select(r => r.Phone).OfType<string>().Distinct().ToList(),
                Comment = g.Select(r => r.SupplierComment).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                PartNumbers = g.Select(r => r.PartNumber).Distinct().ToList(),
                Files = g.Select(r => r.FileName).Distinct().ToList(),
                RowCount = g.Count(),
            })
            .OrderBy(c => c.CompanyRaw)
            .ToList();

        response.History = await HistoryAsync(userId, parts, rows, matcher);
        return response;
    }

    /// <summary>"Sourcing" / "Done" per ("p{preferredId}" or "s{supplierId}", part key) for the user's Procurement Plus lines.</summary>
    private async Task<Dictionary<(string, string), string>> SourcingStateAsync(long userId)
    {
        var since = DateTime.UtcNow.AddDays(-PendingSupplierQuoteService.AutoApplyDays);
        var lines = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => p.Source == Pp && p.CreatedByUserId == userId
                && (p.Status == Pending || (p.Status == Accepted && p.ReviewedAt >= since)))
            .Select(p => new { p.PreferredId, p.SupplierId, p.PartNumber, p.Status })
            .ToListAsync();
        var result = new Dictionary<(string, string), string>();
        foreach (var l in lines)
        {
            var who = l.PreferredId != null ? $"p{l.PreferredId}" : $"s{l.SupplierId}";
            var key = (who, PnKey(l.PartNumber));
            // Sourcing wins: there is still something to finish.
            if (l.Status == Pending || !result.ContainsKey(key)) result[key] = l.Status == Pending ? "Sourcing" : "Done";
        }
        return result;
    }

    /// <summary>
    /// Suppliers we worked with on each part but that are not in the current files: their supplier
    /// quotes on RFQs, and rows of files older than FreshDays.
    /// </summary>
    private async Task<List<PpHistoryRow>> HistoryAsync(long userId, List<PartKeys> parts, List<PpOfferRow> current, PreferredMatcher preferred)
    {
        if (parts.Count == 0) return new();
        var index = IndexByKey(parts);
        var inFiles = current.Select(r => (r.PartId, r.SupplierId?.ToString() ?? "c:" + r.CompanyKey)).ToHashSet();
        var history = new List<PpHistoryRow>();

        // Supplier quotes on RFQ items for the part (any RFQ, any time).
        var names = parts.SelectMany(p => p.Names).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var catalogParts = await _db.Set<PartNumber>().AsNoTracking()
            .Where(p => names.Contains(p.Name) || (p.NewName != null && names.Contains(p.NewName)) || p.Alternatives.Any(a => names.Contains(a.Name)))
            .Select(p => new { p.Id, p.Name, p.NewName, Alternatives = p.Alternatives.Select(a => a.Name).ToList() })
            .ToListAsync();
        var partsByCatalogId = catalogParts.ToDictionary(
            c => c.Id,
            c => new[] { c.Name, c.NewName }.Concat(c.Alternatives)
                .Select(PnKey)
                .SelectMany(k => index.GetValueOrDefault(k) ?? new())
                .Select(p => p.Part)
                .DistinctBy(p => p.Id)
                .ToList());
        var catalogIds = catalogParts.Select(c => c.Id).ToList();
        var quotes = await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(r => catalogIds.Contains(r.RFQItem.PartNumberId) && (r.Type ?? "Procument") != "Shop")
                .Select(r => new
                {
                    r.RFQItem.PartNumberId,
                    r.SupplierId,
                    SupplierName = r.Supplier.Name,
                    r.Price,
                    r.Condition,
                    r.Qty,
                    When = r.UpdatedAt ?? r.CreatedAt,
                    RfqName = r.RFQItem.RFQ.Name,
                    r.Note,
                })
                .ToListAsync();
        foreach (var g in quotes
                     .SelectMany(q => partsByCatalogId.GetValueOrDefault(q.PartNumberId)!.Select(p => (Part: p, Quote: q)))
                     .GroupBy(x => (x.Part.Id, x.Quote.SupplierId)))
        {
            if (inFiles.Contains((g.Key.Item1, g.Key.SupplierId.ToString()))) continue;
            var last = g.OrderByDescending(x => x.Quote.When).First();
            history.Add(new PpHistoryRow
            {
                PartId = last.Part.Id,
                PartNumber = last.Part.Name,
                SupplierId = last.Quote.SupplierId,
                SupplierName = last.Quote.SupplierName,
                Kind = "RFQ quote",
                LastPrice = last.Quote.Price,
                Condition = last.Quote.Condition,
                Qty = last.Quote.Qty,
                LastDate = last.Quote.When,
                Times = g.Count(),
                LastRfqName = last.Quote.RfqName,
                Comment = last.Quote.Note,
                IsPreferred = preferred.Match(last.Quote.SupplierId, null, SupplierNameNormalizer.Key(last.Quote.SupplierName)) != null,
            });
        }

        // Rows of the user's older files (past the fresh window, up to a year back).
        var freshSince = DateTime.UtcNow.AddDays(-FreshDays);
        var oldest = DateTime.UtcNow.AddDays(-365);
        var allKeys = index.Keys.ToList();
        var oldOffers = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Where(o => o.Upload.UserId == userId && o.Upload.UploadedAt < freshSince && o.Upload.UploadedAt >= oldest
                && (allKeys.Contains(o.PartKey) || (o.AltKey != null && allKeys.Contains(o.AltKey))))
            .Select(o => new
            {
                o.PartKey, o.AltKey, o.SupplierId, o.Email, SupplierName = o.Supplier != null ? o.Supplier.Name : null,
                o.CompanyRaw, o.CompanyKey, o.Price, o.Condition, o.Qty, o.SupplierComment, o.Upload.UploadedAt, o.Upload.FileName,
            })
            .ToListAsync();
        var quoted = history.Select(h => (h.PartId, h.SupplierId?.ToString() ?? "")).ToHashSet();
        foreach (var g in oldOffers
                     .SelectMany(o => PartsFor(index, o.PartKey, o.AltKey).Select(p => (Part: p.Part, Offer: o)))
                     .GroupBy(x => (x.Part.Id, Who: x.Offer.SupplierId?.ToString() ?? "c:" + x.Offer.CompanyKey)))
        {
            if (inFiles.Contains(g.Key) || quoted.Contains(g.Key)) continue;
            var last = g.OrderByDescending(x => x.Offer.UploadedAt).First();
            history.Add(new PpHistoryRow
            {
                PartId = last.Part.Id,
                PartNumber = last.Part.Name,
                SupplierId = last.Offer.SupplierId,
                SupplierName = last.Offer.SupplierName ?? last.Offer.CompanyRaw,
                Kind = "File",
                LastPrice = last.Offer.Price,
                Condition = last.Offer.Condition,
                Qty = last.Offer.Qty,
                LastDate = last.Offer.UploadedAt,
                Times = g.Select(x => x.Offer.UploadedAt).Distinct().Count(),
                LastRfqName = last.Offer.FileName,
                Comment = last.Offer.SupplierComment,
                IsPreferred = preferred.Match(last.Offer.SupplierId, last.Offer.Email, last.Offer.CompanyKey) != null,
            });
        }

        return history
            .OrderBy(h => h.PartNumber)
            .ThenByDescending(h => h.IsPreferred)
            .ThenByDescending(h => h.LastDate)
            .ToList();
    }

    // ──── Preferred suppliers (Preferreds table) ────

    /// <summary>
    /// Finds the preferred supplier a file row belongs to: its catalog link, an exact email, the same
    /// company email domain (free-mail never), else the same company name.
    /// </summary>
    private sealed class PreferredMatcher
    {
        private readonly Dictionary<long, Preferred> _bySupplier = new();
        private readonly Dictionary<string, Preferred> _byEmail = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Preferred> _byDomain = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Preferred> _byName = new();

        public PreferredMatcher(IEnumerable<Preferred> preferreds)
        {
            foreach (var p in preferreds.OrderBy(p => p.Id))
            {
                if (p.SupplierId != null) _bySupplier.TryAdd(p.SupplierId.Value, p);
                foreach (var email in EmailsOf(p))
                {
                    _byEmail.TryAdd(email, p);
                    var domain = SupplierEmails.Domain(email);
                    if (!SupplierEmails.IsGenericDomain(domain)) _byDomain.TryAdd(domain, p);
                }
                if (p.NameKey.Length > 0) _byName.TryAdd(p.NameKey, p);
            }
        }

        public Preferred? Match(long? supplierId, string? email, string? companyKey)
        {
            if (supplierId != null && _bySupplier.TryGetValue(supplierId.Value, out var linked)) return linked;
            var e = SupplierEmails.Normalize(email);
            if (e != null)
            {
                if (_byEmail.TryGetValue(e, out var exact)) return exact;
                var domain = SupplierEmails.Domain(e);
                if (!SupplierEmails.IsGenericDomain(domain) && _byDomain.TryGetValue(domain, out var sameDomain)) return sameDomain;
            }
            return companyKey != null && _byName.TryGetValue(companyKey, out var named) ? named : null;
        }
    }

    private async Task<PreferredMatcher> LoadPreferredMatcherAsync() =>
        new(await _db.Set<Preferred>().AsNoTracking().Where(p => p.Kind == SupplierKind).ToListAsync());

    private static List<string> EmailsOf(Preferred p) =>
        SplitList(p.Emails).Select(SupplierEmails.Normalize).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<List<PpPreferredSupplierResponse>> GetPreferredAsync()
    {
        var rows = await _db.Set<Preferred>().AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.Kind == SupplierKind)
            .OrderBy(p => p.Name)
            .ToListAsync();
        var names = await UserNamesAsync(rows.Select(r => r.AddedByUserId));
        return rows.Select(p => ToResponse(p, names)).ToList();
    }

    private static PpPreferredSupplierResponse ToResponse(Preferred p, Dictionary<long, string> userNames) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Emails = EmailsOf(p),
        Phone = p.Phone,
        SourceMethod = PreferredSupplierMethods.Normalize(p.SourceMethod),
        Note = p.Note,
        SupplierId = p.SupplierId,
        SupplierName = p.Supplier?.Name,
        SupplierStatus = p.Supplier?.Status,
        AddedAt = p.AddedAt,
        AddedBy = p.AddedByUserId != null ? userNames.GetValueOrDefault(p.AddedByUserId.Value) : null,
    };

    /// <summary>
    /// Adds (id null) or edits a preferred supplier. Only a name is needed; a catalog supplier, when given
    /// without a name, fills the name, emails and phone. Adding one that already exists (same catalog
    /// supplier, same name or same email) updates that one instead.
    /// </summary>
    public async Task<PpPreferredSupplierResponse> SavePreferredAsync(long userId, long? id, PpSavePreferredSupplierRequest request)
    {
        var supplierId = request.SupplierId is > 0 ? request.SupplierId : null;
        var catalog = supplierId == null ? null : await _db.Set<Supplier>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId)
            ?? throw new InvalidOperationException("Supplier not found.");
        var name = SupplierNameNormalizer.Clean(NullIfBlank(request.Name) ?? catalog?.Name);
        if (name.Length == 0) throw new InvalidOperationException("Supplier name is required.");
        var emails = request.Emails.Select(SupplierEmails.Normalize).OfType<string>().ToList();
        if (catalog != null && id == null && emails.Count == 0) emails = SupplierEmails.AllEmails(catalog);
        var key = Trunc(SupplierNameNormalizer.Key(name), 300)!;

        Preferred? row;
        if (id != null)
        {
            row = await _db.Set<Preferred>().FirstOrDefaultAsync(p => p.Id == id && p.Kind == SupplierKind)
                ?? throw new InvalidOperationException("Preferred supplier not found.");
        }
        else
        {
            var all = await _db.Set<Preferred>().Where(p => p.Kind == SupplierKind).ToListAsync();
            row = all.FirstOrDefault(p => (supplierId != null && p.SupplierId == supplierId) || p.NameKey == key)
                ?? all.FirstOrDefault(p => EmailsOf(p).Intersect(emails, StringComparer.OrdinalIgnoreCase).Any());
            if (row == null)
            {
                row = new Preferred { Kind = SupplierKind, AddedAt = DateTime.UtcNow, AddedByUserId = userId };
                _db.Set<Preferred>().Add(row);
            }
            else emails = EmailsOf(row).Concat(emails).ToList(); // adding again merges the addresses
        }

        row.Name = Trunc(name, 300)!;
        row.NameKey = key;
        row.Emails = Trunc(string.Join(", ", emails.Distinct(StringComparer.OrdinalIgnoreCase)), 2000);
        if (row.Emails == "") row.Emails = null;
        row.Phone = Trunc(NullIfBlank(request.Phone) ?? (id == null ? row.Phone ?? NullIfBlank(catalog?.Phone) : null), 100);
        row.SourceMethod = PreferredSupplierMethods.Normalize(request.SourceMethod ?? row.SourceMethod);
        row.Note = Trunc(NullIfBlank(request.Note) ?? (id == null ? row.Note : null), 500);
        await _db.SaveChangesAsync();
        if (supplierId != null && row.SupplierId != supplierId) await SetPreferredSupplierAsync(row.Id, supplierId);
        else if (supplierId == null && id != null && request.SupplierId == 0 && row.SupplierId != null) await SetPreferredSupplierAsync(row.Id, null);

        var saved = await _db.Set<Preferred>().AsNoTracking().Include(p => p.Supplier).FirstAsync(p => p.Id == row.Id);
        return ToResponse(saved, await UserNamesAsync([saved.AddedByUserId]));
    }

    public async Task<bool> UpdatePreferredAsync(long id, PpUpdatePreferredRequest request)
    {
        var row = await _db.Set<Preferred>().FirstOrDefaultAsync(p => p.Id == id && p.Kind == SupplierKind);
        if (row == null) return false;
        if (request.SourceMethod != null) row.SourceMethod = PreferredSupplierMethods.Normalize(request.SourceMethod);
        if (request.Note != null) row.Note = Trunc(NullIfBlank(request.Note), 500);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemovePreferredAsync(long id) =>
        await _db.Set<Preferred>().Where(p => p.Id == id && p.Kind == SupplierKind).ExecuteDeleteAsync() > 0;

    /// <summary>
    /// Links a preferred supplier to a catalog supplier (needed to put its prices on RFQs): its lines
    /// being sourced point there, its emails join the catalog contacts, and its name is remembered so
    /// files with that company name match the catalog supplier too.
    /// </summary>
    public async Task<PpLinkPreferredResponse?> LinkPreferredAsync(long id, PpLinkPreferredRequest request)
    {
        var row = await _db.Set<Preferred>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.Kind == SupplierKind);
        if (row == null) return null;
        var supplierId = request.SupplierId is > 0 ? request.SupplierId : null;
        if (supplierId != null && !await _db.Set<Supplier>().AnyAsync(s => s.Id == supplierId))
            throw new InvalidOperationException("Supplier not found.");
        return await SetPreferredSupplierAsync(id, supplierId);
    }

    private async Task<PpLinkPreferredResponse> SetPreferredSupplierAsync(long preferredId, long? supplierId)
    {
        var response = new PpLinkPreferredResponse();
        var row = await _db.Set<Preferred>().FirstAsync(p => p.Id == preferredId);
        row.SupplierId = supplierId;
        await _db.SaveChangesAsync();
        if (supplierId == null) return response;

        response.LinesUpdated = await _db.Set<PendingSupplierQuote>()
            .Where(p => p.Source == Pp && p.Status == Pending && p.PreferredId == preferredId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SupplierId, supplierId));

        foreach (var email in EmailsOf(row))
        {
            var (added, heldBy) = await _quotes.AddEmailToSupplierAsync(supplierId.Value, email);
            if (added) response.EmailsAdded.Add(email);
            else if (heldBy != null) response.EmailsOnOtherSupplier.Add($"{email} ({heldBy})");
        }

        if (row.NameKey.Length > 0 && !await _db.Set<SupplierAlias>().AnyAsync(a => a.AliasKey == row.NameKey))
        {
            _db.Set<SupplierAlias>().Add(new SupplierAlias
            {
                AliasKey = row.NameKey,
                Alias = row.Name,
                SupplierId = supplierId.Value,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = row.AddedByUserId,
            });
            await _db.SaveChangesAsync();
        }
        return response;
    }

    /// <summary>
    /// Name, email or contact search. An email ("y@abc.com") or a domain ("abc.com") also finds every
    /// supplier using that company domain - those come first (DomainMatch). Free-mail domains (gmail,
    /// outlook...) never match by domain.
    /// </summary>
    public async Task<List<PpSupplierOption>> SearchSuppliersAsync(string? q)
    {
        var query = _db.Set<Supplier>().AsNoTracking().Where(s => s.IsActive && s.Status != "Rejected" && s.Dependency != "Disabled");
        var term = q?.Trim();
        var domain = DomainOf(term);
        if (!string.IsNullOrEmpty(term))
        {
            var at = domain == null ? null : "@" + domain;
            query = query.Where(s => s.Name.Contains(term)
                || (s.Email != null && s.Email.Contains(term))
                || (s.Contacts != null && s.Contacts.Contains(term))
                || (at != null && ((s.Email != null && s.Email.Contains(at)) || (s.Contacts != null && s.Contacts.Contains(at)))));
        }
        var list = await query
            .OrderBy(s => s.Status == "Approved" ? 0 : 1).ThenBy(s => s.Name)
            .Take(60)
            .ToListAsync();
        var ids = list.Select(s => s.Id).ToList();
        var preferredIds = (await _db.Set<Preferred>().AsNoTracking()
                .Where(p => p.Kind == SupplierKind && p.SupplierId != null && ids.Contains(p.SupplierId.Value))
                .Select(p => p.SupplierId!.Value)
                .ToListAsync())
            .ToHashSet();
        return list
            // LIKE "@abc.com" also hits "@abc.com.tr" - the exact domain check runs here.
            .Select(s => (Supplier: s, ByDomain: domain != null && SupplierEmails.HasDomain(s, domain)))
            .OrderByDescending(x => x.ByDomain)
            .Take(30)
            .Select(x => new PpSupplierOption
            {
                Id = x.Supplier.Id,
                Name = x.Supplier.Name,
                Email = x.Supplier.Email,
                Status = x.Supplier.Status,
                IsPreferred = preferredIds.Contains(x.Supplier.Id),
                DomainMatch = x.ByDomain,
            })
            .ToList();
    }

    private static readonly Regex DomainPattern = new(@"^[a-z0-9\-]+(\.[a-z0-9\-]+)+$", RegexOptions.Compiled);

    /// <summary>"y@abc.com" or "abc.com" -> "abc.com"; null for names and free-mail domains.</summary>
    private static string? DomainOf(string? term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Contains(' ')) return null;
        var t = term.Trim().ToLowerInvariant();
        var domain = t.Contains('@') ? t[(t.LastIndexOf('@') + 1)..] : t;
        if (!DomainPattern.IsMatch(domain)) return null;
        return SupplierEmails.IsGenericDomain(domain) ? null : domain;
    }

    // ──── Sourcing → Done ────

    public async Task<PpStartSourcingResponse> StartSourcingAsync(long userId, PpStartSourcingRequest request)
    {
        var response = new PpStartSourcingResponse();
        var parts = await LoadPartsAsync(userId, false, PreferredTracks.Ils);
        var index = IndexByKey(parts);
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var preferredIds = request.Suppliers.Select(s => s.PreferredId).Distinct().ToList();
        var preferreds = await _db.Set<Preferred>()
            .Where(p => p.Kind == SupplierKind && preferredIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        var matcher = await LoadPreferredMatcherAsync();
        var offers = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Include(o => o.Upload)
            .Where(o => o.Upload.UserId == userId && o.Upload.UploadedAt >= since)
            .ToListAsync();
        var preferredByCompany = offers.GroupBy(o => o.CompanyKey)
            .ToDictionary(g => g.Key, g => g.Select(o => matcher.Match(o.SupplierId, o.Email, o.CompanyKey)).FirstOrDefault(m => m != null)?.Id);
        var open = await _db.Set<PendingSupplierQuote>()
            .Where(p => p.Source == Pp && p.Status == Pending && p.CreatedByUserId == userId
                && p.PreferredId != null && preferredIds.Contains(p.PreferredId.Value))
            .ToListAsync();
        var created = new List<PendingSupplierQuote>();

        foreach (var pick in request.Suppliers.DistinctBy(s => s.PreferredId))
        {
            if (!preferreds.TryGetValue(pick.PreferredId, out var pref)) continue;
            var method = PreferredSupplierMethods.Normalize(pick.SourceMethod ?? pref.SourceMethod);
            // The method picked here becomes the supplier's default.
            if (pick.SourceMethod != null) pref.SourceMethod = method;

            var picked = offers
                .Where(o => preferredByCompany[o.CompanyKey] == pref.Id && (pick.OfferIds.Count == 0 || pick.OfferIds.Contains(o.Id)))
                .ToList();
            // A catalog supplier is only needed at Done: the preferred's link, else what the file rows
            // matched (kept as the preferred's link). None = the user links one before Done.
            pref.SupplierId ??= picked.Select(o => o.SupplierId).FirstOrDefault(id => id != null);

            // One line per part and condition; the row with a price (else the most stock) fills it in.
            var lines = picked
                .SelectMany(o => PartsFor(index, o.PartKey, o.AltKey).Select(p => (Part: p.Part, Offer: o)))
                .GroupBy(x => (x.Part.Id, CondKey(x.Offer.Condition)))
                .Select(g => g.OrderByDescending(x => x.Offer.Price != null).ThenByDescending(x => x.Offer.Qty ?? 0).First());

            foreach (var (part, offer) in lines)
            {
                var existing = open.FirstOrDefault(l => l.PreferredId == pref.Id
                    && PnKey(l.PartNumber) == part.NameKey && CondKey(l.Condition) == CondKey(offer.Condition));
                if (existing != null)
                {
                    existing.SourceMethod = method;
                    existing.SupplierId ??= pref.SupplierId;
                    existing.PreferredPartId ??= part.Id;
                    response.Existing++;
                    response.LineIds.Add(existing.Id);
                    continue;
                }
                var line = new PendingSupplierQuote
                {
                    Source = Pp,
                    SourceMethod = method,
                    CreatedByUserId = userId,
                    PreferredId = pref.Id,
                    PreferredPartId = part.Id,
                    SupplierId = pref.SupplierId,
                    SupplierEmail = Trunc(offer.Email ?? EmailsOf(pref).FirstOrDefault(), 320),
                    SupplierNameRaw = Trunc(pref.Name, 300),
                    EmailSubject = Trunc($"Procurement Plus · {offer.Upload.FileName}", 500),
                    PartNumber = part.Name,
                    // Listed under one of our alternatives: that is what this supplier offers.
                    Alt = offer.PartKey != part.NameKey ? Trunc(offer.PartNo, 200) : null,
                    Price = offer.Price,
                    PriceRaw = offer.PriceRaw,
                    Qty = offer.Qty,
                    Condition = Trunc(offer.Condition?.ToUpperInvariant(), 100),
                    Status = Pending,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Set<PendingSupplierQuote>().Add(line);
                open.Add(line);
                created.Add(line);
                AddEvent(part.Id, userId, "Sourcing", $"Sourcing from {pref.Name} ({PreferredSupplierMethods.Normalize(method)})");
                response.Created++;
            }
        }
        await _db.SaveChangesAsync();
        response.LineIds = response.LineIds.Concat(created.Select(l => l.Id)).Distinct().ToList();
        return response;
    }

    public async Task<List<PpQuoteLineResponse>> GetSourcingAsync(long userId, bool everyone)
    {
        var ids = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => p.Source == Pp && p.Status == Pending && (everyone || p.CreatedByUserId == userId))
            .Select(p => p.Id)
            .ToListAsync();
        return await BuildLinesAsync(ids);
    }

    public Task<bool> IsLineAsync(long id) =>
        _db.Set<PendingSupplierQuote>().AnyAsync(p => p.Id == id && p.Source == Pp && (p.Status == Pending || p.Status == Accepted));

    public async Task<PpQuoteLineResponse?> UpdateLineAsync(long id, UpdatePendingSupplierQuoteRequest request)
    {
        if (!await IsLineAsync(id)) return null;
        var updated = await _quotes.UpdateAsync(id, request);
        return updated == null ? null : (await BuildLinesAsync([id])).FirstOrDefault();
    }

    public async Task<int> RemoveLinesAsync(long userId, List<long> ids)
    {
        var now = DateTime.UtcNow;
        var gone = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => ids.Contains(p.Id) && p.Source == Pp && p.Status == Pending && p.PreferredPartId != null)
            .ToListAsync();
        foreach (var l in gone)
            AddEvent(l.PreferredPartId!.Value, userId, "Removed", $"{l.Supplier?.Name ?? l.SupplierNameRaw ?? "Supplier"} removed (no quote)");
        await _db.SaveChangesAsync();
        return await _db.Set<PendingSupplierQuote>()
            .Where(p => ids.Contains(p.Id) && p.Source == Pp && p.Status == Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, Removed)
                .SetProperty(p => p.ReviewedAt, (DateTime?)now)
                .SetProperty(p => p.ReviewedByUserId, (long?)userId));
    }

    public async Task<PpDoneResponse> DoneAsync(long userId, List<long> ids)
    {
        var mine = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Source == Pp && p.Status == Pending)
            .Select(p => p.Id)
            .ToListAsync();
        // Same as accepting an email quote; a part with no open RFQ is still Done and fills new RFQs.
        var accepted = await _quotes.AcceptAsync(mine, userId, allowWithoutRfq: true);
        var lines = await BuildLinesAsync(mine);
        foreach (var l in lines.Where(l => l.Status == Accepted && l.PreferredPartId != null))
        {
            var rfqs = l.WrittenRfqs.Count > 0 ? " → " + string.Join(", ", l.WrittenRfqs.Select(r => r.RfqName)) : " → new RFQs (7 days)";
            AddEvent(l.PreferredPartId!.Value, userId, "Done",
                $"Done: {l.PreferredName ?? l.Line.SupplierName ?? l.Line.SupplierNameRaw} {Money(l.Line.Price)} {l.Line.Condition}".TrimEnd() + rfqs);
        }
        await _db.SaveChangesAsync();
        return new PpDoneResponse
        {
            Summary = new PpDoneSummary { Accepted = accepted.Accepted, QuotesWritten = accepted.QuotesWritten, Failed = accepted.Failed },
            Lines = lines,
        };
    }

    public async Task<PpDoneListResponse> GetDoneAsync(int? days, long? byUserId, string? search)
    {
        var query = _db.Set<PendingSupplierQuote>().AsNoTracking().Where(p => p.Source == Pp && p.Status == Accepted);
        if (days is > 0)
        {
            var since = DateTime.UtcNow.AddDays(-days.Value);
            query = query.Where(p => p.ReviewedAt >= since);
        }
        var userIds = await query.Where(p => p.ReviewedByUserId != null).Select(p => p.ReviewedByUserId!.Value).Distinct().ToListAsync();
        if (byUserId != null) query = query.Where(p => p.ReviewedByUserId == byUserId);
        var q = search?.Trim();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(p => p.PartNumber.Contains(q) || (p.Alt != null && p.Alt.Contains(q)) || (p.Supplier != null && p.Supplier.Name.Contains(q)));
        var ids = await query.OrderByDescending(p => p.ReviewedAt).Take(1000).Select(p => p.Id).ToListAsync();
        var names = await UserNamesAsync(userIds.Cast<long?>());
        return new PpDoneListResponse
        {
            Lines = await BuildLinesAsync(ids),
            Users = names.Select(n => new PpUserOption { Id = n.Key, Name = n.Value }).OrderBy(u => u.Name).ToList(),
        };
    }

    /// <summary>The email-quote line (with RFQ matches when pending) plus supplier, method and written RFQs.</summary>
    private async Task<List<PpQuoteLineResponse>> BuildLinesAsync(ICollection<long> ids)
    {
        if (ids.Count == 0) return new();
        var lines = await _quotes.GetLinesAsync(ids);
        var raw = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();
        var rawById = raw.ToDictionary(r => r.Id);
        var written = await WrittenRfqsAsync(raw);
        var creators = await UserNamesAsync(raw.Select(r => r.CreatedByUserId));
        var preferredIds = raw.Select(r => r.PreferredId).OfType<long>().Distinct().ToList();
        var preferreds = await _db.Set<Preferred>().AsNoTracking().Where(p => preferredIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        return lines
            .Where(l => rawById.ContainsKey(l.Id))
            .Select(l =>
            {
                var r = rawById[l.Id];
                var pref = r.PreferredId != null ? preferreds.GetValueOrDefault(r.PreferredId.Value) : null;
                return new PpQuoteLineResponse
                {
                    Line = l,
                    PreferredPartId = r.PreferredPartId,
                    Status = r.Status,
                    PreferredId = r.PreferredId,
                    PreferredName = pref?.Name,
                    PreferredEmails = pref != null ? EmailsOf(pref) : new(),
                    SourceMethod = r.SourceMethod,
                    SupplierStatus = r.Supplier?.Status,
                    SupplierMainEmail = r.Supplier?.Email,
                    SupplierPhone = r.Supplier?.Phone,
                    CreatedByUserId = r.CreatedByUserId,
                    CreatedByName = r.CreatedByUserId != null ? creators.GetValueOrDefault(r.CreatedByUserId.Value) : null,
                    WrittenRfqs = written.GetValueOrDefault(l.Id) ?? new(),
                };
            })
            .OrderBy(l => l.PreferredName ?? l.Line.SupplierName).ThenBy(l => l.Line.PartNumber)
            .ToList();
    }

    // ──── Matching helpers ────

    /// <summary>
    /// RFQ items for these part numbers (name, new name, item alt, catalog alternatives), deadline on/after
    /// <paramref name="minDeadline"/>. Pass names as written plus their keys — SQL compares case-insensitively
    /// but not across whitespace.
    /// </summary>
    private async Task<List<RFQItem>> FindRfqItemsAsync(IEnumerable<string> partNames, DateTime minDeadline)
    {
        var names = partNames.Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return new();
        return await _db.Set<RFQItem>().AsNoTracking()
            .Include(i => i.RFQ).ThenInclude(r => r.Customer)
            .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(i => i.RFQ.LeadTime >= minDeadline)
            .Where(i => names.Contains(i.PartNumber.Name)
                        || (i.PartNumber.NewName != null && names.Contains(i.PartNumber.NewName))
                        || (i.Alt != null && names.Contains(i.Alt))
                        || i.PartNumber.Alternatives.Any(a => names.Contains(a.Name)))
            .ToListAsync();
    }

    private static HashSet<string> ItemKeys(RFQItem item) =>
        new[] { item.PartNumber.Name, item.PartNumber.NewName, item.Alt }
            .Concat(item.PartNumber.Alternatives.Select(a => a.Name))
            .Select(PnKey)
            .Where(k => k.Length > 0)
            .ToHashSet();

    private async Task<Dictionary<long, string>> UserNamesAsync(IEnumerable<long?> ids)
    {
        var list = ids.OfType<long>().Distinct().ToList();
        if (list.Count == 0) return new();
        return await _db.Set<User>().AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
    }

    private static string PnKey(string? partNumber) => Whitespace.Replace(partNumber ?? "", "").ToUpperInvariant();

    /// <summary>"a, b; c" → [a, b, c] (comma / semicolon / new line separated lists stored on a preferred).</summary>
    private static List<string> SplitList(string? text) =>
        (text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Alternatives of a preferred part, without its own P/N and duplicates; null when none.</summary>
    private static string? JoinAlternatives(IEnumerable<string?> alternatives, string ownKey)
    {
        var list = alternatives.OfType<string>().Select(a => a.Trim())
            .Where(a => a.Length > 0 && PnKey(a) != ownKey)
            .DistinctBy(PnKey)
            .ToList();
        return list.Count == 0 ? null : Trunc(string.Join(", ", list), 2000);
    }
    private static string CondKey(string? condition) => (condition ?? "").Trim().ToUpperInvariant();
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Trunc(string? value, int max) => value == null || value.Length <= max ? value : value[..max];
}
