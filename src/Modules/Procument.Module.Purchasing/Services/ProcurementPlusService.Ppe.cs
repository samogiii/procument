using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Catalog.Services;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// The new Procurement Plus page (api/ppe). A reviewer assigns parts to a user — the PPE (Procurement Part
/// Expert) — from Email RFQs; the user sees each part once, across every RFQ asking for it whose deadline is
/// at most <see cref="ProcurementPlusService.PpeDeadlineDays"/> days ago. Suppliers picked from ILS files are
/// draft lines (Source "PPE") until they have a price and a catalog supplier; then they are accepted onto
/// all those RFQs (same write as Email Quotes). Editing a quote row updates it on every one of those RFQs.
/// </summary>
public interface IPpeService
{
    // folderId = one of my folders; null = the public folder (parts in no folder).
    Task<PpePartPage> GetPartsAsync(long? userId, string? search, bool showOld, long? folderId, int page, int pageSize);
    Task<List<string>> GetPpePartNumbersAsync(long? userId, string? search, bool showOld, long? folderId);
    /// <summary>One part with its RFQs and supplier rows; first brings its RFQs in line (<c>SyncPpeRfqsAsync</c>).</summary>
    Task<PpePartDetail?> GetPartAsync(long? userId, long actorId, long partId);
    Task<List<PpeFolder>> GetFoldersAsync(long userId);
    Task<PpeFolder> SaveFolderAsync(long userId, long actorId, long? id, string? name);
    Task<bool> DeleteFolderAsync(long userId, long id);
    Task<int> SetPartFoldersAsync(long userId, PpeSetFoldersRequest request);
    /// <summary>Email Quotes accept: the suppliers' lines under Procurement Plus parts take the accepted email prices.</summary>
    Task<int> PriceFromAcceptedEmailsAsync(List<long> emailLineIds, long actorId);
    Task<PpeSaveQuoteResponse> SaveQuoteAsync(long? userId, long actorId, long partId, PpeSaveQuoteRequest request);
    Task<PpeAddOffersResponse> AddOffersAsync(long userId, PpeAddOffersRequest request);
    Task<bool> RemoveLineAsync(long? userId, long actorId, long lineId);
    Task<int> SetPartsHiddenAsync(long? userId, List<long> ids, bool hidden);
    Task<List<PpeCatalogPart>> SearchCatalogPartsAsync(long userId, string? q);
    Task<PpeAddPartResponse> AddPpePartAsync(long userId, long actorId, PpeAddPartRequest request);
    Task<PpeBulkAddPartsResponse> AddPpePartsAsync(long userId, long actorId, List<PpeAddPartRequest> items, List<long>? folderIds = null);
    Task<PpPartHistory?> GetPpeHistoryAsync(long? userId, long partId);
    Task<PpeOffersResponse> GetPartOffersAsync(long userId, long partId);
    Task<PpeIlsResponse> GetIlsAsync(long userId);
    Task<PpUploadResult> UploadPpeAsync(long userId, IEnumerable<IFormFile> files);
    Task<bool> DeleteUploadAsync(long userId, long uploadId);
    Task<List<PpUserSummary>> GetPpeUsersAsync();
    Task<PpeCommissionResponse> SaveCommissionAsync(long? userId, long actorId, bool isSuperAdmin, long partId, PpeCommissionRequest request);

    // Preferred suppliers: one list per user, shared with ProMax (Preferreds rows Kind Supplier).
    Task<List<PpPreferredSupplierResponse>> GetPreferredAsync(long ownerId);
    Task<PpPreferredSupplierResponse> SavePreferredAsync(long ownerId, long actorId, long? id, PpSavePreferredSupplierRequest request);
    Task<bool> UpdatePreferredAsync(long ownerId, long id, PpUpdatePreferredRequest request);
    Task<bool> RemovePreferredAsync(long ownerId, long id);
    Task<List<PpSupplierOption>> SearchSuppliersAsync(long ownerId, string? q);
}

public partial class ProcurementPlusService : IPpeService
{
    /// <summary>RFQs whose deadline is at most this many days ago belong to a part (Sam: "up to 2 weeks ago").</summary>
    public const int PpeDeadlineDays = 14;
    private const string PpeKind = PreferredKinds.PpePart;
    private const string Ppe = PendingSupplierQuote.SourcePpe;
    public const int MaxPpePageSize = 200;

    private static DateTime PpeEarliestDeadline => DateTime.UtcNow.Date.AddDays(-PpeDeadlineDays);

    private Task<List<PartKeys>> LoadPpePartsAsync(long? userId, bool includeHidden) =>
        LoadPartsAsync(userId, includeHidden, kind: PpeKind);

    private async Task<PartKeys?> LoadPpePartAsync(long? userId, long partId) =>
        (await LoadPpePartsAsync(userId, true)).FirstOrDefault(p => p.Part.Id == partId);

    /// <summary>The part's RFQ items whose RFQ deadline is at most 14 days ago, per part.</summary>
    private async Task<Dictionary<long, List<RFQItem>>> PpeItemsAsync(List<PartKeys> parts)
    {
        var items = await FindRfqItemsAsync(parts.SelectMany(p => p.Names), PpeEarliestDeadline);
        return parts.ToDictionary(p => p.Part.Id, p => items.Where(i => ItemKeys(i).Overlaps(p.Keys)).ToList());
    }

    // ──── Parts tab ────

    /// <summary>
    /// The Parts tab's list in its order (folder, search, Show older), before paging. Listed = Show older only
    /// (what the folder chips count).
    /// </summary>
    private async Task<(List<PartKeys> Filtered, List<PartKeys> Listed, List<PartKeys> All, Dictionary<long, List<RFQItem>> Items, Func<PartKeys, bool> IsOld)>
        FilteredPpePartsAsync(long? userId, string? search, bool showOld, long? folderId)
    {
        var parts = await LoadPpePartsAsync(userId, false);
        var items = await PpeItemsAsync(parts);
        var oldSince = DateTime.UtcNow.AddDays(-PpeDeadlineDays);
        bool IsOld(PartKeys p) => items[p.Part.Id].Count == 0 && p.Part.AddedAt < oldSince;

        var q = search?.Trim();
        var listed = parts.Where(p => showOld || !IsOld(p)).ToList();
        var filtered = listed
            // A folder shows only its parts; the public folder (none) shows the parts in no folder (Sam 2026-10-28).
            // Without a page user (SuperAdmin, all users) there are no folders to look in, so every part shows.
            .Where(p => folderId == null ? userId == null || FolderIdsOf(p.Part).Count == 0 : FolderIdsOf(p.Part).Contains(folderId.Value))
            .Where(p => string.IsNullOrEmpty(q)
                || p.Names.Any(n => n.Contains(q, StringComparison.OrdinalIgnoreCase))
                || (p.Part.Description ?? p.Part.CatalogPart?.Description ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
            // Nearest deadline first; parts without an open RFQ last, newest assigned first.
            .OrderBy(p => items[p.Part.Id].Count == 0 ? 1 : 0)
            .ThenBy(p => items[p.Part.Id].Select(i => (DateTime?)i.RFQ.LeadTime).Min())
            .ThenByDescending(p => p.Part.AddedAt)
            .ToList();
        return (filtered, listed, parts, items, IsOld);
    }

    /// <summary>"Copy all P/N": every part number of the Parts tab with its filters (no paging), in the list's order.</summary>
    public async Task<List<string>> GetPpePartNumbersAsync(long? userId, string? search, bool showOld, long? folderId) =>
        (await FilteredPpePartsAsync(userId, search, showOld, folderId)).Filtered.Select(p => p.Part.Name).ToList();

    public async Task<PpePartPage> GetPartsAsync(long? userId, string? search, bool showOld, long? folderId, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPpePageSize);
        var (filtered, listed, parts, items, IsOld) = await FilteredPpePartsAsync(userId, search, showOld, folderId);
        var lastPage = Math.Max(1, (filtered.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, lastPage);
        var pageParts = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PpePartPage
        {
            Items = await BuildPpeRowsAsync(pageParts, items, IsOld),
            Total = filtered.Count,
            Page = page,
            PageSize = pageSize,
            OldCount = parts.Count(IsOld),
            Folders = userId == null ? new() : await FoldersWithCountsAsync(userId.Value, listed),
            PublicCount = userId == null ? listed.Count : listed.Count(p => FolderIdsOf(p.Part).Count == 0),
        };
    }

    // ──── Folders (each user's own; the public folder = the parts in no folder) ────

    private const string FolderKind = PreferredKinds.PpeFolder;
    public const int MaxFolderNameLength = 100;

    /// <summary>The folder ids a part is in.</summary>
    private static List<long> FolderIdsOf(Preferred part) =>
        SplitList(part.FolderIds).Select(s => long.TryParse(s, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();

    private static string? JoinFolderIds(IEnumerable<long> ids)
    {
        var list = ids.Where(id => id > 0).Distinct().Order().ToList();
        return list.Count == 0 ? null : string.Join(",", list);
    }

    private async Task<List<PpeFolder>> FoldersWithCountsAsync(long userId, List<PartKeys> listed)
    {
        var counts = listed.SelectMany(p => FolderIdsOf(p.Part)).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        return (await MyParts(userId, FolderKind).AsNoTracking().OrderBy(f => f.Name).ToListAsync())
            .Select(f => new PpeFolder { Id = f.Id, Name = f.Name, PartCount = counts.GetValueOrDefault(f.Id) })
            .ToList();
    }

    public async Task<List<PpeFolder>> GetFoldersAsync(long userId) =>
        await FoldersWithCountsAsync(userId, await LoadPpePartsAsync(userId, false));

    /// <summary>New folder (id null) or a rename; names are unique per user (case-insensitive).</summary>
    public async Task<PpeFolder> SaveFolderAsync(long userId, long actorId, long? id, string? name)
    {
        var clean = NullIfBlank(name) ?? throw new InvalidOperationException("Type the folder name.");
        if (clean.Length > MaxFolderNameLength) throw new InvalidOperationException($"At most {MaxFolderNameLength} characters.");
        var key = clean.ToUpperInvariant();
        if (await MyParts(userId, FolderKind).AnyAsync(f => f.NameKey == key && f.Id != id))
            throw new InvalidOperationException($"You have a folder \"{clean}\" already.");
        Preferred folder;
        if (id != null)
            folder = await MyParts(userId, FolderKind).FirstOrDefaultAsync(f => f.Id == id) ?? throw new InvalidOperationException("Folder not found.");
        else
        {
            folder = new Preferred { Kind = FolderKind, UserId = userId, AddedAt = DateTime.UtcNow, AddedByUserId = actorId };
            _db.Set<Preferred>().Add(folder);
        }
        folder.Name = clean;
        folder.NameKey = key;
        await _db.SaveChangesAsync();
        return new PpeFolder { Id = folder.Id, Name = folder.Name };
    }

    /// <summary>Deletes the folder only — its parts stay in their other folders, or go back to the public folder.</summary>
    public async Task<bool> DeleteFolderAsync(long userId, long id)
    {
        var folder = await MyParts(userId, FolderKind).FirstOrDefaultAsync(f => f.Id == id);
        if (folder == null) return false;
        var token = id.ToString();
        foreach (var part in await MyParts(userId, PpeKind).Where(p => p.FolderIds != null && p.FolderIds.Contains(token)).ToListAsync())
            part.FolderIds = JoinFolderIds(FolderIdsOf(part).Where(f => f != id));
        _db.Set<Preferred>().Remove(folder);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Puts parts into (Add) and takes them out of (Remove) my folders; a part can be in several.</summary>
    public async Task<int> SetPartFoldersAsync(long userId, PpeSetFoldersRequest request)
    {
        var mine = await MyFolderIdsAsync(userId, request.Add.Concat(request.Remove));
        var parts = await MyParts(userId, PpeKind).Where(p => request.Ids.Contains(p.Id)).ToListAsync();
        foreach (var part in parts)
            part.FolderIds = JoinFolderIds(FolderIdsOf(part)
                .Concat(request.Add.Where(mine.Contains))
                .Where(f => !request.Remove.Contains(f)));
        await _db.SaveChangesAsync();
        return parts.Count;
    }

    /// <summary>The ids that are my folders (anything else is refused).</summary>
    private async Task<HashSet<long>> MyFolderIdsAsync(long userId, IEnumerable<long> ids)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return new();
        var mine = (await MyParts(userId, FolderKind).Where(f => wanted.Contains(f.Id)).Select(f => f.Id).ToListAsync()).ToHashSet();
        if (mine.Count != wanted.Count) throw new InvalidOperationException("Folder not found — reload the page.");
        return mine;
    }

    private async Task<List<PpePartRow>> BuildPpeRowsAsync(List<PartKeys> parts, Dictionary<long, List<RFQItem>> items, Func<PartKeys, bool> isOld)
    {
        if (parts.Count == 0) return new();
        var partIds = parts.Select(p => p.Part.Id).ToList();
        var stats = await PriceStatsAsync(parts);
        var drafts = (await _db.Set<PendingSupplierQuote>().AsNoTracking()
                .Where(l => l.Source == Ppe && l.Status == Pending && l.PreferredPartId != null && partIds.Contains(l.PreferredPartId.Value))
                .Select(l => new { PartId = l.PreferredPartId!.Value, l.SupplierId, l.SupplierNameRaw, l.Price })
                .ToListAsync())
            .ToLookup(d => d.PartId);
        var itemIds = parts.SelectMany(p => items[p.Part.Id]).Select(i => i.Id).Distinct().ToList();
        var quotes = itemIds.Count == 0 ? new() : await _db.Set<ProcumentRecord>().AsNoTracking()
            .Where(r => itemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop")
            .Select(r => new { r.RFQItemId, r.SupplierId, Priced = r.Price > 0 })
            .ToListAsync();
        var quotesPerItem = quotes.ToLookup(q => q.RFQItemId);

        return parts.Select(p =>
        {
            var mine = items[p.Part.Id];
            var newest = mine.OrderByDescending(i => i.RFQ.CreatedAt).FirstOrDefault();
            var condition = NullIfBlank(p.Part.Condition) ?? newest?.Condition;
            var partStats = stats.GetValueOrDefault(p.Part.Id) ?? new();
            var asked = partStats.FirstOrDefault(s => condition != null && s.Condition == CondKey(condition));
            decimal? avg = asked?.Average;
            if (avg == null && partStats.Count > 0)
                avg = Math.Round(partStats.Sum(s => s.Average * s.Count) / partStats.Sum(s => s.Count), 2);
            var partDrafts = drafts[p.Part.Id].ToList();
            // Suppliers with a price: on the part's RFQs, or priced here and not written yet (e.g. not in the catalog).
            var priced = mine.SelectMany(i => quotesPerItem[i.Id]).Where(q => q.Priced).Select(q => "S" + q.SupplierId)
                .Concat(partDrafts.Where(d => d.Price > 0)
                    .Select(d => d.SupplierId != null ? "S" + d.SupplierId : "N" + SupplierNameNormalizer.Key(d.SupplierNameRaw ?? "")))
                .Distinct()
                .Count();
            return new PpePartRow
            {
                Id = p.Part.Id,
                PartNumber = p.Part.Name,
                Description = p.Part.Description ?? p.Part.CatalogPart?.Description,
                Alternatives = p.Alternatives,
                Qty = p.Part.Qty ?? newest?.Qty,
                Condition = condition,
                Conditions = mine.Select(i => CondKey(i.Condition)).Append(CondKey(condition))
                    .Where(c => c.Length > 0).Distinct().ToList(),
                AvgPrice = avg,
                AvgPriceCondition = asked?.Condition,
                PriceStats = partStats,
                RfqCount = mine.Select(i => i.RFQId).Distinct().Count(),
                NearestDeadline = mine.Select(i => (DateTime?)i.RFQ.LeadTime).Min(),
                DraftCount = partDrafts.Count(d => d.Price is null or <= 0),
                PricedCount = priced,
                QuoteCount = mine.Sum(i => quotesPerItem[i.Id].Count()),
                AssignedAt = p.Part.AddedAt,
                Old = isOld(p),
                FolderIds = FolderIdsOf(p.Part),
            };
        }).ToList();
    }

    public async Task<int> SetPartsHiddenAsync(long? userId, List<long> ids, bool hidden)
    {
        var parts = await MyParts(userId, PpeKind).Where(p => ids.Contains(p.Id)).ToListAsync();
        foreach (var p in parts) p.HiddenAt = hidden ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        return parts.Count;
    }

    // ──── Add part by hand ────

    /// <summary>Add part dialog: parts in the database whose P/N, new name or an alternative contains q.</summary>
    public async Task<List<PpeCatalogPart>> SearchCatalogPartsAsync(long userId, string? q)
    {
        var s = q?.Trim();
        if (string.IsNullOrEmpty(s)) return new();
        var list = await _db.Set<PartNumber>().AsNoTracking()
            .Where(p => p.Name.Contains(s) || (p.NewName != null && p.NewName.Contains(s)) || p.Alternatives.Any(a => a.Name.Contains(s)))
            .OrderBy(p => p.Name.StartsWith(s) ? 0 : 1).ThenBy(p => p.Name)
            .Take(20)
            .Select(p => new PpeCatalogPart
            {
                Id = p.Id,
                PartNumber = p.Name,
                Description = p.Description,
                Alternatives = p.Alternatives.Select(a => a.Name).ToList(),
            })
            .ToListAsync();
        var listed = (await MyParts(userId, PpeKind).Where(p => p.HiddenAt == null).Select(p => p.NameKey).ToListAsync()).ToHashSet();
        foreach (var p in list) p.AlreadyAdded = listed.Contains(PnKey(p.PartNumber));
        return list;
    }

    /// <summary>
    /// A part the user adds by hand (Sam: "add a new part or select from the database and work on it"): the user's own
    /// PPE part, matched to RFQs by its keys like an assigned one. Same P/N on the list already = that part (shown again
    /// if removed, qty / condition / alternatives updated).
    /// </summary>
    public async Task<PpeAddPartResponse> AddPpePartAsync(long userId, long actorId, PpeAddPartRequest request)
    {
        string name;
        long? catalogId;
        var description = NullIfBlank(request.Description);
        if (request.PartNumberId != null)
        {
            var catalog = await _db.Set<PartNumber>().AsNoTracking()
                .Where(p => p.Id == request.PartNumberId)
                .Select(p => new { p.Id, p.Name, p.Description })
                .FirstOrDefaultAsync() ?? throw new InvalidOperationException("Part not found in the database.");
            name = catalog.Name.Trim();
            catalogId = catalog.Id;
            description ??= NullIfBlank(catalog.Description);
        }
        else
        {
            name = request.PartNumber?.Trim() ?? "";
            if (name.Length == 0) throw new InvalidOperationException("Type a part number.");
            // A typed P/N that is in the database gets linked (its alternatives then match too).
            var catalog = await _db.Set<PartNumber>().AsNoTracking()
                .Where(p => p.Name == name || p.NewName == name)
                .Select(p => new { p.Id, p.Description })
                .FirstOrDefaultAsync();
            catalogId = catalog?.Id;
            description ??= NullIfBlank(catalog?.Description);
        }
        var key = PnKey(name);
        if (key.Length == 0) throw new InvalidOperationException("Type a part number.");
        var folders = await MyFolderIdsAsync(userId, request.FolderIds ?? []);

        var existing = await MyParts(userId, PpeKind).FirstOrDefaultAsync(p => p.NameKey == key);
        var response = new PpeAddPartResponse
        {
            AlreadyListed = existing is { HiddenAt: null },
            Restored = existing is { HiddenAt: not null },
        };
        var part = UpsertPpePart(existing, userId, actorId, name, catalogId, description, request, folders);
        await _db.SaveChangesAsync();
        if (!response.AlreadyListed)
        {
            AddEvent(part.Id, actorId, "Added", response.Restored ? "Added again by hand" : "Added by hand");
            await _db.SaveChangesAsync();
        }
        response.Id = part.Id;
        response.PartNumber = part.Name;
        return response;
    }

    /// <summary>Most rows one bulk import takes.</summary>
    public const int MaxBulkPpeParts = 500;

    /// <summary>
    /// Bulk import (pasted Part Number / Qty / Condition rows): each typed P/N like Add part — linked to the database
    /// when it is there, my existing part with that P/N updated (and shown again) instead of a second one.
    /// </summary>
    public async Task<PpeBulkAddPartsResponse> AddPpePartsAsync(long userId, long actorId, List<PpeAddPartRequest> items, List<long>? folderIds = null)
    {
        var folders = await MyFolderIdsAsync(userId, folderIds ?? []);
        var wanted = items
            .Where(i => !string.IsNullOrWhiteSpace(i.PartNumber))
            .GroupBy(i => PnKey(i.PartNumber))
            .Where(g => g.Key.Length > 0)
            .Select(g => g.First())
            .ToList();
        if (wanted.Count == 0) throw new InvalidOperationException("Paste at least one part number.");
        if (wanted.Count > MaxBulkPpeParts) throw new InvalidOperationException($"At most {MaxBulkPpeParts} parts at once.");

        var names = wanted.Select(i => i.PartNumber!.Trim()).ToList();
        var catalog = await _db.Set<PartNumber>().AsNoTracking()
            .Where(p => names.Contains(p.Name) || (p.NewName != null && names.Contains(p.NewName)))
            .Select(p => new { p.Id, p.Name, p.NewName, p.Description })
            .ToListAsync();
        var keys = wanted.Select(i => PnKey(i.PartNumber)).ToList();
        var mine = (await MyParts(userId, PpeKind).Where(p => keys.Contains(p.NameKey)).ToListAsync())
            .GroupBy(p => p.NameKey)
            .ToDictionary(g => g.Key, g => g.First());

        var response = new PpeBulkAddPartsResponse();
        var events = new List<(Preferred Part, string Text)>();
        foreach (var item in wanted)
        {
            var name = item.PartNumber!.Trim();
            var key = PnKey(name);
            var match = catalog.FirstOrDefault(c => PnKey(c.Name) == key) ?? catalog.FirstOrDefault(c => PnKey(c.NewName) == key);
            var existing = mine.GetValueOrDefault(key);
            if (existing is { HiddenAt: null }) response.AlreadyListed++;
            else if (existing != null) response.Restored++;
            else response.Added++;
            var shown = existing is { HiddenAt: null };
            var part = UpsertPpePart(existing, userId, actorId, name, match?.Id, NullIfBlank(item.Description) ?? NullIfBlank(match?.Description), item, folders);
            if (!shown) events.Add((part, existing == null ? "Added by bulk import" : "Added again by bulk import"));
        }
        await _db.SaveChangesAsync();
        foreach (var (part, text) in events) AddEvent(part.Id, actorId, "Added", text);
        await _db.SaveChangesAsync();
        return response;
    }

    /// <summary>
    /// A hand-added part: a new PPE part of mine, or my existing one with that P/N — shown again, not "older" for another
    /// 14 days, qty / condition / alternatives from the request, added to the picked folders (none = public only). Not saved here.
    /// </summary>
    private Preferred UpsertPpePart(Preferred? part, long userId, long actorId, string name, long? catalogId, string? description,
        PpeAddPartRequest request, IReadOnlyCollection<long> folderIds)
    {
        var key = PnKey(name);
        var condition = Trunc(NullIfBlank(request.Condition)?.ToUpperInvariant(), 50);
        var qty = request.Qty > 0 ? request.Qty : null;
        var now = DateTime.UtcNow;
        if (part != null)
        {
            part.HiddenAt = null;
            part.AddedAt = now;
            part.FolderIds = JoinFolderIds(FolderIdsOf(part).Concat(folderIds));
            part.PartNumberId ??= catalogId;
            part.Description ??= Trunc(description, 500);
            part.Alternatives = JoinAlternatives(SplitList(part.Alternatives).Concat(request.Alternatives ?? []), part.NameKey);
            if (qty != null) part.Qty = qty;
            if (condition != null) part.Condition = condition;
            return part;
        }
        part = new Preferred
        {
            Kind = PpeKind,
            UserId = userId,
            PartNumberId = catalogId,
            Name = Trunc(name, 300)!,
            NameKey = Trunc(key, 300)!,
            Description = Trunc(description, 500),
            Alternatives = JoinAlternatives(request.Alternatives ?? [], key),
            Qty = qty,
            Condition = condition,
            AddedAt = now,
            AddedByUserId = actorId,
            FolderIds = JoinFolderIds(folderIds),
        };
        _db.Set<Preferred>().Add(part);
        return part;
    }

    // ──── One part: its RFQs and supplier rows ────

    public async Task<PpePartDetail?> GetPartAsync(long? userId, long actorId, long partId)
    {
        var part = await LoadPpePartAsync(userId, partId);
        if (part == null) return null;
        var items = await PpeItemsAsync([part]);
        await SyncPpeRfqsAsync(part, items[part.Part.Id], actorId);
        var oldSince = DateTime.UtcNow.AddDays(-PpeDeadlineDays);
        var row = (await BuildPpeRowsAsync([part], items, p => items[p.Part.Id].Count == 0 && p.Part.AddedAt < oldSince)).First();
        var mine = items[part.Part.Id];
        var refs = mine.OrderBy(i => i.RFQ.LeadTime).Select(ItemRef).ToList();
        return new PpePartDetail { Part = row, Rfqs = refs, Rows = await BuildPpeQuoteRowsAsync(part, mine) };
    }

    private static PpeRfqRef ItemRef(RFQItem i) => new()
    {
        RfqId = i.RFQId,
        RfqName = i.RFQ.Name,
        RfqItemId = i.Id,
        Deadline = i.RFQ.LeadTime,
        CustomerId = i.RFQ.CustomerId,
        CustomerCode = i.RFQ.Customer?.CustomerCode,
        CustomerName = i.RFQ.Customer?.Name,
        CustomerBase = i.RFQ.Customer?.Base,
        MinCoef = i.RFQ.Customer?.MinCoef,
        PartNumber = i.PartNumber.Name,
        Qty = i.Qty,
        Condition = i.Condition,
    };

    /// <summary>
    /// Drafts (lines here without a price / catalog supplier), then the supplier quotes on the part's RFQs —
    /// one row per quote across RFQs (the line that wrote them, else supplier + condition + quoted P/N + serial) —
    /// and accepted Email Quotes lines for the part (editable: the edit goes to the email line). Alt is shown against THIS part: a Y price is
    /// Alt = Y here whether the RFQ asked for this part (Alt Y) or for Y itself (Alt empty on that RFQ).
    /// </summary>
    private async Task<List<PpeQuoteRow>> BuildPpeQuoteRowsAsync(PartKeys part, List<RFQItem> items)
    {
        var rows = new List<PpeQuoteRow>();
        var itemById = items.ToDictionary(i => i.Id);
        var CardAlt = CardAltFor(part);
        string? QuoteAlt(ProcumentRecord q) => CardAlt(QuotedPartNumber(q, itemById[q.RFQItemId]));
        var itemIds = items.Select(i => i.Id).ToList();
        var quotes = itemIds.Count == 0 ? new() : await _db.Set<ProcumentRecord>().AsNoTracking()
            .Include(r => r.Supplier)
            .Where(r => itemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") != "Shop")
            .ToListAsync();

        var ownerIds = quotes.Select(q => q.SourcePendingQuoteId).OfType<long>().Distinct().ToList();
        var owners = ownerIds.Count == 0 ? new() : await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(l => ownerIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Source, l.SourceMethod, l.EmailSubject })
            .ToDictionaryAsync(l => l.Id);

        var drafts = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(l => l.Source == Ppe && l.Status == Pending && l.PreferredPartId == part.Part.Id)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
        var supplierIds = drafts.Select(d => d.SupplierId).OfType<long>().Distinct().ToList();
        var suppliers = await _db.Set<Supplier>().AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        foreach (var d in drafts)
        {
            var supplier = d.SupplierId != null ? suppliers.GetValueOrDefault(d.SupplierId.Value) : null;
            rows.Add(new PpeQuoteRow
            {
                Key = "L" + d.Id,
                Kind = PpeRowKinds.Draft,
                LineId = d.Id,
                Reason = supplier == null ? "Pick a supplier from the catalog"
                    : supplier.Status != "Approved" ? $"Supplier is {supplier.Status}"
                    : d.Price is null or <= 0 ? "Fill the price"
                    : items.Count == 0 ? $"No RFQ with a deadline in the last {PpeDeadlineDays} days" : "Not written yet",
                FilePrice = d.Price == null ? d.PriceRaw : null,
                FileName = d.EmailSubject?.StartsWith(PpeFileSubject) == true ? d.EmailSubject[PpeFileSubject.Length..] : null,
                Methods = SplitList(d.SourceMethod),
                UpdatedAt = d.CreatedAt,
                SupplierId = supplier?.Id,
                SupplierName = supplier?.Name ?? d.SupplierNameRaw ?? "",
                SupplierStatus = supplier?.Status,
                SupplierEmail = d.SupplierEmail,
                Price = d.Price,
                Qty = d.Qty,
                Condition = d.Condition,
                Alt = CardAlt(d.Alt ?? d.PartNumber),
                LeadTime = d.LeadTime,
                SellLeadTime = d.SellLeadTime,
                CertName = d.CertName,
                TagDate = d.TagDate,
                ShippingCost = d.ShippingCost,
                ShippingPoint = d.ShippingPoint,
                Note = d.Remark,
                MyNotes = d.MyNotes,
                CustomCertReferences = SupplierQuoteService.SplitCertReferences(d.CustomCertReferences).ToList(),
                SerialNumber = d.SerialNumber,
                TraceTo = d.TraceTo,
                TagType = d.TagType,
                PriceType = d.PriceType,
                TaggedBy = d.TaggedBy,
                MoQ = d.MoQ,
                Warranty = d.Warranty,
                CR = d.CR,
            });
        }

        // One row per supplier offer (supplier + condition + alt + serial + price) across every RFQ, whoever wrote each
        // quote: the same supplier on two customers' RFQs is one row (Sam) — Add Commission splits it per customer. A
        // second price from the supplier is a row of its own (Sam 2026-10-26: never merged into / replacing the first).
        foreach (var group in quotes.GroupBy(q => PpeOfferKey(q, itemById[q.RFQItemId], CardAlt)))
        {
            var list = group.OrderByDescending(q => q.UpdatedAt ?? q.CreatedAt).ToList();
            var q = list[0];
            var groupOwners = list
                .Select(x => x.SourcePendingQuoteId is long id && owners.TryGetValue(id, out var o) ? o : null)
                .ToList();
            // Every quote came from Email Quotes = an Email row; editing it edits those email lines too (Sam 2026-10-26).
            var fromEmail = groupOwners.All(o => o != null && o.Source == null);
            var owner = groupOwners.FirstOrDefault(o => o != null && o.Source == Ppe);
            rows.Add(new PpeQuoteRow
            {
                Key = group.Key,
                Kind = fromEmail ? PpeRowKinds.Email : PpeRowKinds.Quote,
                LineId = owner?.Id,
                QuoteIds = list.Select(x => x.Id).ToList(),
                Rfqs = list.Select(x =>
                {
                    var r = ItemRef(itemById[x.RFQItemId]);
                    r.QuoteId = x.Id;
                    r.QuotePrice = x.Price;
                    r.QuoteQty = x.Qty;
                    r.QuoteShippingCost = x.ShippingCost;
                    r.Coef1 = x.Coef_1;
                    r.QuoteUnitPrice = x.UnitPrice;
                    r.CommissionAt = x.CommissionAt;
                    return r;
                }).OrderBy(r => r.Deadline).ToList(),
                Methods = owner != null ? SplitList(owner.SourceMethod) : new(),
                UpdatedAt = q.UpdatedAt ?? q.CreatedAt,
                SupplierId = q.SupplierId,
                SupplierName = q.Supplier?.Name ?? "",
                SupplierStatus = q.Supplier?.Status,
                SupplierTrusted = q.Supplier?.Trusted ?? false,
                SupplierEmail = q.SupplierEmail,
                Price = q.Price,
                Qty = q.Qty,
                Condition = q.Condition,
                Alt = QuoteAlt(q),
                Unit = q.Unit,
                LeadTime = q.LeadTime,
                SellLeadTime = q.SellLeadTime,
                CertName = q.CertName,
                TagDate = q.TagDate,
                ShippingCost = q.ShippingCost,
                ShippingPoint = q.ShippingPoint,
                Note = q.Note,
                MyNotes = q.MyNotes,
                CustomCertReferences = SupplierQuoteService.SplitCertReferences(q.CustomCertReferences).ToList(),
                SerialNumber = q.SerialNumber,
                TraceTo = q.TraceTo,
                TagType = q.TagType,
                PriceType = q.PriceType,
                TaggedBy = q.TaggedBy,
                MoQ = q.MoQ,
                Warranty = q.Warranty,
                CR = q.CR,
            });
        }

        // Accepted email quotes for the part that are on none of these RFQs (else they are a row above already).
        var since = DateTime.UtcNow.AddDays(-PpeDeadlineDays);
        var names = part.Names;
        var emailLines = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Include(l => l.Supplier)
            .Where(l => l.Source == null && l.Status == Accepted && l.ReviewedAt >= since
                && (names.Contains(l.PartNumber) || (l.Alt != null && names.Contains(l.Alt))))
            .OrderByDescending(l => l.ReviewedAt)
            .Take(200)
            .ToListAsync();
        // Every field, so an edit here (which saves the whole form) keeps what the line has.
        foreach (var e in emailLines.Where(e => !owners.ContainsKey(e.Id) && EmailQuoteMatch.PartKeys(e).Any(part.Keys.Contains)))
            rows.Add(new PpeQuoteRow
            {
                Key = "E" + e.Id,
                Kind = PpeRowKinds.Email,
                EmailLineId = e.Id,
                Reason = e.AppliedCount > 0 ? null : "Accepted — on no open RFQ",
                UpdatedAt = e.ReviewedAt ?? e.CreatedAt,
                SupplierId = e.SupplierId,
                SupplierName = e.Supplier?.Name ?? e.SupplierNameRaw ?? "",
                SupplierStatus = e.Supplier?.Status,
                SupplierTrusted = e.Supplier?.Trusted ?? false,
                SupplierEmail = e.SupplierEmail,
                Price = e.Price,
                Qty = e.Qty,
                Condition = e.Condition,
                Alt = CardAlt(e.Alt ?? e.PartNumber),
                LeadTime = e.LeadTime,
                SellLeadTime = e.SellLeadTime,
                CertName = e.CertName,
                TagDate = e.TagDate,
                ShippingCost = e.ShippingCost,
                ShippingPoint = e.ShippingPoint,
                Note = e.Remark,
                MyNotes = e.MyNotes,
                CustomCertReferences = SupplierQuoteService.SplitCertReferences(e.CustomCertReferences).ToList(),
                SerialNumber = e.SerialNumber,
                TraceTo = e.TraceTo,
                TagType = e.TagType,
                PriceType = e.PriceType,
                TaggedBy = e.TaggedBy,
                MoQ = e.MoQ,
                Warranty = e.Warranty,
                CR = e.CR,
            });

        return rows;
    }

    /// <summary>The P/N a price is for, against this part (null = the part's own P/N).</summary>
    private static Func<string?, string?> CardAltFor(PartKeys part)
    {
        var ownKeys = new[] { part.Part.Name, part.Part.NameKey, part.Part.CatalogPart?.Name, part.Part.CatalogPart?.NewName }
            .Select(PnKey).Where(k => k.Length > 0).ToHashSet();
        return quoted => string.IsNullOrWhiteSpace(quoted) || ownKeys.Contains(PnKey(quoted)) ? null : quoted.Trim();
    }

    /// <summary>An RFQ quote is for its Alt P/N, else for the RFQ item's own P/N.</summary>
    private static string QuotedPartNumber(ProcumentRecord q, RFQItem item) =>
        string.IsNullOrWhiteSpace(q.Alt) ? item.PartNumber.Name : q.Alt;

    /// <summary>One supplier row of a part across its RFQs: supplier + condition + quoted P/N + serial + price.</summary>
    private static string PpeOfferKey(ProcumentRecord q, RFQItem item, Func<string?, string?> cardAlt) =>
        $"S{q.SupplierId}|{CondKey(q.Condition ?? item.Condition)}|{PnKey(cardAlt(QuotedPartNumber(q, item)))}|{PnKey(q.SerialNumber)}|{q.Price.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}";

    private const string PpeFileSubject = "Procurement Plus · ";

    // ──── Save (SupplierQuoteEditDialog) ────

    /// <summary>
    /// Every save goes through a Procurement Plus line: the draft / the line that wrote the row, or a new line
    /// (Add Supplier, or a quote row typed on an RFQ page — its quotes become the line's so a changed condition
    /// or alt still updates them). With a price and an approved catalog supplier the line is accepted onto every
    /// RFQ for the part with a deadline at most 14 days ago, and re-applied after each edit. Accepted Email Quotes lines
    /// behind the row take the edit themselves (Sam 2026-10-26) and are re-applied the same way.
    /// </summary>
    public async Task<PpeSaveQuoteResponse> SaveQuoteAsync(long? userId, long actorId, long partId, PpeSaveQuoteRequest request)
    {
        var part = await LoadPpePartAsync(userId, partId) ?? throw new InvalidOperationException("Part not found.");
        PendingSupplierQuote? line = null;
        List<ProcumentRecord> claimed = new();
        var emailLines = new List<PendingSupplierQuote>();
        if (request.LineId is > 0)
            line = await _db.Set<PendingSupplierQuote>()
                .FirstOrDefaultAsync(l => l.Id == request.LineId && l.Source == Ppe && l.Status != Removed && l.PreferredPartId == part.Part.Id)
                ?? throw new InvalidOperationException("Supplier line not found.");
        if (request.EmailLineId is > 0)
        {
            var email = await _db.Set<PendingSupplierQuote>()
                .FirstOrDefaultAsync(l => l.Id == request.EmailLineId && l.Source == null && l.Status == Accepted)
                ?? throw new InvalidOperationException("Email Quotes line not found — reload the part.");
            if (!EmailQuoteMatch.PartKeys(email).Any(part.Keys.Contains))
                throw new InvalidOperationException("This Email Quote is not for this part.");
            emailLines.Add(email);
        }
        if (request.QuoteIds.Count > 0)
        {
            // A row stands for the same supplier quote on every RFQ: the edit takes all of them over, except the ones
            // an Email Quotes line wrote — that line takes the edit instead and writes it again.
            var quotes = await _db.Set<ProcumentRecord>().Where(r => request.QuoteIds.Contains(r.Id)).ToListAsync();
            var ownerIds = quotes.Select(q => q.SourcePendingQuoteId).OfType<long>().Distinct().ToList();
            var owners = await _db.Set<PendingSupplierQuote>().Where(l => ownerIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            bool FromEmail(ProcumentRecord q) => q.SourcePendingQuoteId is long id && owners.TryGetValue(id, out var o) && o.Source == null;
            emailLines.AddRange(owners.Values.Where(o => o.Source == null && o.Status == Accepted));
            var editable = quotes.Where(q => !FromEmail(q)).ToList();
            line ??= owners.Values.FirstOrDefault(o => o.Source == Ppe && o.PreferredPartId == part.Part.Id && o.Status != Removed);
            claimed = line == null ? editable : editable.Where(q => q.SourcePendingQuoteId != line.Id).ToList();
        }
        emailLines = emailLines.DistinctBy(l => l.Id).ToList();

        var supplier = await ResolvePpeSupplierAsync(request.SupplierName);
        var response = new PpeSaveQuoteResponse();
        if (emailLines.Count > 0)
        {
            // Already on the RFQs: they keep it with the edit, so it needs an approved catalog supplier and a price.
            if (supplier == null) throw new InvalidOperationException("Pick the supplier from the catalog — this Email Quote is on the RFQs.");
            if (supplier.Status != "Approved") throw new InvalidOperationException($"Supplier {supplier.Name} is {supplier.Status} — this Email Quote is on the RFQs.");
            if (!(request.Price > 0)) throw new InvalidOperationException("Fill the price — this Email Quote is on the RFQs.");
            foreach (var email in emailLines) ApplyEmailForm(email, request, supplier, part);
            await _db.SaveChangesAsync();
            // Every RFQ the email wrote on takes the edit, and the part's RFQs (deadline at most 14 days ago) that lack it get it.
            var emailIds = emailLines.Select(e => e.Id).ToList();
            var reapplied = await _quotes.ReapplyAsync(emailIds, actorId, PpeDeadlineDays);
            response.QuotesWritten += reapplied.QuotesAdded + reapplied.QuotesUpdated;
            response.Message = reapplied.Failed.FirstOrDefault()?.Reason;
            if (line == null && claimed.Count == 0)
            {
                await SyncPpeRfqsAsync(part, (await PpeItemsAsync([part]))[part.Part.Id], actorId);
                // Only Email Quotes behind the row — no Procurement Plus line.
                var quote = await _db.Set<ProcumentRecord>().AsNoTracking()
                    .Where(r => r.SourcePendingQuoteId != null && emailIds.Contains(r.SourcePendingQuoteId.Value))
                    .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .Select(r => new { r.Id, r.RFQItem.RFQId })
                    .FirstOrDefaultAsync();
                response.LineId = emailIds[0];
                response.Written = true;
                response.QuoteId = quote?.Id;
                response.RfqId = quote?.RFQId;
                AddEvent(part.Part.Id, actorId, "Supplier", $"{supplier.Name} email quote edited");
                await _db.SaveChangesAsync();
                return response;
            }
        }

        if (line == null)
        {
            line = new PendingSupplierQuote
            {
                Source = Ppe,
                SourceMethod = request.QuoteIds.Count > 0 ? "RFQ" : PreferredSupplierMethods.Search,
                CreatedByUserId = part.Part.UserId,
                PreferredPartId = part.Part.Id,
                PartNumber = Trunc(part.Part.Name, 200)!,
                EmailSubject = Trunc(PpeFileSubject.TrimEnd(' ', '·') + " · " + (request.QuoteIds.Count > 0 ? "RFQ quote" : "Added"), 500),
                Status = Pending,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Set<PendingSupplierQuote>().Add(line);
        }
        ApplyPpeForm(line, request, supplier);
        await _db.SaveChangesAsync();

        if (claimed.Count > 0)
        {
            var formerOwners = claimed.Select(q => q.SourcePendingQuoteId).OfType<long>().Where(id => id != line.Id).Distinct().ToList();
            foreach (var q in claimed) q.SourcePendingQuoteId = line.Id;
            await _db.SaveChangesAsync();
            await StopEmptyLinesAsync(formerOwners);
        }

        response.LineId = line.Id;
        // Quotes the line owns already (written before, or just taken over) are rewritten by Re-apply,
        // which also adds the line to the part's open RFQs that do not have it.
        var rewriteOwned = claimed.Count > 0 || line.Status == Accepted;
        if (line.Status == Pending)
        {
            var reason = supplier == null ? "Saved without a price on the RFQs — pick a supplier from the catalog."
                : supplier.Status != "Approved" ? $"Saved — supplier {supplier.Name} is {supplier.Status}, so it is not on the RFQs."
                : line.Price is null or <= 0 ? null // a draft: the price comes later
                : "";
            if (reason == "")
            {
                var accepted = await _quotes.AcceptAsync([line.Id], actorId, allowWithoutRfq: true, graceDays: PpeDeadlineDays);
                response.QuotesWritten += accepted.QuotesWritten;
                response.Message = accepted.Failed.FirstOrDefault()?.Reason;
            }
            else response.Message = reason;
        }
        if (rewriteOwned)
        {
            var reapplied = await _quotes.ReapplyAsync([line.Id], actorId, PpeDeadlineDays);
            response.QuotesWritten += reapplied.QuotesAdded + reapplied.QuotesUpdated;
            response.Message ??= reapplied.Failed.FirstOrDefault()?.Reason;
        }

        // The edit may have met the same quote written by another line (an Email Quote): keep one of each.
        var partItemIds = (await PpeItemsAsync([part]))[part.Part.Id];
        await SyncPpeRfqsAsync(part, partItemIds, actorId);

        var status = await _db.Set<PendingSupplierQuote>().AsNoTracking().Where(l => l.Id == line.Id).Select(l => l.Status).FirstAsync();
        response.Written = status == Accepted;
        var owned = await _db.Set<ProcumentRecord>().AsNoTracking()
            .Where(r => r.SourcePendingQuoteId == line.Id)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Select(r => new { r.Id, r.RFQItem.RFQId })
            .FirstOrDefaultAsync();
        if (owned == null && response.Written && line.Price is decimal linePrice)
        {
            // Its quote was the duplicate of another line's (kept instead): that one is the row's quote now.
            var ids = partItemIds.Select(i => i.Id).ToList();
            owned = await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(r => ids.Contains(r.RFQItemId) && r.SupplierId == line.SupplierId && r.Price == linePrice)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new { r.Id, r.RFQItem.RFQId })
                .FirstOrDefaultAsync();
        }
        response.QuoteId = owned?.Id;
        response.RfqId = owned?.RFQId;
        AddEvent(part.Part.Id, actorId, "Supplier", $"{line.SupplierNameRaw} {(response.Written ? "priced" : "saved")}");
        await _db.SaveChangesAsync();
        return response;
    }

    /// <summary>The dialog's supplier (picked from the catalog by name): an active, not disabled supplier, approved first.</summary>
    private async Task<Supplier?> ResolvePpeSupplierAsync(string? name)
    {
        var clean = name?.Trim();
        if (string.IsNullOrEmpty(clean)) return null;
        return await _db.Set<Supplier>().AsNoTracking()
            .Where(s => s.Name == clean && s.IsActive && s.Dependency != "Disabled")
            .OrderBy(s => s.Status == "Approved" ? 0 : 1)
            .FirstOrDefaultAsync();
    }

    /// <summary>The dialog's fields onto the line ('' / 0 = cleared; the RFQ quote payload's conventions).</summary>
    private static void ApplyPpeForm(PendingSupplierQuote line, SaveSupplierQuoteRequest f, Supplier? supplier)
    {
        line.SupplierId = supplier?.Id;
        line.SupplierNameRaw = Trunc(supplier?.Name ?? NullIfBlank(f.SupplierName) ?? line.SupplierNameRaw, 300);
        if (f.SupplierEmail != null) line.SupplierEmail = Trunc(SupplierEmails.Normalize(f.SupplierEmail), 320);
        line.Price = f.Price > 0 ? f.Price : null;
        if (line.Price != null) line.PriceRaw = null;
        line.Qty = f.Qty > 0 ? f.Qty : null;
        line.Condition = Trunc(NullIfBlank(f.Condition)?.ToUpperInvariant(), 100);
        line.Alt = Trunc(AltPartNumbers.Clean(f.Alt), 200);
        line.LeadTime = Trunc(NullIfBlank(f.LeadTime), 100);
        line.SellLeadTime = Trunc(NullIfBlank(f.SellLeadTime), 100);
        line.ShippingCost = f.ShippingCost;
        line.ShippingPoint = Trunc(NullIfBlank(f.ShippingPoint), 200);
        line.CertName = Trunc(NullIfBlank(f.CertName), 200);
        line.TagDate = f.TagDate;
        line.Remark = Trunc(NullIfBlank(f.Note), 2000);
        line.MyNotes = Trunc(NullIfBlank(f.MyNotes), 2000);
        line.CustomCertReferences = SupplierQuoteService.JoinCertReferences(f.CustomCertReferences ?? []);
        line.SerialNumber = Trunc(NullIfBlank(f.SerialNumber), QuoteDetailFields.SerialNumberLength);
        line.TraceTo = Trunc(NullIfBlank(f.TraceTo), QuoteDetailFields.TraceToLength);
        line.TagType = Trunc(NullIfBlank(f.TagType), QuoteDetailFields.TagTypeLength);
        line.PriceType = Trunc(NullIfBlank(f.PriceType), QuoteDetailFields.PriceTypeLength);
        line.TaggedBy = Trunc(NullIfBlank(f.TaggedBy), QuoteDetailFields.TaggedByLength);
        line.MoQ = Trunc(NullIfBlank(f.MoQ), QuoteDetailFields.MoQLength);
        line.Warranty = Trunc(NullIfBlank(f.Warranty), QuoteDetailFields.WarrantyLength);
        line.CR = Trunc(NullIfBlank(f.CR), QuoteDetailFields.CRLength);
    }

    /// <summary>
    /// The dialog's fields onto an accepted Email Quotes line (Email Quotes shows the edit too). The line keeps its own
    /// P/N; the dialog's Alt is against this part ("Same P/N" = the part itself).
    /// </summary>
    private static void ApplyEmailForm(PendingSupplierQuote email, SaveSupplierQuoteRequest f, Supplier supplier, PartKeys part)
    {
        var tagDate = email.TagDate;
        var priceRaw = email.PriceRaw; // the price as read from the mail — re-reading the mail compares against it
        ApplyPpeForm(email, f, supplier);
        email.PriceRaw = priceRaw;
        var quoted = AltPartNumbers.Clean(f.Alt) ?? part.Part.Name;
        email.Alt = PnKey(quoted) == PnKey(email.PartNumber) ? null : Trunc(quoted, 200);
        if (email.TagDate != tagDate) email.TagDateRaw = email.TagDate?.ToString("yyyy-MM-dd");
    }

    public async Task<bool> RemoveLineAsync(long? userId, long actorId, long lineId)
    {
        var line = await _db.Set<PendingSupplierQuote>()
            .FirstOrDefaultAsync(l => l.Id == lineId && l.Source == Ppe && l.Status == Pending);
        if (line == null || line.PreferredPartId == null || !await MyParts(userId, PpeKind).AnyAsync(p => p.Id == line.PreferredPartId)) return false;
        line.Status = Removed;
        line.ReviewedAt = DateTime.UtcNow;
        line.ReviewedByUserId = actorId;
        await _db.SaveChangesAsync();
        return true;
    }

    // ──── Add Commission ────

    /// <summary>Selling total (unit price × qty) from which a Trusted supplier may give the customer exactly its MinCoef.</summary>
    public const decimal CommissionTotalThreshold = 10_000m;
    /// <summary>Below this selling total no commission can be saved at all (Sam).</summary>
    public const decimal CommissionMinTotal = 5_000m;
    /// <summary>Lowest commission coef without a Trusted supplier + customer MinCoef.</summary>
    public const double DefaultMinCommissionCoef = 1.13;

    /// <summary>
    /// Why this coef is not allowed for the quote (null = allowed). For now every coef is allowed (any coef &gt; 0 —
    /// SaveCommissionAsync checks that).
    /// </summary>
    /// <remarks>
    /// Switched off for now (Sam 2026-10-28) — kept commented to roll back: the 5K$ / 10K$ Total Price conditions, the
    /// Trusted supplier + customer MinCoef rule and the 1.13 minimum. They checked each RFQ quote on its own Total
    /// Price ((price + shipping / qty) × coef × qty): under 5K$ → no commission; Trusted + MinCoef: ≥ 10K$ → coef ≥
    /// MinCoef, below → coef &gt; MinCoef; otherwise coef ≥ 1.13.
    /// </remarks>
    public static string? CommissionRuleError(ProcumentRecord quote, bool supplierTrusted, decimal? customerMinCoef, double coef)
    {
        // var qty = quote.Qty > 0 ? quote.Qty : 1;
        // var unit = ((double)quote.Price + (quote.ShippingCost ?? 0) / qty) * coef;
        // var total = (decimal)(unit * qty);
        // const double eps = 1e-9;
        // if (total < CommissionMinTotal)
        //     return $"Total Price (Unit Price × Qty) {total:#,0.##}$ is under {CommissionMinTotal:#,0}$ — no commission";
        // if (supplierTrusted && customerMinCoef is decimal min)
        // {
        //     var m = (double)min;
        //     if (total >= CommissionTotalThreshold)
        //         return coef + eps < m ? $"coef must be at least the customer's minimum {m:0.####}" : null;
        //     return coef <= m + eps ? $"selling total is under {CommissionTotalThreshold:#,0}$, so the coef must be bigger than the customer's minimum {m:0.####}" : null;
        // }
        // return coef + eps < DefaultMinCommissionCoef
        //     ? $"coef must be at least {DefaultMinCommissionCoef}{(supplierTrusted ? " (the customer has no MinCoef)" : " (supplier is not Trusted)")}"
        //     : null;
        // return coef + eps < DefaultMinCommissionCoef ? $"coef must be at least {DefaultMinCommissionCoef}" : null;
        return null;
    }

    /// <summary>
    /// Saves one coef per supplier per customer as Coef 1 on those supplier quotes (the part's RFQs with a deadline at
    /// most 14 days ago), locks it for the Add Quote page, and takes the part off the list.
    /// </summary>
    public async Task<PpeCommissionResponse> SaveCommissionAsync(long? userId, long actorId, bool isSuperAdmin, long partId, PpeCommissionRequest request)
    {
        var part = await LoadPpePartAsync(userId, partId) ?? throw new InvalidOperationException("Part not found.");
        var items = (await PpeItemsAsync([part]))[part.Part.Id];
        var itemIds = items.Select(i => i.Id).ToHashSet();
        var quoteIds = request.Items.SelectMany(i => i.QuoteIds).Distinct().ToList();
        if (quoteIds.Count == 0) throw new InvalidOperationException("Pick the suppliers first.");
        var quotes = await _db.Set<ProcumentRecord>()
            .Include(q => q.Supplier)
            .Include(q => q.RFQItem).ThenInclude(i => i.RFQ).ThenInclude(r => r.Customer)
            .Where(q => quoteIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id);

        var errors = new List<string>();
        foreach (var item in request.Items)
        {
            if (item.Coef <= 0) { errors.Add("Every coef must be bigger than 0."); continue; }
            var valid = new List<ProcumentRecord>();
            foreach (var id in item.QuoteIds.Distinct())
            {
                if (!quotes.TryGetValue(id, out var q) || !itemIds.Contains(q.RFQItemId))
                {
                    errors.Add($"Quote {id} is not on this part's RFQs any more — reload the part.");
                    continue;
                }
                if (CommissionRules.IsActive(q.CommissionAt) && !isSuperAdmin)
                {
                    errors.Add($"{q.Supplier.Name} · {q.RFQItem.RFQ.Customer?.CustomerCode ?? "—"} · {q.RFQItem.RFQ.Name}: a commission is saved already (only SuperAdmin can change it).");
                    continue;
                }
                valid.Add(q);
            }
            // Shipping typed in the dialog counts in the price (Unit Price = (Buy Price + Shipping / Qty) × Coef) and is
            // saved on the quote — only written when the whole commission passes (nothing is saved on an error).
            foreach (var s in item.Shipping)
                if (valid.FirstOrDefault(q => q.Id == s.QuoteId) is { } shipped)
                    shipped.ShippingCost = s.ShippingCost is > 0 ? Math.Round(s.ShippingCost.Value, 2) : null;
            // Each RFQ quote on its own Total Price (Unit Price × Qty).
            foreach (var q in valid)
                if (CommissionRuleError(q, q.Supplier.Trusted, q.RFQItem.RFQ.Customer?.MinCoef, item.Coef) is string why)
                    errors.Add($"{q.Supplier.Name} · {q.RFQItem.RFQ.Customer?.CustomerCode ?? "—"} · {q.RFQItem.RFQ.Name}: {why}.");
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors.Distinct()));

        var now = DateTime.UtcNow;
        var updated = 0;
        foreach (var item in request.Items)
            foreach (var id in item.QuoteIds.Distinct())
            {
                var q = quotes[id];
                q.Coef_1 = Math.Round(item.Coef, 4);
                // A unit price typed by hand stays as typed (the coef is not changed to match it) — the Add Quote page
                // shows it as the quote's custom unit price; null = Unit Price comes from the coef.
                q.UnitPrice = item.UnitPrice is > 0 ? Math.Round(item.UnitPrice.Value, 2) : null;
                q.TotalPrice = q.UnitPrice * (q.Qty > 0 ? q.Qty : 1);
                q.CommissionAt = now;
                q.CommissionByUserId = actorId;
                updated++;
            }

        // Commission saved = the part is done here (Sam): it leaves the list.
        var row = await MyParts(userId, PpeKind).FirstAsync(p => p.Id == partId);
        row.HiddenAt = now;
        AddEvent(partId, actorId, "Commission", $"Commission saved on {updated} supplier quote{(updated == 1 ? "" : "s")}");
        await _db.SaveChangesAsync();
        return new PpeCommissionResponse { QuotesUpdated = updated, PartRemoved = true };
    }

    // ──── ILS rows → draft lines ────

    /// <summary>
    /// Picked ILS rows become draft lines (no price) on every listed part each row answers (by its P/N or Alt), or only on
    /// PartId — ONE line per supplier per part, without condition or alt (Sam 2026-10-26): the chips picked say what to
    /// ask for, the supplier's quote mail fills price, condition and alt when it is accepted in Email Quotes
    /// (<see cref="PriceFromAcceptedEmailsAsync"/>). A supplier with a line still waiting for its price, or one without
    /// condition / alt, is there already.
    /// </summary>
    public async Task<PpeAddOffersResponse> AddOffersAsync(long userId, PpeAddOffersRequest request)
    {
        var response = new PpeAddOffersResponse();
        var offers = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Include(o => o.Upload)
            .Include(o => o.Supplier)
            .Where(o => request.OfferIds.Contains(o.Id) && o.Upload.UserId == userId)
            .ToListAsync();
        if (offers.Count == 0) return response;
        var parts = (await LoadPpePartsAsync(userId, false)).Where(p => request.PartId == null || p.Part.Id == request.PartId).ToList();
        var index = IndexByKey(parts);
        var partIds = parts.Select(p => p.Part.Id).ToList();
        // The supplier is on the part already: a line waiting for its price, or one without condition / alt (any status).
        var waiting = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(l => l.Source == Ppe && l.Status != Removed
                && ((l.Status == Pending && (l.Price == null || l.Price <= 0)) || (l.Condition == null && l.Alt == null))
                && l.PreferredPartId != null && partIds.Contains(l.PreferredPartId.Value))
            .Select(l => new { l.PreferredPartId, l.SupplierId, l.SupplierNameRaw })
            .ToListAsync();
        var methods = PreferredSupplierMethods.Join(request.Methods) ?? PreferredSupplierMethods.Ils;

        var picks = offers
            .SelectMany(o =>
            {
                var targets = PartsFor(index, o.PartKey, o.AltKey);
                return (targets.Count == 0 && request.PartId != null ? parts : targets).Select(p => (Part: p, Offer: o));
            })
            .GroupBy(x => (x.Part.Part.Id, Who: x.Offer.SupplierId?.ToString() ?? SupplierNameNormalizer.Key(x.Offer.CompanyRaw)));
        foreach (var pick in picks)
        {
            var part = pick.First().Part;
            // The file's prices are only a hint (the cheapest listing): the price comes from the user or the supplier's mail.
            var offer = pick.Select(x => x.Offer).OrderBy(o => o.Price == null).ThenBy(o => o.Price).First();
            var exists = waiting.Any(l => l.PreferredPartId == part.Part.Id
                && ((offer.SupplierId != null && l.SupplierId == offer.SupplierId)
                    || SupplierNameNormalizer.Key(l.SupplierNameRaw ?? "") == SupplierNameNormalizer.Key(offer.CompanyRaw)));
            if (exists)
            {
                response.Existing++;
                continue;
            }
            _db.Set<PendingSupplierQuote>().Add(new PendingSupplierQuote
            {
                Source = Ppe,
                SourceMethod = methods,
                CreatedByUserId = part.Part.UserId,
                PreferredPartId = part.Part.Id,
                SupplierId = offer.SupplierId,
                SupplierNameRaw = Trunc(offer.Supplier?.Name ?? offer.CompanyRaw, 300),
                SupplierEmail = Trunc(offer.Email ?? SupplierEmails.Normalize(offer.Supplier?.Email), 320),
                EmailSubject = Trunc(PpeFileSubject + offer.Upload.FileName, 500),
                PartNumber = Trunc(part.Part.Name, 200)!,
                PriceRaw = Trunc(offer.PriceRaw ?? offer.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture), 100),
                Status = Pending,
                CreatedAt = DateTime.UtcNow,
            });
            response.Added++;
        }
        await _db.SaveChangesAsync();
        return response;
    }

    // ──── Email Quotes accept → the suppliers' lines here ────

    /// <summary>
    /// When Email Quotes accepts a supplier's mail (Sam 2026-10-26), that supplier's lines under Procurement Plus parts —
    /// same catalog supplier, address or company domain, the part or one of its alternatives — take the quote: price,
    /// condition, alt and the other details, and go onto the part's RFQs (deadline at most 14 days ago). Which line:
    /// one that is exactly this quote (<see cref="QuoteIdentity"/> — rewritten), else one still waiting for a price
    /// (added from the ILS files without condition / alt); any other quote of a supplier already priced here becomes a
    /// line of its own — a price is never replaced. Suppliers nobody added under the part are left alone.
    /// </summary>
    public async Task<int> PriceFromAcceptedEmailsAsync(List<long> emailLineIds, long actorId)
    {
        if (emailLineIds.Count == 0) return 0;
        var emails = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Include(l => l.Supplier)
            .Where(l => emailLineIds.Contains(l.Id) && l.Source == null && l.Status == Accepted && l.Price > 0 && l.SupplierId != null)
            .OrderBy(l => l.Id)
            .ToListAsync();
        if (emails.Count == 0) return 0;

        // Supplier lines being priced, or written in the last 14 days (a quote replacing their price).
        var since = DateTime.UtcNow.AddDays(-PpeDeadlineDays);
        var supplierIds = emails.Select(e => e.SupplierId!.Value).Distinct().ToList();
        var lines = (await _db.Set<PendingSupplierQuote>()
                .Where(l => l.Source == Ppe && l.PreferredPartId != null
                    && (l.Status == Pending || (l.Status == Accepted && l.ReviewedAt >= since))
                    && (l.SupplierId == null || supplierIds.Contains(l.SupplierId.Value)))
                .OrderBy(l => l.CreatedAt)
                .ToListAsync())
            .Where(l => emails.Any(e => EmailQuoteMatch.SameSupplier(l, e)))
            .ToList();
        if (lines.Count == 0) return 0;
        // Every user's parts (whoever added the supplier), removed ones too — the price still belongs on their RFQs.
        var parts = (await LoadPartsAsync(null, true, kind: PpeKind, ids: lines.Select(l => l.PreferredPartId!.Value).Distinct().ToList()))
            .ToDictionary(p => p.Part.Id);

        var priced = new List<PendingSupplierQuote>();
        foreach (var email in emails)
        {
            var keys = EmailQuoteMatch.PartKeys(email).ToHashSet();
            var byPart = lines
                .Where(l => parts.TryGetValue(l.PreferredPartId!.Value, out var p) && p.Keys.Overlaps(keys) && EmailQuoteMatch.SameSupplier(l, email))
                .GroupBy(l => l.PreferredPartId!.Value)
                .ToList();
            foreach (var group in byPart)
            {
                var part = parts[group.Key];
                var quoted = AltPartNumbers.Clean(email.Alt) ?? email.PartNumber.Trim();
                var ownKeys = new[] { part.Part.Name, part.Part.CatalogPart?.Name }.Select(PnKey).Where(k => k.Length > 0).ToHashSet();
                var alt = ownKeys.Contains(PnKey(quoted)) ? null : Trunc(quoted, 200);
                // Exactly this quote already (every field the same) = rewrite it; else fill a line still waiting for its
                // price; else a line of its own — a second price never replaces the first (Sam 2026-10-26).
                var line = group.FirstOrDefault(l => l.Price > 0 && QuoteIdentity.Same(l, l.Alt, email, alt))
                    ?? group.FirstOrDefault(l => l.Status == Pending && l.Price is null or <= 0);
                if (line == null)
                {
                    // Another price / condition / alt from the supplier: a second line beside the first.
                    var first = group.First();
                    line = new PendingSupplierQuote
                    {
                        Source = Ppe,
                        SourceMethod = first.SourceMethod,
                        CreatedByUserId = first.CreatedByUserId,
                        PreferredPartId = first.PreferredPartId,
                        PreferredId = first.PreferredId,
                        PartNumber = first.PartNumber,
                        EmailSubject = Trunc(PpeFileSubject.TrimEnd(' ', '·') + " · Email quote", 500),
                        Status = Pending,
                        CreatedAt = DateTime.UtcNow,
                    };
                    _db.Set<PendingSupplierQuote>().Add(line);
                    lines.Add(line);
                }
                FillFromEmail(line, email, alt);
                priced.Add(line);
                AddEvent(part.Part.Id, actorId, "Supplier",
                    $"{line.SupplierNameRaw} priced by email {Money(email.Price)}{(email.Condition != null ? " " + email.Condition : "")}{(alt != null ? " · alt " + alt : "")}");
            }
        }
        await _db.SaveChangesAsync();

        // New / waiting lines are accepted onto the part's RFQs; lines written before are written again with the new price.
        var toAccept = priced.Where(l => l.Status == Pending).Select(l => l.Id).Distinct().ToList();
        var toReapply = priced.Where(l => l.Status == Accepted).Select(l => l.Id).Distinct().ToList();
        if (toAccept.Count > 0) await _quotes.AcceptAsync(toAccept, actorId, allowWithoutRfq: true, graceDays: PpeDeadlineDays);
        if (toReapply.Count > 0) await _quotes.ReapplyAsync(toReapply, actorId, PpeDeadlineDays);
        return priced.Select(l => l.Id).Distinct().Count();
    }

    /// <summary>The supplier's email quote onto a line: price, condition and alt always; the other details where the mail has them.</summary>
    private static void FillFromEmail(PendingSupplierQuote line, PendingSupplierQuote email, string? alt)
    {
        line.SupplierId = email.SupplierId;
        line.SupplierNameRaw = Trunc(email.Supplier?.Name ?? line.SupplierNameRaw ?? email.SupplierNameRaw, 300);
        line.SupplierEmail = Trunc(SupplierEmails.Normalize(email.SupplierEmail) ?? line.SupplierEmail, 320);
        line.Price = email.Price;
        line.PriceRaw = null;
        line.Condition = email.Condition;
        line.Alt = alt;
        line.Qty = email.Qty ?? line.Qty;
        line.CertName = email.CertName ?? line.CertName;
        line.TagDate = email.TagDate ?? line.TagDate;
        line.TagDateRaw = email.TagDateRaw ?? line.TagDateRaw;
        line.LeadTime = email.LeadTime ?? line.LeadTime;
        line.SellLeadTime = email.SellLeadTime ?? line.SellLeadTime;
        line.ShippingCost = email.ShippingCost ?? line.ShippingCost;
        line.ShippingPoint = email.ShippingPoint ?? line.ShippingPoint;
        line.Remark = email.Remark ?? line.Remark;
        line.SerialNumber = email.SerialNumber ?? line.SerialNumber;
        line.TraceTo = email.TraceTo ?? line.TraceTo;
        line.TagType = email.TagType ?? line.TagType;
        line.PriceType = email.PriceType ?? line.PriceType;
        line.TaggedBy = email.TaggedBy ?? line.TaggedBy;
        line.MoQ = email.MoQ ?? line.MoQ;
        line.Warranty = email.Warranty ?? line.Warranty;
        line.CR = email.CR ?? line.CR;
    }

    // ──── History / availability / files ────

    public async Task<PpPartHistory?> GetPpeHistoryAsync(long? userId, long partId)
    {
        var part = await LoadPpePartAsync(userId, partId);
        if (part == null) return null;
        var quotes = (await QuotesByKeysAsync(new() { [partId] = part.Keys }, part.Names, null)).GetValueOrDefault(partId) ?? new();
        return new PpPartHistory
        {
            PartNumber = part.Part.Name,
            Quotes = quotes.Take(MaxHistoryQuotes).ToList(),
            QuoteCount = quotes.Count,
        };
    }

    private async Task<List<PpeLineHead>> PpeLineHeadsAsync(List<long> partIds) =>
        partIds.Count == 0 ? new() : await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(l => l.Source == Ppe && l.Status != Removed && l.PreferredPartId != null && partIds.Contains(l.PreferredPartId.Value))
            .Select(l => new PpeLineHead
            {
                Id = l.Id,
                PartId = l.PreferredPartId!.Value,
                SupplierId = l.SupplierId,
                PreferredId = l.PreferredId,
                SupplierNameRaw = l.SupplierNameRaw,
                Condition = l.Condition,
                Alt = l.Alt,
                Status = l.Status,
                Price = l.Price,
            })
            .ToListAsync();

    /// <summary>Available suppliers for one part: rows of the user's current ILS files answering it.</summary>
    public async Task<PpeOffersResponse> GetPartOffersAsync(long userId, long partId)
    {
        var part = await LoadPpePartAsync(userId, partId) ?? throw new InvalidOperationException("Part not found.");
        await RelinkFreshOffersAsync(userId, [part]);
        var rows = await OfferRowsAsync([part], userId);
        await AddKnownPricesAsync(rows);
        return new PpeOffersResponse
        {
            Rows = rows,
            Lines = await PpeLineHeadsAsync([partId]),
        };
    }

    /// <summary>A price is "valid" (shown beside an ILS chip) when it is at most this many days old — like the RFQ price age.</summary>
    public const int KnownPriceDays = 14;

    /// <summary>
    /// Puts on each file row the newest valid price (≤ 14 days) we already have from that catalog supplier for the
    /// same P/N (the part or the alternative the file lists) and condition: an RFQ supplier quote or an Email Quotes line.
    /// </summary>
    private async Task AddKnownPricesAsync(List<PpOfferRow> rows)
    {
        var supplierIds = rows.Select(r => r.SupplierId).OfType<long>().Distinct().ToList();
        if (supplierIds.Count == 0) return;
        var names = rows.Select(r => r.FilePartNo.Trim())
            .Concat(rows.Select(r => PnKey(r.FilePartNo)))
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var since = DateTime.UtcNow.AddDays(-KnownPriceDays);

        var rfq = (await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(q => supplierIds.Contains(q.SupplierId) && q.Price > 0 && (q.Type ?? "Procument") != "Shop"
                    && (q.UpdatedAt ?? q.CreatedAt) >= since
                    && ((q.Alt != null && names.Contains(q.Alt))
                        || names.Contains(q.RFQItem.PartNumber.Name)
                        || (q.RFQItem.PartNumber.NewName != null && names.Contains(q.RFQItem.PartNumber.NewName))))
                .Select(q => new
                {
                    q.SupplierId, q.Alt, q.Condition, ItemCondition = q.RFQItem.Condition, q.Price,
                    PartName = q.RFQItem.PartNumber.Name, NewName = q.RFQItem.PartNumber.NewName,
                    When = q.UpdatedAt ?? q.CreatedAt, RfqName = q.RFQItem.RFQ.Name,
                })
                .ToListAsync())
            .Select(q => (q.SupplierId,
                // The P/N the quote is for: its alt, else the RFQ item's part (and its new name).
                Keys: string.IsNullOrWhiteSpace(q.Alt) ? new[] { PnKey(q.PartName), PnKey(q.NewName) } : new[] { PnKey(q.Alt) },
                Cond: CondKey(q.Condition ?? q.ItemCondition), q.Price, q.When, Source: "RFQ", Ref: (string?)q.RfqName));

        var email = (await _db.Set<PendingSupplierQuote>().AsNoTracking()
                .Where(l => l.Source == null && l.Status != Removed && l.Price > 0 && l.SupplierId != null
                    && supplierIds.Contains(l.SupplierId.Value) && (l.EmailDate ?? l.CreatedAt) >= since
                    && (names.Contains(l.PartNumber) || (l.Alt != null && names.Contains(l.Alt))))
                .Select(l => new { SupplierId = l.SupplierId!.Value, l.PartNumber, l.Alt, l.Condition, Price = l.Price!.Value, When = l.EmailDate ?? l.CreatedAt })
                .ToListAsync())
            .Select(l => (l.SupplierId,
                Keys: string.IsNullOrWhiteSpace(l.Alt) ? new[] { PnKey(l.PartNumber) } : new[] { PnKey(l.Alt), PnKey(l.PartNumber) },
                Cond: CondKey(l.Condition), l.Price, l.When, Source: "Email", Ref: (string?)null));

        var known = rfq.Concat(email).ToList();
        foreach (var row in rows.Where(r => r.SupplierId != null))
        {
            var key = PnKey(row.FilePartNo);
            var cond = CondKey(row.Condition);
            var best = known
                .Where(k => k.SupplierId == row.SupplierId && k.Keys.Contains(key) && (cond.Length == 0 || k.Cond == cond))
                .OrderByDescending(k => k.When)
                .FirstOrDefault();
            if (best.Source == null) continue;
            row.KnownPrice = best.Price;
            row.KnownPriceSource = best.Source;
            row.KnownPriceRef = best.Ref;
            row.KnownPriceDate = best.When;
        }
    }

    /// <summary>ILS tab: the user's current files and every row answering one of their listed parts.</summary>
    public async Task<PpeIlsResponse> GetIlsAsync(long userId)
    {
        var since = DateTime.UtcNow.AddDays(-FreshDays);
        var parts = await LoadPpePartsAsync(userId, false);
        await RelinkFreshOffersAsync(userId, parts);
        var uploads = await _db.Set<ProcurementPlusUpload>().AsNoTracking()
            .Where(u => u.UserId == userId && u.UploadedAt >= since)
            .OrderByDescending(u => u.UploadedAt)
            .ToListAsync();
        var rows = uploads.Count == 0 ? new List<PpOfferRow>() : await OfferRowsAsync(parts, userId);
        await AddKnownPricesAsync(rows);
        var items = rows.Count == 0 ? new() : await PpeItemsAsync(parts);
        return new PpeIlsResponse
        {
            FreshDays = FreshDays,
            Uploads = uploads.Select(u => new PpUploadResponse
            {
                Id = u.Id,
                FileName = u.FileName,
                UploadedAt = u.UploadedAt,
                RowCount = u.RowCount,
                MatchedRows = rows.Where(r => r.UploadId == u.Id).Select(r => r.OfferId).Distinct().Count(),
            }).ToList(),
            Rows = rows,
            Preferred = await GetPreferredAsync(userId),
            Lines = await PpeLineHeadsAsync(parts.Select(p => p.Part.Id).ToList()),
            Parts = parts.Select(p => new PpePartBrief
            {
                Id = p.Part.Id,
                PartNumber = p.Part.Name,
                Description = p.Part.Description ?? p.Part.CatalogPart?.Description,
                Qty = p.Part.Qty,
                Condition = p.Part.Condition,
                Conditions = (items.GetValueOrDefault(p.Part.Id) ?? []).Select(i => CondKey(i.Condition)).Append(CondKey(p.Part.Condition))
                    .Where(c => c.Length > 0).Distinct().ToList(),
            }).ToList(),
        };
    }

    /// <summary>Same files as ProMax (per user); matched rows are counted against the user's Procurement Plus parts.</summary>
    public async Task<PpUploadResult> UploadPpeAsync(long userId, IEnumerable<IFormFile> files)
    {
        var result = await UploadAsync(userId, files);
        var keys = (await LoadPpePartsAsync(userId, false)).SelectMany(p => p.Keys).ToHashSet();
        var ids = result.Uploads.Select(u => u.Id).ToList();
        var counts = await _db.Set<ProcurementPlusOffer>().AsNoTracking()
            .Where(o => ids.Contains(o.UploadId))
            .Select(o => new { o.UploadId, o.PartKey, o.AltKey })
            .ToListAsync();
        foreach (var u in result.Uploads)
            u.MatchedRows = counts.Count(o => o.UploadId == u.Id && (keys.Contains(o.PartKey) || (o.AltKey != null && keys.Contains(o.AltKey))));
        return result;
    }

    /// <summary>SuperAdmin's user filter: everyone with Procurement Plus parts.</summary>
    public async Task<List<PpUserSummary>> GetPpeUsersAsync()
    {
        var counts = await _db.Set<Preferred>().AsNoTracking()
            .Where(p => p.Kind == PpeKind && p.HiddenAt == null && p.UserId != null)
            .GroupBy(p => p.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();
        var names = await UserNamesAsync(counts.Select(c => (long?)c.UserId));
        return counts.Select(c => new PpUserSummary { Id = c.UserId, Name = names.GetValueOrDefault(c.UserId) ?? $"#{c.UserId}", PartCount = c.Count })
            .OrderBy(u => u.Name)
            .ToList();
    }
}
