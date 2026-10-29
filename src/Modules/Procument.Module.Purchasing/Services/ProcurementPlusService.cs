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
    // track "All" = the Parts tab (every track + saved-RFQ items not on the list); origin = parts|project|wechat|rfq.
    Task<PpBoardPage> GetBoardAsync(long? userId, string track, bool includeHidden, string? search, string? status, string? origin, string? sort, int page, int pageSize);
    Task<List<string>> GetBoardPartNumbersAsync(long? userId, string track, bool includeHidden, string? search, string? status, string? origin, string? sort);
    Task<PpSupplierPage> GetSupplierViewAsync(long? userId, string track, string? search, int page, int pageSize);
    Task<PpRfqCoverage?> GetRfqCoverageAsync(PpUserScope scope, long? userId, long rfqId);
    Task<PpPartHistory?> GetPartHistoryAsync(long? userId, long id);
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
    Task<List<PpPreferredSupplierResponse>> GetPreferredAsync(long ownerId);
    Task<PpPreferredSupplierResponse> SavePreferredAsync(long ownerId, long actorId, long? id, PpSavePreferredSupplierRequest request);
    Task<bool> UpdatePreferredAsync(long ownerId, long id, PpUpdatePreferredRequest request);
    Task<bool> RemovePreferredAsync(long ownerId, long id);
    Task<PpLinkPreferredResponse?> LinkPreferredAsync(long ownerId, long id, PpLinkPreferredRequest request);
    Task<List<PpSupplierOption>> SearchSuppliersAsync(long ownerId, string? q);

    // Sourcing → Done
    Task<PpStartSourcingResponse> StartSourcingAsync(long userId, PpStartSourcingRequest request);
    Task<List<PpQuoteLineResponse>> GetSourcingAsync(long userId, bool everyone);
    Task<bool> IsLineAsync(long id);
    Task<PpQuoteLineResponse?> UpdateLineAsync(long id, UpdatePendingSupplierQuoteRequest request);
    Task<PpQuoteLineResponse?> SetLineMethodsAsync(long id, List<string> methods);
    Task<int> RemoveLinesAsync(long userId, List<long> ids);
    Task<List<string>> PartsNotInDatabaseAsync(List<long> ids);
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
public partial class ProcurementPlusService : IProcurementPlusService
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
    private readonly ICustomerQuotePriceLookup _customerQuotes;

    public ProcurementPlusService(DbContext db, IPendingSupplierQuoteService quotes, ICustomerQuotePriceLookup customerQuotes)
    {
        _db = db;
        _quotes = quotes;
        _customerQuotes = customerQuotes;
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
                    new[] { i.NewName, i.Alt }.Concat(i.Alternatives).Concat(pick.Alternatives ?? []).OfType<string>().ToList(),
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
                    (c.Alternatives ?? []).Concat(match == null ? [] : new[] { match.NewName }.OfType<string>().Concat(match.Alternatives)).ToList(),
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
        var touched = new List<Preferred>();
        foreach (var w in wanted.GroupBy(w => PnKey(w.Name)).Select(g => g.First()))
        {
            var key = PnKey(w.Name);
            if (key.Length == 0) continue;
            if (byKey.TryGetValue(key, out var existing))
            {
                touched.Add(existing);
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
            touched.Add(part);
            events.Add((part, "Added", $"Added to {track}{TargetText(w.Target)}"));
            result.Added++;
        }
        await _db.SaveChangesAsync();
        foreach (var (part, kind, text) in events) AddEvent(part.Id, scope.UserId, kind, text);
        await _db.SaveChangesAsync();
        result.Ids = touched.Select(p => p.Id).ToList();
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
    private IQueryable<Preferred> MyParts(long? userId, string kind = PartKind) =>
        _db.Set<Preferred>().Where(p => p.Kind == kind && (userId == null || p.UserId == userId));

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
        string? origin, string? sort, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxBoardPageSize);
        var sel = await SelectBoardAsync(userId, track, includeHidden, search, status, origin, sort);
        var lastPage = Math.Max(1, (sel.Ordered.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, lastPage);
        var pageParts = sel.Ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PpBoardPage
        {
            Items = await BuildBoardPartsAsync(pageParts, userId, sel.Heads, sel.PartOf),
            Total = sel.Ordered.Count,
            Page = page,
            PageSize = pageSize,
            Counts = sel.Counts,
        };
    }

    /// <summary>Every part number on a board with its filters (no paging) — the Parts tab's "Copy all".</summary>
    public async Task<List<string>> GetBoardPartNumbersAsync(long? userId, string track, bool includeHidden, string? search, string? status,
        string? origin, string? sort)
    {
        var sel = await SelectBoardAsync(userId, track, includeHidden, search, status, origin, sort);
        return sel.Ordered.Select(p => p.Part.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The Parts tab: every track at once, plus the items of the user's saved RFQs that are not on the list.</summary>
    public const string AllTrack = "All";
    /// <summary>Track of a Parts-tab entry that only stands for a saved-RFQ item (not on the list yet).</summary>
    public const string RfqTrack = "RFQ";

    private static string BoardTrack(string? track) =>
        string.Equals(track?.Trim(), AllTrack, StringComparison.OrdinalIgnoreCase) ? AllTrack : PreferredTracks.Normalize(track);

    /// <summary>The tab a Parts-tab entry comes from: parts (ILS) / project / wechat / rfq.</summary>
    private static string OriginOf(Preferred p) => p.Track switch
    {
        PreferredTracks.Project => "project",
        PreferredTracks.WeChat => "wechat",
        RfqTrack => "rfq",
        _ => "parts",
    };

    private sealed record BoardSelection(List<PartKeys> Ordered, PpBoardCounts Counts, List<LineHead> Heads, Dictionary<long, long> PartOf);

    /// <summary>A board's parts after search, origin and status filters, sorted — with the counts for the filter chips.</summary>
    private async Task<BoardSelection> SelectBoardAsync(long? userId, string track, bool includeHidden, string? search, string? status,
        string? origin, string? sort)
    {
        track = BoardTrack(track);
        var parts = await BoardPartsAsync(userId, track, includeHidden);
        // The user's own RFQ quotes of the last two weeks become supplier lines under their parts first.
        if (userId != null) await SyncRecentRfqQuotesAsync(userId.Value, parts);
        var heads = await LineHeadsAsync(userId);
        var partOf = MapLinesToParts(heads, parts.Where(p => p.Part.Id > 0).Select(p => p.Part));
        var linesByPart = heads.Where(h => partOf.ContainsKey(h.Id)).GroupBy(h => partOf[h.Id]).ToDictionary(g => g.Key, g => g.ToList());
        var withFiles = track is PreferredTracks.Ils or AllTrack;
        var inFiles = withFiles ? await PartsWithOffersAsync(userId, parts) : new HashSet<long>();

        var q = search?.Trim();
        var key = PnKey(q);
        var searched = string.IsNullOrEmpty(q)
            ? parts
            : parts.Where(p => p.Keys.Any(k => k.Contains(key))
                || (p.Part.Description ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (linesByPart.GetValueOrDefault(p.Part.Id) ?? []).Any(l => (l.SupplierNameRaw ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)))
              .ToList();

        var origins = searched.GroupBy(p => OriginOf(p.Part)).ToDictionary(g => g.Key, g => g.Count());
        var byOrigin = string.IsNullOrEmpty(origin) || origin == "all"
            ? searched
            : searched.Where(p => OriginOf(p.Part) == origin).ToList();

        var statusOf = byOrigin.ToDictionary(p => p.Part.Id, p => StatusOf(linesByPart.GetValueOrDefault(p.Part.Id)));
        var counts = new PpBoardCounts
        {
            All = byOrigin.Count,
            NoSupplier = statusOf.Values.Count(v => v == "none"),
            Sourcing = statusOf.Values.Count(v => v == "sourcing"),
            Ready = statusOf.Values.Count(v => v == "ready"),
            Done = statusOf.Values.Count(v => v == "done"),
            NotInFiles = withFiles ? byOrigin.Count(p => !inFiles.Contains(p.Part.Id)) : 0,
            Origins = origins,
        };

        var filtered = (status ?? "all") switch
        {
            "none" or "sourcing" or "ready" or "done" => byOrigin.Where(p => statusOf[p.Part.Id] == status).ToList(),
            "nofiles" => byOrigin.Where(p => !inFiles.Contains(p.Part.Id)).ToList(),
            _ => byOrigin,
        };

        IEnumerable<PartKeys> sorted = sort switch
        {
            "pn" => filtered.OrderBy(p => p.Part.Name, StringComparer.OrdinalIgnoreCase),
            "suppliers" => filtered.OrderByDescending(p => linesByPart.GetValueOrDefault(p.Part.Id)?.Count ?? 0).ThenBy(p => p.Part.Name),
            "deadline" => await SortByDeadlineAsync(filtered),
            _ => filtered.OrderByDescending(p => p.Part.AddedAt).ThenBy(p => p.Part.Name),
        };
        return new BoardSelection(sorted.ToList(), counts, heads, partOf);
    }

    /// <summary>A board's parts: one track, or (Parts tab) every track plus the saved-RFQ items not on the list.</summary>
    private async Task<List<PartKeys>> BoardPartsAsync(long? userId, string track, bool includeHidden)
    {
        if (track != AllTrack) return await LoadPartsAsync(userId, includeHidden, track);
        var all = await LoadPartsAsync(userId, true);
        var shown = includeHidden ? all : all.Where(p => p.Part.HiddenAt == null).ToList();
        return shown.Concat(await MyRfqItemPartsAsync(userId, all)).ToList();
    }

    /// <summary>
    /// Items of the user's saved RFQs (By RFQ) whose part is not on their list (shown or removed): one entry per
    /// part number, as a stand-in part (Id = -RFQ item id, Track = RFQ, never saved) so the Parts tab can list,
    /// search and match it against the files. Adding a supplier to it puts it on the list first.
    /// </summary>
    private async Task<List<PartKeys>> MyRfqItemPartsAsync(long? userId, List<PartKeys> listed)
    {
        var saved = await _db.Set<Preferred>().AsNoTracking()
            .Where(p => p.Kind == PreferredKinds.Rfq && p.RfqId != null && (userId == null || p.UserId == userId))
            .OrderByDescending(p => p.AddedAt)
            .Select(p => new { p.UserId, RfqId = p.RfqId!.Value, p.AddedAt })
            .ToListAsync();
        if (saved.Count == 0) return new();
        var rfqIds = saved.Select(s => s.RfqId).Distinct().ToList();
        var items = await _db.Set<RFQItem>().AsNoTracking()
            .Include(i => i.RFQ).ThenInclude(r => r.Customer)
            .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(i => rfqIds.Contains(i.RFQId))
            .OrderBy(i => i.Id)
            .ToListAsync();

        var listedKeys = listed.GroupBy(p => p.Part.UserId ?? 0).ToDictionary(g => g.Key, g => g.SelectMany(p => p.Keys).ToHashSet());
        var result = new List<PartKeys>();
        var byOwnerKey = new Dictionary<(long, string), PartKeys>();
        foreach (var s in saved)
        {
            var owner = s.UserId ?? 0;
            var own = listedKeys.GetValueOrDefault(owner);
            foreach (var i in items.Where(i => i.RFQId == s.RfqId))
            {
                var keys = ItemKeys(i);
                var nameKey = PnKey(i.PartNumber.Name);
                if (nameKey.Length == 0 || (own != null && keys.Overlaps(own))) continue;
                var from = new PpRfqRef
                {
                    RfqId = i.RFQId,
                    RfqName = i.RFQ.Name,
                    Deadline = i.RFQ.LeadTime,
                    CustomerCode = i.RFQ.Customer?.CustomerCode,
                    Qty = i.Qty,
                };
                if (byOwnerKey.TryGetValue((owner, nameKey), out var same))
                {
                    same.Keys.UnionWith(keys);
                    same.Names.AddRange(keys.Where(k => !same.Names.Contains(k, StringComparer.OrdinalIgnoreCase)));
                    if (!same.FromRfqs!.Any(r => r.RfqId == i.RFQId)) same.FromRfqs!.Add(from);
                    continue;
                }
                var alternatives = new[] { i.PartNumber.NewName, i.Alt }.Concat(i.PartNumber.Alternatives.Select(a => a.Name))
                    .Where(a => !string.IsNullOrWhiteSpace(a) && PnKey(a) != nameKey)
                    .Select(a => a!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var stand = new Preferred
                {
                    Id = -i.Id,
                    Kind = PartKind,
                    UserId = s.UserId,
                    PartNumberId = i.PartNumberId,
                    Name = i.PartNumber.Name,
                    NameKey = nameKey,
                    Description = i.PartNumber.Description,
                    Qty = i.Qty,
                    Condition = i.Condition,
                    Track = RfqTrack,
                    AddedAt = s.AddedAt,
                };
                var names = new[] { i.PartNumber.Name }.Concat(alternatives).Concat(keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var entry = new PartKeys(stand, alternatives, keys, names, new List<PpRfqRef> { from });
                byOwnerKey[(owner, nameKey)] = entry;
                result.Add(entry);
            }
        }
        return await WithFamiliesAsync(result);
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
        var events = await EventsAsync(ids.Where(id => id > 0).ToList());
        var offers = (await OfferRowsAsync(parts, userId))
            .GroupBy(r => r.PartId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Price == null ? 1 : 0).ThenBy(r => r.Price).ToList());
        var emailOf = await EmailLinesAsync(parts, lines);
        return built.Select(p => new PpBoardPart
        {
            Part = p,
            Lines = lines.Where(l => partOf.GetValueOrDefault(l.Line.Id) == p.Id)
                .OrderBy(l => l.Status == Pending ? 0 : 1)
                .ThenBy(l => l.Line.Price == null ? 1 : 0)
                .ThenBy(l => l.Line.Price)
                .ToList(),
            Events = events.GetValueOrDefault(p.Id) ?? new(),
            Offers = offers.GetValueOrDefault(p.Id) ?? new(),
            EmailLines = emailOf.GetValueOrDefault(p.Id) ?? new(),
        }).ToList();
    }

    /// <summary>
    /// Email Quotes lines (MailFlow) for these parts: pending, or accepted in the last AutoApplyDays. A part's
    /// own line the same supplier quoted by email (same part + condition, EmailQuoteMatch) gets EmailQuote set and
    /// that email line is not listed again; the rest come back per part id, newest first.
    /// </summary>
    private async Task<Dictionary<long, List<PpQuoteLineResponse>>> EmailLinesAsync(List<PartKeys> parts, List<PpQuoteLineResponse> lines)
    {
        var names = parts.SelectMany(p => p.Names).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return new();
        var since = DateTime.UtcNow.AddDays(-PendingSupplierQuoteService.AutoApplyDays);
        var emails = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(q => q.Source == null && (q.Status == Pending || (q.Status == Accepted && q.ReviewedAt >= since))
                && (names.Contains(q.PartNumber) || (q.Alt != null && names.Contains(q.Alt))))
            .OrderByDescending(q => q.EmailDate ?? q.CreatedAt)
            .Take(500)
            .ToListAsync();
        if (emails.Count == 0) return new();

        // Own lines answered by an email (being priced ones only — Done lines keep what was written).
        var ownIds = lines.Where(l => l.Status == Pending).Select(l => l.Line.Id).ToList();
        var own = await _db.Set<PendingSupplierQuote>().AsNoTracking().Where(q => ownIds.Contains(q.Id)).ToListAsync();
        var used = new HashSet<long>();
        foreach (var o in own)
        {
            var email = emails.FirstOrDefault(e => EmailQuoteMatch.Matches(o, e));
            if (email == null) continue;
            used.Add(email.Id);
            var l = lines.First(x => x.Line.Id == o.Id);
            l.EmailQuote = new PpEmailQuoteRef
            {
                Id = email.Id,
                Subject = email.EmailSubject,
                Date = email.EmailDate ?? email.CreatedAt,
                SupplierEmail = email.SupplierEmail,
                Price = email.Price,
                Status = email.Status,
            };
        }

        var rest = emails.Where(e => !used.Contains(e.Id)).ToList();
        var built = (await BuildLinesAsync(rest.Select(e => e.Id).ToList())).ToDictionary(l => l.Line.Id);
        var result = new Dictionary<long, List<PpQuoteLineResponse>>();
        foreach (var p in parts)
        {
            var mine = rest.Where(e => EmailQuoteMatch.PartKeys(e).Any(p.Keys.Contains))
                .Select(e => built.GetValueOrDefault(e.Id))
                .OfType<PpQuoteLineResponse>()
                .ToList();
            if (mine.Count > 0) result[p.Part.Id] = mine;
        }
        return result;
    }

    /// <summary>
    /// Rows for these parts in their owner's current files (the last FreshDays) — one row per (file row, part),
    /// with the preferred supplier the company matched. Feeds Suppliers available and every part card.
    /// </summary>
    private async Task<List<PpOfferRow>> OfferRowsAsync(List<PartKeys> parts, long? userId)
    {
        var index = IndexByKey(parts);
        var allKeys = index.Keys.ToList();
        if (allKeys.Count == 0) return new();
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var offers = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Upload)
            .Where(o => (userId == null || o.Upload.UserId == userId) && o.Upload.UploadedAt >= since
                && (allKeys.Contains(o.PartKey) || (o.AltKey != null && allKeys.Contains(o.AltKey))))
            .OrderBy(o => o.Id)
            .ToListAsync();
        if (offers.Count == 0) return new();

        // Preferred suppliers are per user (Sam): each file is matched against its owner's list.
        var matchers = await LoadPreferredMatchersAsync(offers.Select(o => o.Upload.UserId));
        // One company = one preferred supplier: the first of its rows that matches decides.
        var preferredByCompany = offers.GroupBy(o => (o.Upload.UserId, o.CompanyKey))
            .ToDictionary(g => g.Key, g => g.Select(o => matchers[o.Upload.UserId].Match(o.SupplierId, o.Email, o.CompanyKey)).FirstOrDefault(m => m != null));
        var state = userId != null ? await SourcingStateAsync(userId.Value) : new Dictionary<(string, string), string>();

        var rows = new List<PpOfferRow>();
        foreach (var o in offers)
        {
            var pref = preferredByCompany[(o.Upload.UserId, o.CompanyKey)];
            // Files are per user: only the part owner's files count for it.
            foreach (var p in PartsFor(index, o.PartKey, o.AltKey).Where(p => p.Part.UserId == o.Upload.UserId))
            {
                rows.Add(new PpOfferRow
                {
                    OfferId = o.Id,
                    UploadId = o.UploadId,
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
                    FileName = o.Upload.FileName,
                    UploadedAt = o.Upload.UploadedAt,
                    SourceMethod = pref != null ? PreferredSupplierMethods.Normalize(pref.SourceMethod) : null,
                    SourcingState = state.GetValueOrDefault((pref != null ? $"p{pref.Id}" : $"s{o.SupplierId}", p.Part.NameKey)),
                });
            }
        }
        return rows;
    }

    /// <summary>"By supplier": every supplier with lines on this board's parts, and those lines (paged by supplier).</summary>
    public async Task<PpSupplierPage> GetSupplierViewAsync(long? userId, string track, string? search, int page, int pageSize)
    {
        track = BoardTrack(track);
        pageSize = Math.Clamp(pageSize, 1, MaxBoardPageSize);
        var parts = await LoadPartsAsync(userId, false, track == AllTrack ? null : track);
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
                CustomerName = r.Customer.Name,
                ReceivedDate = r.ReceivedDate,
                CreatedAt = r.CreatedAt,
                OwnerName = r.User != null ? r.User.Name : null,
                SenderMail = r.SenderMail,
                Notes = r.Notes,
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
                .Select(r => new { r.RFQItemId, r.SupplierId, SupplierName = r.Supplier.Name, r.Price, r.Condition, r.Qty, r.LeadTime, r.Alt, r.CertName, r.TagDate, When = r.UpdatedAt ?? r.CreatedAt })
                .ToListAsync())
            .GroupBy(r => r.RFQItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Price).Select(r => new PpRfqQuote
            {
                SupplierId = r.SupplierId,
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

        // Items not on the list: their rows in the files too, through a stand-in part per item.
        var owner = userId ?? scope.UserId;
        var standIns = await WithFamiliesAsync(items.Where(i => matchOf[i.Id] == null).Select(i =>
        {
            var keys = ItemKeys(i);
            var stand = new Preferred { Id = -i.Id, Kind = PartKind, UserId = owner, Name = i.PartNumber.Name, NameKey = PnKey(i.PartNumber.Name), Track = RfqTrack };
            return new PartKeys(stand, new(), keys, keys.ToList());
        }).ToList());
        var looseOffers = (await OfferRowsAsync(standIns, userId ?? owner))
            .GroupBy(r => -r.PartId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Price == null ? 1 : 0).ThenBy(r => r.Price).ToList());

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
                    Offers = board == null ? looseOffers.GetValueOrDefault(i.Id) ?? new() : new(),
                    HasSupplier = rfqQuotes.Count > 0 || (board?.Lines.Count ?? 0) > 0,
                };
            }).ToList(),
        };
    }

    /// <summary>Older quotes listed per RFQ item (newest first).</summary>
    public const int MaxOlderQuotes = 50;

    /// <summary>
    /// Supplier quotes for each item's part on OTHER RFQs — matched by the item's part number, new name,
    /// RFQ-item alt and its whole alternative family — newest first.
    /// </summary>
    private async Task<Dictionary<long, List<PpRfqQuote>>> OlderQuotesAsync(List<RFQItem> items, long rfqId)
    {
        var keysOf = await ItemFamilyKeysAsync(items);
        return await QuotesByKeysAsync(
            items.ToDictionary(i => i.Id, keysOf),
            items.SelectMany(i => new[] { i.PartNumber.Name, i.PartNumber.NewName, i.Alt }.Concat(i.PartNumber.Alternatives.Select(a => a.Name))),
            rfqId);
    }

    /// <summary>
    /// Supplier quotes on RFQs (newest first, any RFQ but excludeRfqId) for each entry's part — an entry is a set of
    /// P/N keys (name, new name, alternatives); the catalog parts answering to them decide which RFQ items count.
    /// </summary>
    private async Task<Dictionary<long, List<PpRfqQuote>>> QuotesByKeysAsync(Dictionary<long, HashSet<string>> keysOf,
        IEnumerable<string?> rawNames, long? excludeRfqId)
    {
        var names = rawNames
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
            .Where(r => catalogIds.Contains(r.RFQItem.PartNumberId) && (excludeRfqId == null || r.RFQItem.RFQId != excludeRfqId)
                && (r.Type ?? "Procument") != "Shop")
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Take(5000)
            .Select(r => new
            {
                r.RFQItem.PartNumberId,
                r.RFQItem.RFQId,
                RfqName = r.RFQItem.RFQ.Name,
                r.RFQItem.RFQ.Customer.CustomerCode,
                PartName = r.RFQItem.PartNumber.Name,
                r.SupplierId,
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

        return keysOf.ToDictionary(e => e.Key, e =>
        {
            var ids = catalog.Where(c => c.Value.Overlaps(e.Value)).Select(c => c.Key).ToHashSet();
            return records.Where(r => ids.Contains(r.PartNumberId)).Select(r => new PpRfqQuote
            {
                RfqId = r.RFQId,
                RfqName = r.RfqName,
                CustomerCode = r.CustomerCode,
                PartNumber = r.PartName,
                SupplierId = r.SupplierId,
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

    /// <summary>Quotes listed in a part's history (newest first).</summary>
    public const int MaxHistoryQuotes = 200;

    /// <summary>
    /// A part's history (the clock button on a part card): supplier quotes for it on every RFQ — by its name,
    /// alternatives and linked catalog part — and its tracing timeline. id &lt; 0 = a saved-RFQ item not on the list.
    /// </summary>
    public async Task<PpPartHistory?> GetPartHistoryAsync(long? userId, long id)
    {
        string partNumber;
        HashSet<string> keys;
        List<string?> names;
        if (id > 0)
        {
            var part = (await LoadPartsAsync(userId, true)).FirstOrDefault(p => p.Part.Id == id);
            if (part == null) return null;
            (partNumber, keys, names) = (part.Part.Name, part.Keys, part.Names.Cast<string?>().ToList());
        }
        else
        {
            var item = await _db.Set<RFQItem>().AsNoTracking()
                .Include(i => i.PartNumber).ThenInclude(p => p.Alternatives)
                .FirstOrDefaultAsync(i => i.Id == -id);
            if (item == null) return null;
            (partNumber, keys) = (item.PartNumber.Name, (await ItemFamilyKeysAsync([item]))(item));
            names = new[] { item.PartNumber.Name, item.PartNumber.NewName, item.Alt }.Concat(item.PartNumber.Alternatives.Select(a => a.Name)).ToList();
        }

        var quotes = (await QuotesByKeysAsync(new() { [id] = keys }, names, null)).GetValueOrDefault(id) ?? new();
        return new PpPartHistory
        {
            PartNumber = partNumber,
            Quotes = quotes.Take(MaxHistoryQuotes).ToList(),
            QuoteCount = quotes.Count,
            Events = id > 0 ? (await EventsAsync([id])).GetValueOrDefault(id) ?? new() : new(),
        };
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
        var keysOf = await ItemFamilyKeysAsync(items);
        foreach (var r in rfqs)
            r.CoveredCount = items.Count(i => i.RFQId == r.RfqId && (quoted.Contains(i.Id) || keysOf(i).Overlaps(lineKeys)));

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
        // A row of the part owner's files: the line takes its company, price, qty, condition and alt.
        ProcurementPlusOffer? offer = null;
        if (request.OfferId is > 0)
            offer = await _db.Set<ProcurementPlusOffer>().AsNoTracking().Include(o => o.Upload)
                .FirstOrDefaultAsync(o => o.Id == request.OfferId && o.Upload.UserId == part.UserId)
                ?? throw new InvalidOperationException("File row not found.");
        if (request.PreferredId is > 0)
            pref = await _db.Set<Preferred>().AsNoTracking()
                       .FirstOrDefaultAsync(p => p.Id == request.PreferredId && p.Kind == SupplierKind && p.UserId == part.UserId)
                ?? throw new InvalidOperationException("Preferred supplier not found.");
        else if (offer != null)
            pref = (await LoadPreferredMatcherAsync(offer.Upload.UserId)).Match(offer.SupplierId, offer.Email, offer.CompanyKey);
        var supplierId = request.SupplierId is > 0 ? request.SupplierId : pref?.SupplierId ?? offer?.SupplierId;
        if (supplierId != null)
            catalog = await _db.Set<Supplier>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId)
                ?? throw new InvalidOperationException("Supplier not found.");
        var name = pref?.Name ?? catalog?.Name ?? (offer != null ? offer.CompanyRaw : SupplierNameNormalizer.Clean(request.SupplierName));
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Pick or type a supplier.");

        // Listed under one of the part's alternatives: that is what this supplier offers.
        var alt = offer != null && offer.PartKey != part.NameKey ? Trunc(offer.PartNo, 200) : null;
        if (offer != null)
        {
            // Added already (double click, or added before): hand back that line. Each alternative the
            // supplier lists is its own line.
            var condition = CondKey(offer.Condition);
            var open = await _db.Set<PendingSupplierQuote>().AsNoTracking()
                .Where(q => q.Source == Pp && q.Status == Pending && q.PreferredPartId == part.Id)
                .Select(q => new { q.Id, q.SupplierNameRaw, q.Condition, q.Alt })
                .ToListAsync();
            var same = open.FirstOrDefault(q => SupplierNameNormalizer.Key(q.SupplierNameRaw ?? "") == SupplierNameNormalizer.Key(name)
                && CondKey(q.Condition) == condition && PnKey(q.Alt) == PnKey(alt));
            if (same != null) return (await BuildLinesAsync([same.Id])).First();
        }

        var line = new PendingSupplierQuote
        {
            Source = Pp,
            // The methods picked when adding (ILS / Email / Search, one or more), else the usual default.
            SourceMethod = PreferredSupplierMethods.Join(request.Methods)
                ?? (part.Track == PreferredTracks.Ils ? PreferredSupplierMethods.Normalize(pref?.SourceMethod) : part.Track),
            CreatedByUserId = part.UserId,
            PreferredId = pref?.Id,
            PreferredPartId = part.Id,
            SupplierId = catalog?.Id,
            SupplierNameRaw = Trunc(name, 300),
            SupplierEmail = Trunc(offer?.Email ?? (pref != null ? EmailsOf(pref).FirstOrDefault() : null) ?? SupplierEmails.Normalize(catalog?.Email), 320),
            EmailSubject = Trunc(offer != null ? $"Procurement Plus · {offer.Upload.FileName}" : $"Procurement Plus · {part.Track}", 500),
            PartNumber = Trunc(part.Name, 200)!,
            Alt = alt,
            Price = offer?.Price,
            PriceRaw = offer?.PriceRaw,
            // Name only (added from the part's history): the user fills qty / condition / price.
            Qty = request.NameOnly ? null : offer?.Qty ?? part.Qty,
            Condition = request.NameOnly ? null : Trunc(offer?.Condition?.ToUpperInvariant() ?? part.Condition, 100),
            Status = Pending,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<PendingSupplierQuote>().Add(line);
        if (offer != null)
        {
            // The supplier quoted it by email already: the line takes that price (shown as "quoted by email").
            var names = new[] { line.PartNumber, line.Alt }.OfType<string>().ToList();
            var since = DateTime.UtcNow.AddDays(-PendingSupplierQuoteService.AutoApplyDays);
            var emails = await _db.Set<PendingSupplierQuote>().AsNoTracking()
                .Where(q => q.Source == null && q.Price != null && (q.Status == Pending || (q.Status == Accepted && q.ReviewedAt >= since))
                    && (names.Contains(q.PartNumber) || (q.Alt != null && names.Contains(q.Alt))))
                .OrderByDescending(q => q.EmailDate ?? q.CreatedAt)
                .ToListAsync();
            var email = emails.FirstOrDefault(e => EmailQuoteMatch.Matches(line, e));
            if (email != null) EmailQuoteMatch.CopyInto(line, email);
        }
        AddEvent(part.Id, actorId, "Supplier", offer != null ? $"Supplier {name} added from {offer.Upload.FileName}" : $"Supplier {name} added");
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
    /// <remarks>FromRfqs: a stand-in for saved-RFQ items not on the list (Parts tab) — the RFQs asking for it.</remarks>
    private sealed record PartKeys(Preferred Part, List<string> Alternatives, HashSet<string> Keys, List<string> Names, List<PpRfqRef>? FromRfqs = null);

    /// <summary>
    /// Preferred parts of a user (null = every user), optionally of one track (ILS / Project / WeChat).
    /// kind = PpePart: the parts assigned to the user on the new Procurement Plus page. ids = only those parts.
    /// </summary>
    private async Task<List<PartKeys>> LoadPartsAsync(long? userId, bool includeHidden, string? track = null, string kind = PartKind,
        IReadOnlyCollection<long>? ids = null)
    {
        var parts = await MyParts(userId, kind).AsNoTracking()
            .Include(p => p.CatalogPart).ThenInclude(c => c!.Alternatives)
            .Where(p => (includeHidden || p.HiddenAt == null) && (track == null || p.Track == track))
            .Where(p => ids == null || ids.Contains(p.Id))
            .OrderByDescending(p => p.AddedAt).ThenBy(p => p.Name)
            .ToListAsync();
        return await WithFamiliesAsync(parts.Select(p =>
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
        }).ToList());
    }

    /// <summary>
    /// Widens each part's keys, names and alternatives to its whole alternative family — X lists Y and Y
    /// lists Z: a part X answers to Z too (PartFamilies), on RFQs, files, email lines and price history.
    /// </summary>
    private async Task<List<PartKeys>> WithFamiliesAsync(List<PartKeys> parts)
    {
        if (parts.Count == 0) return parts;
        var families = await PartFamilies.LoadAsync(_db, parts.SelectMany(p => p.Names));
        return parts.Select(p =>
        {
            var added = p.Keys.SelectMany(families.FamilyOf).Where(k => !p.Keys.Contains(k)).Distinct().ToList();
            if (added.Count == 0) return p;
            var spellings = added.Select(k => families.NameOf(k) ?? k).ToList();
            return p with
            {
                Alternatives = p.Alternatives.Concat(spellings.Where(n => PnKey(n) != p.Part.NameKey))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Keys = p.Keys.Concat(added).ToHashSet(),
                Names = p.Names.Concat(spellings).Concat(added).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            };
        }).ToList();
    }

    /// <summary>ItemKeys widened to each item's alternative family (items must include PartNumber.Alternatives).</summary>
    private async Task<Func<RFQItem, HashSet<string>>> ItemFamilyKeysAsync(IEnumerable<RFQItem> items)
    {
        var families = await PartFamilies.LoadAsync(_db, items.SelectMany(i =>
            new[] { i.PartNumber.Name, i.PartNumber.NewName, i.Alt }.Concat(i.PartNumber.Alternatives.Select(a => a.Name))));
        return item =>
        {
            var keys = ItemKeys(item);
            keys.UnionWith(keys.SelectMany(families.FamilyOf).ToList());
            return keys;
        };
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
        var matchers = await LoadPreferredMatchersAsync(offers.Select(o => o.UserId));
        var preferredByCompany = offers.GroupBy(o => (o.UserId, o.CompanyKey))
            .ToDictionary(g => g.Key, g => g.Select(o => matchers[o.UserId].Match(o.SupplierId, o.Email, o.CompanyKey)).FirstOrDefault(m => m != null));

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
            var preferredCount = partOffers.Select(o => preferredByCompany[(o.UserId, o.CompanyKey)]?.Id).OfType<long>().Distinct().Count();
            var otherCount = partOffers.Where(o => preferredByCompany[(o.UserId, o.CompanyKey)] == null)
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
                RfqItemId = p.Part.Id < 0 ? -p.Part.Id : null,
                FromRfqs = p.FromRfqs ?? new(),
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

        // Matched rows per file, against everything on the Parts tab (shown parts of every track + saved-RFQ items).
        var keys = (await BoardPartsAsync(userId, AllTrack, false)).SelectMany(p => p.Keys).ToHashSet();
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

        // Names compare loosely: "AAR Corp." = "AAR CORP" = "A.A.R. Corporation" (punctuation and company
        // suffixes ignored). Every usable catalog supplier is read once — a name in a file rarely matches exactly.
        var byName = (await _db.Set<Supplier>().AsNoTracking()
                .Where(s => s.IsActive && s.Status != "Rejected" && s.Dependency != "Disabled")
                .Select(s => new { s.Id, s.Name, s.Status })
                .ToListAsync())
            .Select(s => (Key: CompanyMatchKey(s.Name), s.Id, s.Status))
            .Where(s => s.Key.Length >= 2) // "3M" still counts; a single letter is too loose
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Status == "Approved" ? 0 : 1).ThenBy(s => s.Id).First().Id);

        foreach (var o in offers)
        {
            if (aliases.TryGetValue(o.CompanyKey, out var aliased)) o.SupplierId = aliased;
            else if (o.Email != null && byEmail.TryGetValue(o.Email, out var emailed)) o.SupplierId = emailed;
            else if (byName.TryGetValue(CompanyMatchKey(o.CompanyRaw), out var named)) o.SupplierId = named;
        }

        // One company = one supplier: rows of a company without an email get what its other rows found.
        foreach (var company in offers.GroupBy(o => o.CompanyKey))
        {
            var found = company.Select(o => o.SupplierId).FirstOrDefault(id => id != null);
            if (found == null) continue;
            foreach (var o in company.Where(o => o.SupplierId == null)) o.SupplierId = found;
        }
    }

    private static readonly Regex NonAlphaNum = new(@"[^A-Z0-9]+", RegexOptions.Compiled);
    private static readonly HashSet<string> CompanySuffixes = new(StringComparer.Ordinal)
    {
        "INC", "INCORPORATED", "LLC", "LTD", "LIMITED", "CORP", "CORPORATION", "CO", "COMPANY", "GMBH", "AG",
        "SA", "SAS", "SARL", "SRL", "SPA", "BV", "NV", "PLC", "PTE", "PTY", "LLP", "LP", "KG", "OY", "AB", "AS",
    };

    /// <summary>
    /// A company name for matching only: upper case, "&amp;" = AND, punctuation dropped, a leading THE and trailing
    /// company suffixes (INC, LLC, LTD, CORP, GMBH…) removed, spaces squeezed out. "" when nothing is left.
    /// </summary>
    private static string CompanyMatchKey(string? name)
    {
        var words = NonAlphaNum.Split(SupplierNameNormalizer.Key(name).Replace("&", " AND "))
            .Where(w => w.Length > 0)
            .ToList();
        if (words.Count > 1 && words[0] == "THE") words.RemoveAt(0);
        while (words.Count > 1 && CompanySuffixes.Contains(words[^1])) words.RemoveAt(words.Count - 1);
        return string.Concat(words);
    }

    /// <summary>
    /// Links the user's current file rows that are still without a catalog supplier, for these parts — so a
    /// supplier added (or given that email) in the catalog after the upload is picked up without uploading again.
    /// </summary>
    private async Task RelinkFreshOffersAsync(long userId, List<PartKeys> parts)
    {
        var allKeys = parts.SelectMany(p => p.Keys).Distinct().ToList();
        if (allKeys.Count == 0) return;
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var unlinked = await _db.Set<ProcurementPlusOffer>()
            .Where(o => o.SupplierId == null && o.Upload.UserId == userId && o.Upload.UploadedAt >= since
                && (allKeys.Contains(o.PartKey) || (o.AltKey != null && allKeys.Contains(o.AltKey))))
            .ToListAsync();
        if (unlinked.Count == 0) return;
        await MatchSuppliersAsync(unlinked);
        if (unlinked.Any(o => o.SupplierId != null)) await _db.SaveChangesAsync();
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
        var matcher = await LoadPreferredMatcherAsync(userId);
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
        // Every track: Project / WeChat parts get their file rows (and can be sourced from them) too — and the items
        // of the user's saved RFQs not on the list yet (stand-ins, PartId < 0; sourcing one puts it on the list).
        var listed = await LoadPartsAsync(userId, true);
        var parts = listed.Where(p => p.Part.HiddenAt == null).Concat(await MyRfqItemPartsAsync(userId, listed)).ToList();
        await RelinkFreshOffersAsync(userId, parts);

        var uploads = await _db.Set<ProcurementPlusUpload>().AsNoTracking()
            .Where(u => u.UserId == userId && u.UploadedAt >= since)
            .OrderByDescending(u => u.UploadedAt)
            .ToListAsync();

        // One row per (file row, part) — a row normally answers one part.
        var rows = uploads.Count == 0 ? new List<PpOfferRow>() : await OfferRowsAsync(parts, userId);
        var matcher = await LoadPreferredMatcherAsync(userId);

        response.Uploads = uploads.Select(u => new PpUploadResponse
        {
            Id = u.Id,
            FileName = u.FileName,
            UploadedAt = u.UploadedAt,
            RowCount = u.RowCount,
            MatchedRows = rows.Where(r => r.UploadId == u.Id).Select(r => r.OfferId).Distinct().Count(),
        }).ToList();

        response.Preferred = rows.Where(r => r.PreferredId != null).ToList();
        response.Others = rows.Where(r => r.PreferredId == null).ToList();

        // "No supplier": ILS parts nobody lists (Project / WeChat parts are already worked on by hand).
        var inFiles = rows.Select(r => r.PartId).ToHashSet();
        var missing = parts.Where(p => p.Part.Track == PreferredTracks.Ils && !inFiles.Contains(p.Part.Id)).ToList();
        var openItems = await FindRfqItemsAsync(missing.SelectMany(p => p.Names),
            DateTime.UtcNow.Date.AddDays(-PendingSupplierQuoteService.DeadlineGraceDays));
        response.Missing = missing.Select(p => new PpMissingPart
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

    /// <summary>One user's preferred suppliers — each user has their own list (Sam).</summary>
    private async Task<PreferredMatcher> LoadPreferredMatcherAsync(long userId) =>
        new(await _db.Set<Preferred>().AsNoTracking().Where(p => p.Kind == SupplierKind && p.UserId == userId).ToListAsync());

    /// <summary>A matcher per user, for rows of several users' files at once (every id asked gets one, maybe empty).</summary>
    private async Task<Dictionary<long, PreferredMatcher>> LoadPreferredMatchersAsync(IEnumerable<long> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var rows = ids.Count == 0 ? new List<Preferred>() : await _db.Set<Preferred>().AsNoTracking()
            .Where(p => p.Kind == SupplierKind && p.UserId != null && ids.Contains(p.UserId.Value))
            .ToListAsync();
        var byUser = rows.ToLookup(p => p.UserId!.Value);
        return ids.ToDictionary(id => id, id => new PreferredMatcher(byUser[id]));
    }

    private static List<string> EmailsOf(Preferred p) =>
        SplitList(p.Emails).Select(SupplierEmails.Normalize).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<List<PpPreferredSupplierResponse>> GetPreferredAsync(long ownerId)
    {
        var rows = await _db.Set<Preferred>().AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.Kind == SupplierKind && p.UserId == ownerId)
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
    public async Task<PpPreferredSupplierResponse> SavePreferredAsync(long ownerId, long actorId, long? id, PpSavePreferredSupplierRequest request)
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
            row = await _db.Set<Preferred>().FirstOrDefaultAsync(p => p.Id == id && p.Kind == SupplierKind && p.UserId == ownerId)
                ?? throw new InvalidOperationException("Preferred supplier not found.");
        }
        else
        {
            var all = await _db.Set<Preferred>().Where(p => p.Kind == SupplierKind && p.UserId == ownerId).ToListAsync();
            row = all.FirstOrDefault(p => (supplierId != null && p.SupplierId == supplierId) || p.NameKey == key)
                ?? all.FirstOrDefault(p => EmailsOf(p).Intersect(emails, StringComparer.OrdinalIgnoreCase).Any());
            if (row == null)
            {
                row = new Preferred { Kind = SupplierKind, UserId = ownerId, AddedAt = DateTime.UtcNow, AddedByUserId = actorId };
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

    public async Task<bool> UpdatePreferredAsync(long ownerId, long id, PpUpdatePreferredRequest request)
    {
        var row = await _db.Set<Preferred>().FirstOrDefaultAsync(p => p.Id == id && p.Kind == SupplierKind && p.UserId == ownerId);
        if (row == null) return false;
        if (request.SourceMethod != null) row.SourceMethod = PreferredSupplierMethods.Normalize(request.SourceMethod);
        if (request.Note != null) row.Note = Trunc(NullIfBlank(request.Note), 500);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemovePreferredAsync(long ownerId, long id) =>
        await _db.Set<Preferred>().Where(p => p.Id == id && p.Kind == SupplierKind && p.UserId == ownerId).ExecuteDeleteAsync() > 0;

    /// <summary>
    /// Links a preferred supplier to a catalog supplier (needed to put its prices on RFQs): its lines
    /// being sourced point there, its emails join the catalog contacts, and its name is remembered so
    /// files with that company name match the catalog supplier too.
    /// </summary>
    public async Task<PpLinkPreferredResponse?> LinkPreferredAsync(long ownerId, long id, PpLinkPreferredRequest request)
    {
        var row = await _db.Set<Preferred>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.Kind == SupplierKind && p.UserId == ownerId);
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
    public async Task<List<PpSupplierOption>> SearchSuppliersAsync(long ownerId, string? q)
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
                .Where(p => p.Kind == SupplierKind && p.UserId == ownerId && p.SupplierId != null && ids.Contains(p.SupplierId.Value))
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
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var preferredIds = request.Suppliers.Select(s => s.PreferredId).OfType<long>().Distinct().ToList();
        var preferreds = await _db.Set<Preferred>()
            .Where(p => p.Kind == SupplierKind && p.UserId == userId && preferredIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        var matcher = await LoadPreferredMatcherAsync(userId);
        var offers = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Include(o => o.Upload)
            .Where(o => o.Upload.UserId == userId && o.Upload.UploadedAt >= since)
            .ToListAsync();
        var preferredByCompany = offers.GroupBy(o => o.CompanyKey)
            .ToDictionary(g => g.Key, g => g.Select(o => matcher.Match(o.SupplierId, o.Email, o.CompanyKey)).FirstOrDefault(m => m != null)?.Id);
        // Saved-RFQ items not on the list yet (Suppliers available lists them too): sourcing one puts it on the Parts list first.
        await AddSourcedRfqItemsAsync(userId, request, offers, preferredByCompany);
        var parts = await LoadPartsAsync(userId, false);
        var index = IndexByKey(parts);
        var open = await _db.Set<PendingSupplierQuote>()
            .Where(p => p.Source == Pp && p.Status == Pending && p.CreatedByUserId == userId
                && p.PreferredId != null && preferredIds.Contains(p.PreferredId.Value))
            .ToListAsync();
        var created = new List<PendingSupplierQuote>();

        foreach (var pick in request.Suppliers.Where(s => s.PreferredId != null).DistinctBy(s => s.PreferredId))
        {
            if (!preferreds.TryGetValue(pick.PreferredId!.Value, out var pref)) continue;
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

        // Non-preferred suppliers: the picked rows, as the catalog supplier they matched or the company in the file
        // (that one needs a catalog supplier picked on the line before Done).
        var otherPicks = request.Suppliers.Where(s => s.PreferredId == null && s.OfferIds.Count > 0).ToList();
        if (otherPicks.Count > 0)
        {
            var catalogIds = offers.Select(o => o.SupplierId).OfType<long>().Distinct().ToList();
            var catalogNames = await _db.Set<Supplier>().AsNoTracking()
                .Where(s => catalogIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name);
            var openOther = await _db.Set<PendingSupplierQuote>()
                .Where(p => p.Source == Pp && p.Status == Pending && p.CreatedByUserId == userId && p.PreferredId == null)
                .ToListAsync();
            string Who(long? supplierId, string? name) => supplierId != null ? $"s{supplierId}" : "n" + SupplierNameNormalizer.Key(name ?? "");

            foreach (var pick in otherPicks)
            {
                var method = PreferredSupplierMethods.Normalize(pick.SourceMethod);
                var lines = offers
                    .Where(o => pick.OfferIds.Contains(o.Id) && preferredByCompany[o.CompanyKey] == null)
                    .SelectMany(o => PartsFor(index, o.PartKey, o.AltKey).Select(p => (Part: p.Part, Offer: o)))
                    .GroupBy(x => (Who(x.Offer.SupplierId, x.Offer.CompanyRaw), x.Part.Id, CondKey(x.Offer.Condition)))
                    .Select(g => g.OrderByDescending(x => x.Offer.Price != null).ThenByDescending(x => x.Offer.Qty ?? 0).First());

                foreach (var (part, offer) in lines)
                {
                    var name = offer.SupplierId != null ? catalogNames.GetValueOrDefault(offer.SupplierId.Value) ?? offer.CompanyRaw : offer.CompanyRaw;
                    var who = Who(offer.SupplierId, offer.CompanyRaw);
                    var existing = openOther.FirstOrDefault(l => PnKey(l.PartNumber) == part.NameKey
                        && CondKey(l.Condition) == CondKey(offer.Condition) && Who(l.SupplierId, l.SupplierNameRaw) == who);
                    if (existing != null)
                    {
                        existing.SourceMethod = method;
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
                        PreferredPartId = part.Id,
                        SupplierId = offer.SupplierId,
                        SupplierEmail = Trunc(offer.Email, 320),
                        SupplierNameRaw = Trunc(name, 300),
                        EmailSubject = Trunc($"Procurement Plus · {offer.Upload.FileName}", 500),
                        PartNumber = part.Name,
                        Alt = offer.PartKey != part.NameKey ? Trunc(offer.PartNo, 200) : null,
                        Price = offer.Price,
                        PriceRaw = offer.PriceRaw,
                        Qty = offer.Qty,
                        Condition = Trunc(offer.Condition?.ToUpperInvariant(), 100),
                        Status = Pending,
                        CreatedAt = DateTime.UtcNow,
                    };
                    _db.Set<PendingSupplierQuote>().Add(line);
                    openOther.Add(line);
                    created.Add(line);
                    AddEvent(part.Id, userId, "Sourcing", $"Sourcing from {name} ({method})");
                    response.Created++;
                }
            }
        }

        await _db.SaveChangesAsync();
        response.LineIds = response.LineIds.Concat(created.Select(l => l.Id)).Distinct().ToList();
        return response;
    }

    /// <summary>The saved-RFQ items (not on the list) the picked file rows answer → added to the user's Parts list (ILS).</summary>
    private async Task AddSourcedRfqItemsAsync(long userId, PpStartSourcingRequest request, List<ProcurementPlusOffer> offers,
        Dictionary<string, long?> preferredOf)
    {
        // A preferred pick with no rows listed means every row of that supplier.
        var everyRow = request.Suppliers.Where(s => s.PreferredId != null && s.OfferIds.Count == 0).Select(s => s.PreferredId!.Value).ToHashSet();
        var ids = request.Suppliers.SelectMany(s => s.OfferIds).ToHashSet();
        var picked = offers.Where(o => ids.Contains(o.Id) || (preferredOf[o.CompanyKey] is { } p && everyRow.Contains(p))).ToList();
        if (picked.Count == 0) return;
        var standIns = IndexByKey(await MyRfqItemPartsAsync(userId, await LoadPartsAsync(userId, true)));
        var itemIds = picked.SelectMany(o => PartsFor(standIns, o.PartKey, o.AltKey)).Select(p => -p.Part.Id).Distinct().ToList();
        if (itemIds.Count == 0) return;
        // The user saved these RFQs (visibility was checked then).
        await AddPartsAsync(new PpUserScope(userId, true, []), new PpAddPartsRequest
        {
            Track = PreferredTracks.Ils,
            Items = itemIds.Select(id => new PpAddPartItem { RfqItemId = id }).ToList(),
        });
    }

    public async Task<List<PpQuoteLineResponse>> GetSourcingAsync(long userId, bool everyone)
    {
        var ids = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => p.Source == Pp && p.Status == Pending && (everyone || p.CreatedByUserId == userId))
            .Select(p => p.Id)
            .ToListAsync();
        return await BuildLinesAsync(ids);
    }

    /// <summary>A line the part cards can edit: a Procurement Plus line, or an Email Quotes line shown under a part.</summary>
    public Task<bool> IsLineAsync(long id) =>
        _db.Set<PendingSupplierQuote>().AnyAsync(p => p.Id == id && (p.Source == Pp || p.Source == null) && (p.Status == Pending || p.Status == Accepted));

    public async Task<PpQuoteLineResponse?> UpdateLineAsync(long id, UpdatePendingSupplierQuoteRequest request)
    {
        if (!await IsLineAsync(id)) return null;
        var updated = await _quotes.UpdateAsync(id, request);
        return updated == null ? null : (await BuildLinesAsync([id])).FirstOrDefault();
    }

    /// <summary>How a line being priced is searched (ILS / Email / Search — one or more); shown on the line under its part.</summary>
    public async Task<PpQuoteLineResponse?> SetLineMethodsAsync(long id, List<string> methods)
    {
        var line = await _db.Set<PendingSupplierQuote>().FirstOrDefaultAsync(p => p.Id == id && p.Source == Pp && p.Status == Pending);
        if (line == null) return null;
        line.SourceMethod = PreferredSupplierMethods.Join(methods)
            ?? throw new InvalidOperationException("Pick ILS, Send Email or Search.");
        await _db.SaveChangesAsync();
        return (await BuildLinesAsync([id])).FirstOrDefault();
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

    /// <summary>
    /// Before Done: the part numbers of these lines that are not in the database (PartNumbers, by name or new
    /// name) — custom parts typed by the user. A custom part found there now is linked to it.
    /// </summary>
    public async Task<List<string>> PartsNotInDatabaseAsync(List<long> ids)
    {
        var lines = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Source == Pp && p.Status == Pending)
            .Select(p => new { p.PartNumber, p.PreferredPartId })
            .ToListAsync();
        var partIds = lines.Select(l => l.PreferredPartId).OfType<long>().Distinct().ToList();
        var parts = await _db.Set<Preferred>().Where(p => p.Kind == PartKind && partIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        // Parts picked from RFQs are linked already; the rest are looked up by their name.
        var unlinked = lines
            .Where(l => l.PreferredPartId == null || !parts.TryGetValue(l.PreferredPartId.Value, out var p) || p.PartNumberId == null)
            .Select(l => (Name: l.PreferredPartId != null && parts.TryGetValue(l.PreferredPartId.Value, out var p) ? p.Name : l.PartNumber, l.PreferredPartId))
            .ToList();
        if (unlinked.Count == 0) return new();
        var names = unlinked.Select(u => u.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var catalog = (await _db.Set<PartNumber>().AsNoTracking()
                .Where(p => names.Contains(p.Name) || (p.NewName != null && names.Contains(p.NewName)))
                .Select(p => new { p.Id, p.Name, p.NewName })
                .ToListAsync())
            .SelectMany(p => new[] { p.Name, p.NewName }.OfType<string>().Select(n => (Key: PnKey(n), p.Id)))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var missing = new List<string>();
        foreach (var u in unlinked.DistinctBy(u => PnKey(u.Name)))
        {
            if (catalog.TryGetValue(PnKey(u.Name), out var catalogId))
            {
                if (u.PreferredPartId != null && parts.TryGetValue(u.PreferredPartId.Value, out var part)) part.PartNumberId ??= catalogId;
            }
            else missing.Add(u.Name.Trim());
        }
        await _db.SaveChangesAsync();
        return missing;
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

    // ──── The user's own RFQ quotes → supplier lines under their parts ────

    /// <summary>How far back the user's own RFQ supplier quotes come onto their parts (Sam: two weeks).</summary>
    public const int RecentQuoteDays = 14;

    /// <summary>
    /// Sam 2026-10-20: every supplier quote this user put on an RFQ in the last RecentQuoteDays days shows as an
    /// editable supplier line under the matching part (by P/N, its alternatives, or the quote's Alt), so it can be
    /// edited and Done onto every other RFQ whose deadline is at most a week ago (Accept skips the quote's own item).
    /// Runs on board load and only adds what is missing — a quote never comes back once it was put on the part
    /// (even if that line was removed or Done), and copies written by a Done are not taken for new quotes.
    /// </summary>
    private async Task SyncRecentRfqQuotesAsync(long userId, List<PartKeys> parts)
    {
        var real = parts.Where(p => p.Part.Id > 0 && p.Part.UserId == userId).ToList();
        if (real.Count == 0) return;
        var since = DateTime.UtcNow.AddDays(-RecentQuoteDays);
        var quotes = await _db.Set<ProcumentRecord>().AsNoTracking()
            .Include(r => r.Supplier)
            .Include(r => r.Certificates)
            .Include(r => r.RFQItem).ThenInclude(i => i.PartNumber)
            .Where(r => r.UserId == userId && r.CreatedAt >= since && (r.Type ?? "Procument") != "Shop")
            .OrderByDescending(r => r.CreatedAt)
            .Take(2000)
            .AsSplitQuery()
            .ToListAsync();
        if (quotes.Count == 0) return;

        var partIds = real.Select(p => p.Part.Id).ToList();
        var existing = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(q => q.Source == Pp && q.PreferredPartId != null && partIds.Contains(q.PreferredPartId.Value))
            .Select(q => new { PartId = q.PreferredPartId!.Value, q.SourceSupplierQuoteId, q.SupplierId, q.Condition, q.Alt, q.Price, q.Status, q.ReviewedAt })
            .ToListAsync();
        var byPart = existing.GroupBy(e => e.PartId).ToDictionary(g => g.Key, g => g.ToList());
        var index = IndexByKey(real);
        var now = DateTime.UtcNow;
        var added = new List<(PendingSupplierQuote Line, ProcumentRecord Source)>();
        var taken = new HashSet<(long, long, string, string)>(); // part, supplier, condition, alt — newest quote wins

        foreach (var r in quotes)
        {
            // The part asked for on the RFQ first; else a part the quote's Alt answers to.
            var part = index.GetValueOrDefault(PnKey(r.RFQItem.PartNumber.Name))?.FirstOrDefault()
                ?? (string.IsNullOrWhiteSpace(r.Alt) ? null : index.GetValueOrDefault(PnKey(r.Alt))?.FirstOrDefault());
            if (part == null) continue;
            var cond = CondKey(r.Condition ?? r.RFQItem.Condition);
            var alt = PnKey(r.Alt);
            if (!taken.Add((part.Part.Id, r.SupplierId, cond, alt))) continue;

            var lines = byPart.GetValueOrDefault(part.Part.Id) ?? [];
            bool Same(long? supplierId, string? condition, string? lineAlt) =>
                supplierId == r.SupplierId && CondKey(condition) == cond && PnKey(lineAlt) == alt;
            if (lines.Any(l =>
                    l.SourceSupplierQuoteId == r.Id                                                  // already brought here
                    || (l.Status == Pending && Same(l.SupplierId, l.Condition, l.Alt))               // being priced already
                    || (l.Status == Accepted && l.SupplierId == r.SupplierId && CondKey(l.Condition) == cond
                        && l.Price == r.Price)                                                       // a copy that Done wrote
                    || (l.Status == Removed && l.SourceSupplierQuoteId != null && Same(l.SupplierId, l.Condition, l.Alt)
                        && l.ReviewedAt >= r.CreatedAt)))                                            // removed after this quote
                continue;

            var line = LineFromRfqQuote(r, userId, now);
            line.PreferredPartId = part.Part.Id;
            _db.Set<PendingSupplierQuote>().Add(line);
            added.Add((line, r));
        }
        if (added.Count == 0) return;
        await _db.SaveChangesAsync();
        await CopyRfqQuoteCertificatesAsync(added, now);
    }

    /// <summary>A Procurement Plus line holding every value of the user's RFQ supplier quote.</summary>
    private static PendingSupplierQuote LineFromRfqQuote(ProcumentRecord src, long userId, DateTime now) => new()
    {
        Source = Pp,
        SourceMethod = RfqTrack,
        SourceSupplierQuoteId = src.Id,
        CreatedByUserId = userId,
        EmailSubject = $"Procurement Plus · {RfqTrack}",
        EmailDate = src.CreatedAt,
        SupplierId = src.SupplierId,
        SupplierNameRaw = src.Supplier.Name,
        SupplierEmail = src.SupplierEmail,
        PartNumber = src.RFQItem.PartNumber.Name,
        Alt = src.Alt,
        Price = src.Price,
        Qty = src.Qty,
        Condition = src.Condition ?? src.RFQItem.Condition,
        CertName = src.CertName,
        TagDate = src.TagDate,
        LeadTime = src.LeadTime,
        // A sell lead time that only follows the buy lead time is derived again per target customer.
        SellLeadTime = string.IsNullOrWhiteSpace(src.SellLeadTime) || LeadTimeRules.IsAutoSellLeadTime(src.SellLeadTime, src.LeadTime)
            ? null : src.SellLeadTime,
        Remark = src.Note,
        MyNotes = src.MyNotes,
        ShippingCost = src.ShippingCost,
        ShippingPoint = src.ShippingPoint,
        CustomCertReferences = src.CustomCertReferences,
        SerialNumber = src.SerialNumber,
        TraceTo = src.TraceTo,
        TagType = src.TagType,
        PriceType = src.PriceType,
        TaggedBy = src.TaggedBy,
        MoQ = src.MoQ,
        Warranty = src.Warranty,
        CR = src.CR,
        Status = Pending,
        CreatedAt = now,
    };

    /// <summary>The RFQ quote's cert PDFs go along onto the line (editable there); Done copies them onto every quote it writes.</summary>
    private async Task CopyRfqQuoteCertificatesAsync(List<(PendingSupplierQuote Line, ProcumentRecord Source)> lines, DateTime now)
    {
        var any = false;
        foreach (var (line, src) in lines)
            foreach (var cert in src.Certificates)
            {
                var from = Path.Combine(SupplierQuoteService.CertificateRoot, src.Id.ToString(), cert.FileName);
                if (!File.Exists(from)) continue;
                var folder = Path.Combine(PendingSupplierQuoteService.CertificateRoot, line.Id.ToString());
                Directory.CreateDirectory(folder);
                var stored = $"{Guid.NewGuid():N}.pdf";
                File.Copy(from, Path.Combine(folder, stored));
                _db.Set<PendingSupplierQuoteCertificate>().Add(new PendingSupplierQuoteCertificate
                {
                    PendingSupplierQuoteId = line.Id,
                    FileName = stored,
                    OriginalFileName = cert.OriginalFileName,
                    FileSizeBytes = cert.FileSizeBytes,
                    UploadedAt = now,
                    UploadedByUserId = cert.UploadedByUserId,
                });
                any = true;
            }
        if (any) await _db.SaveChangesAsync();
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
        // Lines copied from the user's own RFQ quote: which RFQ (and when it was quoted there).
        var sourceIds = raw.Select(r => r.SourceSupplierQuoteId).OfType<long>().Distinct().ToList();
        var sources = await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(q => sourceIds.Contains(q.Id))
                .Select(q => new
                {
                    q.Id,
                    q.CreatedAt,
                    q.Price,
                    q.RFQItem.Qty,
                    q.RFQItem.RFQId,
                    RfqName = q.RFQItem.RFQ.Name,
                    Deadline = q.RFQItem.RFQ.LeadTime,
                    CustomerCode = q.RFQItem.RFQ.Customer.CustomerCode,
                })
                .ToDictionaryAsync(q => q.Id);
        return lines
            .Where(l => rawById.ContainsKey(l.Id))
            .Select(l =>
            {
                var r = rawById[l.Id];
                var pref = r.PreferredId != null ? preferreds.GetValueOrDefault(r.PreferredId.Value) : null;
                var src = r.SourceSupplierQuoteId is long sid ? sources.GetValueOrDefault(sid) : null;
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
                    FromFile = r.Source == Pp && IsFileSubject(r.EmailSubject),
                    IsEmail = r.Source == null,
                    FromRfq = src == null ? null : new PpRfqRef
                    {
                        RfqId = src.RFQId,
                        RfqName = src.RfqName,
                        Deadline = src.Deadline,
                        CustomerCode = src.CustomerCode,
                        Qty = src.Qty,
                        Price = src.Price,
                    },
                    FromRfqQuotedAt = src?.CreatedAt,
                };
            })
            .OrderBy(l => l.PreferredName ?? l.Line.SupplierName).ThenBy(l => l.Line.PartNumber)
            .ToList();
    }

    /// <summary>
    /// Lines made from a file row carry "Procurement Plus · {file name}" as their subject; lines added by hand
    /// carry "Procurement Plus · {track}".
    /// </summary>
    private static bool IsFileSubject(string? subject)
    {
        const string prefix = "Procurement Plus · ";
        if (subject == null || !subject.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var rest = subject[prefix.Length..];
        return rest is not (PreferredTracks.Ils or PreferredTracks.Project or PreferredTracks.WeChat or RfqTrack);
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
            .Where(a => !AltPartNumbers.IsPlaceholder(a) && PnKey(a) != ownKey)
            .DistinctBy(PnKey)
            .ToList();
        return list.Count == 0 ? null : Trunc(string.Join(", ", list), 2000);
    }
    private static string CondKey(string? condition) => (condition ?? "").Trim().ToUpperInvariant();
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Trunc(string? value, int max) => value == null || value.Length <= max ? value : value[..max];
}
