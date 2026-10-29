using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Catalog.Services;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Email RFQs ↔ Procurement Plus: the PPE (Procurement Part Expert) a part is assigned to, and the part
/// insights on the Pending tab (average supplier price, last customers who asked for it).
/// </summary>
public partial class PendingRfqService
{
    /// <summary>Menu Access feature of the new Procurement Plus page — only those users can be a PPE.</summary>
    private const string ProcurementPlusFeature = "procurementPlus";
    private const int RecentCustomerCount = 5;
    private const int PartCustomersCount = 25;
    private const int RequestsPerCustomer = 10;
    /// <summary>Accepted tab "Customers this week": RFQs created in the last this many days.</summary>
    private const int WeekCustomerDays = 7;
    private sealed record WeekRequest(long PartNumberId, long CustomerId, double Qty, int Count, DateTime Last);
    private sealed record CustomerRequest(long PartNumberId, long CustomerId, int Count, DateTime Last);
    /// <summary>Most P/Ns one part-insights call looks up (the page sends them in batches).</summary>
    private const int MaxInsightParts = 200;

    // ──── PPE ────

    /// <summary>
    /// Assigns the parts of these accepted lines to a PPE: every accepted line with the same P/N gets them
    /// (Sam), the user's Procurement Plus gets the part (shown again if they had removed it), and the
    /// previous PPE's draft supplier lines move with it. Null unassigns.
    /// </summary>
    public async Task<AssignPpeResponse> AssignPpeAsync(List<long> ids, long? ppeUserId, long actorId)
    {
        string? userName = null;
        if (ppeUserId != null)
            userName = await _db.Set<User>().AsNoTracking().Where(u => u.Id == ppeUserId).Select(u => u.Name).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("User not found.");

        var picked = await _db.Set<PendingRfqItem>().AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Status == Accepted)
            .Select(p => p.PartNumber)
            .ToListAsync();
        var keys = picked.Select(PnKey).Where(k => k.Length > 0).Distinct().ToList();
        if (keys.Count == 0) throw new InvalidOperationException("Pick accepted lines first.");

        var lines = await _db.Set<PendingRfqItem>()
            .Where(p => p.Status == Accepted && keys.Contains(p.PartNumber.Replace(" ", "").ToUpper()))
            .ToListAsync();
        lines = lines.Where(l => keys.Contains(PnKey(l.PartNumber))).ToList();
        var now = DateTime.UtcNow;
        var response = new AssignPpeResponse { UserName = userName };

        var parts = await _db.Set<Preferred>()
            .Where(p => p.Kind == PreferredKinds.PpePart && keys.Contains(p.NameKey))
            .ToListAsync();
        foreach (var key in keys)
        {
            var keyLines = lines.Where(l => PnKey(l.PartNumber) == key).ToList();
            if (keyLines.Count == 0) continue;
            var previousUsers = keyLines.Select(l => l.PpeUserId).OfType<long>().Where(u => u != ppeUserId).Distinct().ToList();
            foreach (var line in keyLines)
            {
                line.PpeUserId = ppeUserId;
                line.PpeAssignedAt = ppeUserId == null ? null : now;
                line.PpeAssignedByUserId = ppeUserId == null ? null : actorId;
            }
            response.Lines += keyLines.Count;

            Preferred? part = null;
            if (ppeUserId != null)
            {
                part = parts.FirstOrDefault(p => p.UserId == ppeUserId && p.NameKey == key);
                if (part == null)
                {
                    part = await NewPpePartAsync(ppeUserId.Value, key, keyLines, actorId);
                    parts.Add(part);
                }
                part.HiddenAt = null;
                part.AddedAt = now; // "assigned" date — a part with no open RFQ goes to "older" 14 days after this
                part.AddedByUserId = actorId;
                await _db.SaveChangesAsync();
                response.Parts++;
            }

            // Assigned again (same or another PPE): the old commission goes from the part's RFQs that have no customer
            // quote yet, so the PPE adds a new one (new 7 days); RFQs already quoted keep theirs (Sam).
            if (ppeUserId != null)
                response.CommissionsReset += await ResetCommissionsAsync(
                    keyLines.SelectMany(l => SplitAlternatives(l.Alternatives).Prepend(l.PartNumber))
                        .Concat(SplitAlternatives(part?.Alternatives).Prepend(part?.Name ?? "")));

            foreach (var old in parts.Where(p => p.UserId != null && previousUsers.Contains(p.UserId.Value) && p.NameKey == key))
            {
                old.HiddenAt = now;
                if (part != null)
                    await _db.Set<PendingSupplierQuote>()
                        .Where(q => q.Source == PendingSupplierQuote.SourcePpe && q.Status == Pending && q.PreferredPartId == old.Id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(q => q.PreferredPartId, part.Id)
                            .SetProperty(q => q.CreatedByUserId, ppeUserId));
            }
        }
        await _db.SaveChangesAsync();
        return response;
    }

    private async Task<Preferred> NewPpePartAsync(long userId, string key, List<PendingRfqItem> lines, long actorId)
    {
        var newest = lines.OrderByDescending(l => l.ReviewedAt ?? l.CreatedAt).First();
        var name = newest.PartNumber.Trim();
        var bare = Whitespace.Replace(name, "");
        var catalogId = await _db.Set<PartNumber>().AsNoTracking()
            .Where(p => p.Name == name || p.Name == bare || p.NewName == name || p.NewName == bare)
            .Select(p => (long?)p.Id)
            .FirstOrDefaultAsync();
        var alternatives = JoinAlternatives(lines.SelectMany(l => SplitAlternatives(l.Alternatives)));
        var part = new Preferred
        {
            Kind = PreferredKinds.PpePart,
            UserId = userId,
            Name = Trunc(name, 300)!,
            NameKey = Trunc(key, 300)!,
            Description = Trunc(NullIfBlank(lines.Select(l => l.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))), 500),
            Alternatives = Trunc(alternatives, 2000),
            PartNumberId = catalogId,
            Qty = newest.Qty,
            Condition = Trunc(NullIfBlank(newest.Condition), 50),
            AddedAt = DateTime.UtcNow,
            AddedByUserId = actorId,
        };
        _db.Set<Preferred>().Add(part);
        return part;
    }

    /// <summary>
    /// Newly accepted lines whose P/N already has a PPE get that PPE (not "assigned again": an active commission keeps
    /// going and reaches the new RFQ). The part shows again on their page unless an active commission covers it.
    /// </summary>
    private async Task AutoAssignPpeAsync(List<PendingRfqItem> lines, long actorId)
    {
        var open = lines.Where(l => l.PpeUserId == null).ToList();
        if (open.Count == 0) return;
        var owners = await PpeOwnersAsync(open.Select(l => l.PartNumber));
        var now = DateTime.UtcNow;
        var assigned = new HashSet<(long UserId, string Key)>();
        foreach (var line in open)
        {
            if (!owners.TryGetValue(PnKey(line.PartNumber), out var owner)) continue;
            line.PpeUserId = owner.UserId;
            line.PpeAssignedAt = now;
            line.PpeAssignedByUserId = actorId;
            assigned.Add((owner.UserId, PnKey(line.PartNumber)));
        }
        foreach (var (userId, key) in assigned)
        {
            var part = await _db.Set<Preferred>()
                .FirstOrDefaultAsync(p => p.Kind == PreferredKinds.PpePart && p.UserId == userId && p.NameKey == key);
            if (part == null || part.HiddenAt == null) continue;
            var itemIds = await PartRfqItemIdsAsync(SplitAlternatives(part.Alternatives).Prepend(part.Name));
            var covered = await _db.Set<ProcumentRecord>()
                .AnyAsync(q => itemIds.Contains(q.RFQItemId) && q.CommissionAt >= CommissionRules.ActiveSince);
            if (!covered) part.HiddenAt = null; // a new RFQ with no active commission needs the PPE again
        }
    }

    /// <summary>
    /// P/N key → its PPE: the user the accepted lines with that P/N are assigned to (newest assignment), so an
    /// unassigned or reassigned part follows what the reviewer last did.
    /// </summary>
    private async Task<Dictionary<string, (long UserId, string Name)>> PpeOwnersAsync(IEnumerable<string> partNumbers)
    {
        var keys = partNumbers.Select(PnKey).Where(k => k.Length > 0).Distinct().ToList();
        if (keys.Count == 0) return new();
        var rows = (await _db.Set<PendingRfqItem>().AsNoTracking()
                .Where(p => p.Status == Accepted && p.PpeUserId != null && keys.Contains(p.PartNumber.Replace(" ", "").ToUpper()))
                .Select(p => new { p.PartNumber, UserId = p.PpeUserId!.Value, p.PpeAssignedAt, p.Id })
                .ToListAsync())
            .Where(r => keys.Contains(PnKey(r.PartNumber)))
            .ToList();
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var names = await _db.Set<User>().AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
        return rows.GroupBy(r => PnKey(r.PartNumber))
            .ToDictionary(g => g.Key, g =>
            {
                var r = g.OrderByDescending(x => x.PpeAssignedAt).ThenByDescending(x => x.Id).First();
                return (r.UserId, names.GetValueOrDefault(r.UserId) ?? "");
            });
    }

    /// <summary>RFQ items for these P/Ns (name, new name, item alt, catalog alternatives) with a deadline at most 14 days ago.</summary>
    private async Task<List<long>> PartRfqItemIdsAsync(IEnumerable<string?> partNumbers)
    {
        var list = partNumbers.OfType<string>().Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var names = list.Select(p => p.Trim()).Concat(list.Select(p => Whitespace.Replace(p, "")))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return new();
        var earliest = DateTime.UtcNow.Date.AddDays(-ProcurementPlusService.PpeDeadlineDays);
        return await _db.Set<RFQItem>().AsNoTracking()
            .Where(i => i.RFQ.LeadTime >= earliest
                && (names.Contains(i.PartNumber.Name)
                    || (i.PartNumber.NewName != null && names.Contains(i.PartNumber.NewName))
                    || (i.Alt != null && names.Contains(i.Alt))
                    || i.PartNumber.Alternatives.Any(a => names.Contains(a.Name))))
            .Select(i => i.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Takes the saved commission off the part's RFQ quotes whose RFQ item has no customer quote yet (Coef 1 cleared,
    /// unlocked); items already quoted to the customer keep theirs. Returns how many quotes were reset.
    /// </summary>
    private async Task<int> ResetCommissionsAsync(IEnumerable<string?> partNumbers)
    {
        var itemIds = await PartRfqItemIdsAsync(partNumbers);
        if (itemIds.Count == 0) return 0;
        var withCommission = await _db.Set<ProcumentRecord>()
            .Where(q => itemIds.Contains(q.RFQItemId) && q.CommissionAt != null)
            .ToListAsync();
        if (withCommission.Count == 0) return 0;
        var quoted = (await _quotePrices.ByRfqItemsAsync(withCommission.Select(q => q.RFQItemId).Distinct().ToList())).Keys.ToHashSet();
        var reset = 0;
        foreach (var q in withCommission.Where(q => !quoted.Contains(q.RFQItemId)))
        {
            q.Coef_1 = null;
            q.CommissionAt = null;
            q.CommissionByUserId = null;
            reset++;
        }
        if (reset > 0) await _db.SaveChangesAsync();
        return reset;
    }

    private async Task<Dictionary<string, string>> KnownPpeNamesAsync(IEnumerable<string> partNumbers) =>
        (await PpeOwnersAsync(partNumbers)).ToDictionary(o => o.Key, o => o.Value.Name);

    public async Task<List<QuoteReviewerOption>> GetPpeUsersAsync()
    {
        var names = await _db.Set<MenuPermission>().AsNoTracking()
            .Where(p => p.Feature == ProcurementPlusFeature)
            .Select(p => p.UserName)
            .ToListAsync();
        return await _db.Set<User>().AsNoTracking()
            .Where(u => names.Contains(u.Name) && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new QuoteReviewerOption(u.Id, u.Name))
            .ToListAsync();
    }

    // ──── Part insights (Pending tab) ────

    /// <summary>
    /// Catalog parts each P/N answers to (its own row by name / new name, and every part in its alternative
    /// family — X → Y → Z chains both ways, see PartFamilies), keyed by the P/N key.
    /// </summary>
    private async Task<Dictionary<string, HashSet<long>>> PartIdsByKeyAsync(IEnumerable<string> partNumbers)
    {
        var list = partNumbers.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var result = new Dictionary<string, HashSet<long>>();
        if (list.Count == 0) return result;
        var families = await PartFamilies.LoadAsync(_db, list);
        // Family key → the asked P/N keys it belongs to.
        var askedBy = new Dictionary<string, HashSet<string>>();
        foreach (var key in list.Select(PnKey).Where(k => k.Length > 0).Distinct())
            foreach (var member in families.FamilyOf(key).Append(key))
            {
                if (!askedBy.TryGetValue(member, out var asked)) askedBy[member] = asked = new();
                asked.Add(key);
            }
        var spellings = list.Select(p => p.Trim()).Concat(families.Names).ToList();
        var names = spellings.Concat(spellings.Select(p => Whitespace.Replace(p, "")))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var parts = await _db.Set<PartNumber>().AsNoTracking()
            .Where(p => names.Contains(p.Name)
                || (p.NewName != null && names.Contains(p.NewName))
                || p.Alternatives.Any(a => names.Contains(a.Name)))
            .Select(p => new { p.Id, p.Name, p.NewName, Alternatives = p.Alternatives.Select(a => a.Name).ToList() })
            .ToListAsync();
        foreach (var p in parts)
            foreach (var key in new[] { p.Name, p.NewName ?? "" }.Concat(p.Alternatives).Select(PnKey)
                         .SelectMany(k => askedBy.GetValueOrDefault(k) ?? []).Distinct())
            {
                if (!result.TryGetValue(key, out var set)) result[key] = set = new();
                set.Add(p.Id);
            }
        return result;
    }

    /// <summary>
    /// Average supplier quote price (every RFQ, price &gt; 0, no shop rows), the last 5 customers who asked for
    /// each part — "C501 (2) C502 (4)" — and/or this week's customers and the known PPE. Loaded by the page after
    /// the list, so the list itself never waits for these.
    /// </summary>
    public async Task<List<PartInsight>> GetPartInsightsAsync(PartInsightsRequest request)
    {
        var asked = request.PartNumbers.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(MaxInsightParts).ToList();
        var items = asked.Select(p => new PartInsight { PartNumber = p }).ToList();
        if (items.Count == 0) return items;

        if (request.KnownPpe)
        {
            var known = await KnownPpeNamesAsync(asked);
            foreach (var item in items) item.KnownPpeName = known.GetValueOrDefault(PnKey(item.PartNumber));
        }

        var partIds = await PartIdsByKeyAsync(asked);
        var allIds = partIds.Values.SelectMany(s => s).Distinct().ToList();
        if (allIds.Count == 0) return items;

        var prices = await _db.Set<ProcumentRecord>().AsNoTracking()
            .Where(q => allIds.Contains(q.RFQItem.PartNumberId) && q.Price > 0 && (q.Type ?? "Procument") != "Shop")
            .GroupBy(q => new { q.RFQItem.PartNumberId, q.Condition })
            .Select(g => new { g.Key.PartNumberId, g.Key.Condition, Sum = g.Sum(q => q.Price), Count = g.Count() })
            .ToListAsync();
        List<CustomerRequest> requests = !request.RecentCustomers ? new() : await _db.Set<RFQItem>().AsNoTracking()
            .Where(i => allIds.Contains(i.PartNumberId))
            .GroupBy(i => new { i.PartNumberId, i.RFQ.CustomerId })
            .Select(g => new CustomerRequest(g.Key.PartNumberId, g.Key.CustomerId, g.Count(), g.Max(i => i.RFQ.CreatedAt)))
            .ToListAsync();
        var weekCustomers = request.WeekCustomers;
        var weekSince = DateTime.UtcNow.AddDays(-WeekCustomerDays);
        List<WeekRequest> week = !weekCustomers ? new() : await _db.Set<RFQItem>().AsNoTracking()
            .Where(i => allIds.Contains(i.PartNumberId) && i.RFQ.CreatedAt >= weekSince)
            .GroupBy(i => new { i.PartNumberId, i.RFQ.CustomerId })
            .Select(g => new WeekRequest(g.Key.PartNumberId, g.Key.CustomerId, g.Sum(i => i.Qty), g.Count(), g.Max(i => i.RFQ.CreatedAt)))
            .ToListAsync();
        var customerIds = requests.Select(r => r.CustomerId).Concat(week.Select(w => w.CustomerId)).Distinct().ToList();
        var customers = await _db.Set<Customer>().AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.CustomerCode })
            .ToDictionaryAsync(c => c.Id);

        foreach (var item in items)
        {
            if (!partIds.TryGetValue(PnKey(item.PartNumber), out var ids)) continue;
            var byCondition = prices.Where(p => ids.Contains(p.PartNumberId))
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Condition) ? "-" : p.Condition!.Trim().ToUpperInvariant())
                .Select(g => new PartConditionPrice(g.Key, Math.Round(g.Sum(x => x.Sum) / g.Sum(x => x.Count), 2), g.Sum(x => x.Count)))
                .OrderByDescending(c => c.Count)
                .ToList();
            var count = byCondition.Sum(c => c.Count);
            if (count > 0)
            {
                item.AvgPriceByCondition = byCondition;
                item.AvgPriceCount = count;
                item.AvgPrice = Math.Round(byCondition.Sum(c => c.Average * c.Count) / count, 2);
            }
            if (request.RecentCustomers)
                item.RecentCustomers = requests.Where(r => ids.Contains(r.PartNumberId))
                    .GroupBy(r => r.CustomerId)
                    .Select(g => new { CustomerId = g.Key, Count = g.Sum(x => x.Count), Last = g.Max(x => x.Last) })
                    .OrderByDescending(c => c.Last)
                    .Take(RecentCustomerCount)
                    .Select(c => customers.TryGetValue(c.CustomerId, out var cu)
                        ? new PartCustomerCount(c.CustomerId, cu.CustomerCode, cu.Name, c.Count)
                        : new PartCustomerCount(c.CustomerId, null, $"#{c.CustomerId}", c.Count))
                    .ToList();
            if (weekCustomers)
                item.WeekCustomers = week.Where(w => ids.Contains(w.PartNumberId))
                    .GroupBy(w => w.CustomerId)
                    .Select(g => new { CustomerId = g.Key, Qty = g.Sum(x => x.Qty), Count = g.Sum(x => x.Count), Last = g.Max(x => x.Last) })
                    .OrderByDescending(c => c.Last)
                    .Select(c => customers.TryGetValue(c.CustomerId, out var cu)
                        ? new PartCustomerQty(c.CustomerId, cu.CustomerCode, cu.Name, c.Qty, c.Count)
                        : new PartCustomerQty(c.CustomerId, null, $"#{c.CustomerId}", c.Qty, c.Count))
                    .ToList();
        }
        return items;
    }

    /// <summary>
    /// The last 25 customers who asked for the part (or one of its alternatives): each request's RFQ, qty and
    /// condition, and what we quoted them for it.
    /// </summary>
    public async Task<PartCustomersResponse> GetPartCustomersAsync(string partNumber)
    {
        var response = new PartCustomersResponse { PartNumber = partNumber.Trim() };
        var ids = (await PartIdsByKeyAsync([partNumber])).Values.SelectMany(s => s).Distinct().ToList();
        if (ids.Count == 0) return response;

        // Newest customers first: their latest request date decides.
        var latest = await _db.Set<RFQItem>().AsNoTracking()
            .Where(i => ids.Contains(i.PartNumberId))
            .GroupBy(i => i.RFQ.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count(), Last = g.Max(i => i.RFQ.CreatedAt) })
            .OrderByDescending(g => g.Last)
            .Take(PartCustomersCount)
            .ToListAsync();
        var customerIds = latest.Select(c => c.CustomerId).ToList();
        var items = await _db.Set<RFQItem>().AsNoTracking()
            .Where(i => ids.Contains(i.PartNumberId) && customerIds.Contains(i.RFQ.CustomerId))
            .Select(i => new
            {
                i.Id,
                i.RFQ.CustomerId,
                CustomerName = i.RFQ.Customer.Name,
                i.RFQ.Customer.CustomerCode,
                RfqId = i.RFQId,
                RfqName = i.RFQ.Name,
                i.RFQ.CreatedAt,
                Deadline = (DateTime?)i.RFQ.LeadTime,
                PartNumber = i.PartNumber.Name,
                i.Qty,
                i.Condition,
            })
            .ToListAsync();
        var kept = items.GroupBy(i => i.CustomerId)
            .SelectMany(g => g.OrderByDescending(i => i.CreatedAt).Take(RequestsPerCustomer))
            .ToList();
        var quoted = await _quotePrices.ByRfqItemsAsync(kept.Select(i => i.Id).ToList());

        response.Customers = latest.Select(c =>
        {
            var mine = kept.Where(i => i.CustomerId == c.CustomerId).OrderByDescending(i => i.CreatedAt).ToList();
            return new PartCustomerHistory
            {
                CustomerId = c.CustomerId,
                CustomerCode = mine.FirstOrDefault()?.CustomerCode,
                CustomerName = mine.FirstOrDefault()?.CustomerName ?? $"#{c.CustomerId}",
                RequestCount = c.Count,
                Requests = mine.Select(i => new PartCustomerRequest
                {
                    RfqId = i.RfqId,
                    RfqName = i.RfqName,
                    RequestedAt = i.CreatedAt,
                    Deadline = i.Deadline,
                    PartNumber = i.PartNumber,
                    Qty = i.Qty,
                    Condition = i.Condition,
                    Quoted = quoted.GetValueOrDefault(i.Id),
                }).ToList(),
            };
        }).ToList();
        return response;
    }
}
