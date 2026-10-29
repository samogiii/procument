using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Services;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Procurement Plus keeps a part's RFQs the same (Sam 2026-10-28): every supplier row under the part is on each of its
/// RFQs (deadline at most 14 days ago), and one RFQ item never shows the same quote twice.
/// </summary>
public partial class ProcurementPlusService
{
    /// <summary>
    /// Runs when a part opens and after each save. First a quote that is on one RFQ item twice or more with every value
    /// the same (the supplier email aside) is kept once; then each supplier row — added here, typed on an RFQ page or
    /// from Email Quotes — is written onto the part's RFQs that lack it. A row with no line behind it (typed on an RFQ
    /// page) gets an accepted Procurement Plus line copied from its newest quote, so new RFQs for the part get it too
    /// (auto-apply). Quotes already on the RFQs are never changed here.
    /// </summary>
    private async Task SyncPpeRfqsAsync(PartKeys part, List<RFQItem> items, long actorId)
    {
        if (items.Count == 0) return;
        var itemIds = items.Select(i => i.Id).ToList();
        await RemoveDuplicateQuotesAsync(itemIds);
        if (items.Count < 2) return;

        var itemById = items.ToDictionary(i => i.Id);
        var cardAlt = CardAltFor(part);
        var quotes = await _db.Set<ProcumentRecord>().AsNoTracking()
            .Include(r => r.Supplier)
            .Where(r => itemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") == "Procument")
            .ToListAsync();
        var groups = quotes
            .GroupBy(q => PpeOfferKey(q, itemById[q.RFQItemId], cardAlt))
            .Select(g => (Quotes: g.OrderByDescending(q => q.UpdatedAt ?? q.CreatedAt).ThenByDescending(q => q.Id).ToList(),
                Missing: itemIds.Where(id => g.All(q => q.RFQItemId != id)).ToList()))
            // Same rules as accepting a line: a price and an approved catalog supplier.
            .Where(g => g.Missing.Count > 0 && g.Quotes[0].Price > 0 && g.Quotes[0].Supplier.Status == "Approved")
            .ToList();
        if (groups.Count == 0) return;

        var ownerIds = groups.SelectMany(g => g.Quotes).Select(q => q.SourcePendingQuoteId).OfType<long>().Distinct().ToList();
        var owners = ownerIds.Count == 0 ? new() : await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(l => ownerIds.Contains(l.Id) && l.Status == Accepted)
            .ToDictionaryAsync(l => l.Id);
        // The part's own accepted lines too: a copy made before is reused, never made again (e.g. when an RFQ can't take it).
        var partLines = await _db.Set<PendingSupplierQuote>().AsNoTracking()
            .Where(l => l.Source == Ppe && l.PreferredPartId == part.Part.Id && l.Status == Accepted)
            .ToListAsync();

        foreach (var (list, missing) in groups)
        {
            var newest = list[0];
            var item = itemById[newest.RFQItemId];
            var key = PpeOfferKey(newest, item, cardAlt);
            // A line behind the row that writes this very offer (the part's own line first), else a new line copied from it.
            var line = list
                .Select(q => q.SourcePendingQuoteId is long id ? owners.GetValueOrDefault(id) : null)
                .OfType<PendingSupplierQuote>()
                .Concat(partLines)
                .DistinctBy(l => l.Id)
                .Where(l => WritesOffer(l, newest, item, cardAlt) == key)
                .OrderBy(l => l.Source == Ppe && l.PreferredPartId == part.Part.Id ? 0 : 1)
                .FirstOrDefault();
            long lineId;
            if (line != null) lineId = line.Id;
            else
            {
                var copy = PpeLineFromQuote(part, newest, item, cardAlt, actorId);
                _db.Set<PendingSupplierQuote>().Add(copy);
                await _db.SaveChangesAsync();
                lineId = copy.Id;
                AddEvent(part.Part.Id, actorId, "Supplier", $"{newest.Supplier.Name} put on every RFQ of the part");
            }
            await _quotes.WriteToItemsAsync(lineId, missing, newest.UserId ?? actorId, PpeDeadlineDays);
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>The row key of what this line writes on an RFQ item like <paramref name="like"/>'s (to tell it is the same offer).</summary>
    private static string WritesOffer(PendingSupplierQuote line, ProcumentRecord like, RFQItem item, Func<string?, string?> cardAlt) =>
        PpeOfferKey(new ProcumentRecord
        {
            SupplierId = line.SupplierId ?? 0,
            Condition = line.Condition ?? like.Condition,
            Alt = AltPartNumbers.Clean(line.Alt) ?? line.PartNumber,
            SerialNumber = line.SerialNumber,
            Price = line.Price ?? -1,
        }, item, cardAlt);

    /// <summary>
    /// An accepted Procurement Plus line with the RFQ quote's values (the quote stays as it is). Qty only when the supplier
    /// gave another qty than the RFQ asked — otherwise each RFQ keeps its own; sell lead time follows each customer.
    /// </summary>
    private static PendingSupplierQuote PpeLineFromQuote(PartKeys part, ProcumentRecord q, RFQItem item, Func<string?, string?> cardAlt, long actorId)
    {
        var now = DateTime.UtcNow;
        return new PendingSupplierQuote
        {
            Source = Ppe,
            SourceMethod = "RFQ",
            EmailSubject = Trunc(PpeFileSubject + "RFQ quote", 500),
            CreatedByUserId = part.Part.UserId,
            PreferredPartId = part.Part.Id,
            PartNumber = Trunc(part.Part.Name, 200)!,
            Alt = Trunc(cardAlt(QuotedPartNumber(q, item)), 200),
            SupplierId = q.SupplierId,
            SupplierNameRaw = Trunc(q.Supplier.Name, 300),
            SupplierEmail = q.SupplierEmail,
            Price = q.Price,
            Qty = Math.Abs(q.Qty - item.Qty) < 1e-9 ? null : q.Qty,
            Condition = q.Condition,
            LeadTime = q.LeadTime,
            CertName = q.CertName,
            TagDate = q.TagDate,
            Remark = q.Note,
            MyNotes = q.MyNotes,
            ShippingCost = q.ShippingCost,
            ShippingPoint = q.ShippingPoint,
            CustomCertReferences = q.CustomCertReferences,
            SerialNumber = q.SerialNumber,
            TraceTo = q.TraceTo,
            TagType = q.TagType,
            PriceType = q.PriceType,
            TaggedBy = q.TaggedBy,
            MoQ = q.MoQ,
            Warranty = q.Warranty,
            CR = q.CR,
            Status = Accepted,
            CreatedAt = now,
            ReviewedAt = now,
            ReviewedByUserId = actorId,
        };
    }

    /// <summary>
    /// On these RFQ items, the supplier quotes that are the same offer (<see cref="QuoteIdentity.Key"/> — every value but
    /// the supplier email) are kept once. The one kept: used by a customer quote / PO / procurement first, then one with a
    /// commission or coef, then one written by a line, then the oldest; it takes the others' notes, coefs, email and certs
    /// where it has none. A duplicate that is used somewhere is never deleted.
    /// </summary>
    private async Task<int> RemoveDuplicateQuotesAsync(List<long> itemIds)
    {
        var quotes = await _db.Set<ProcumentRecord>()
            .Include(r => r.Certificates)
            .Where(r => itemIds.Contains(r.RFQItemId) && (r.Type ?? "Procument") == "Procument")
            .ToListAsync();
        var groups = quotes.GroupBy(q => (q.RFQItemId, QuoteIdentity.Key(q))).Where(g => g.Count() > 1).ToList();
        if (groups.Count == 0) return 0;

        var used = await UsedSupplierQuoteIdsAsync(groups.SelectMany(g => g).Select(q => q.Id).ToList());
        var removed = new List<(ProcumentRecord Dup, ProcumentRecord Keep)>();
        foreach (var group in groups)
        {
            var keep = group
                .OrderByDescending(q => used.Contains(q.Id))
                .ThenByDescending(q => q.CommissionAt != null)
                .ThenByDescending(q => q.Coef_1 != null)
                .ThenByDescending(q => q.SourcePendingQuoteId != null)
                .ThenBy(q => q.CreatedAt)
                .ThenBy(q => q.Id)
                .First();
            foreach (var dup in group.Where(q => q != keep && !used.Contains(q.Id)))
            {
                MergeDuplicate(keep, dup);
                removed.Add((dup, keep));
            }
        }
        if (removed.Count == 0) return 0;

        // Certificates move to the kept quote (file and row); a copy of a PDF it has already (same source) goes with the duplicate.
        foreach (var (dup, keep) in removed)
            foreach (var cert in dup.Certificates.ToList())
            {
                if (cert.SourcePendingCertificateId != null && keep.Certificates.Any(c => c.SourcePendingCertificateId == cert.SourcePendingCertificateId))
                {
                    TryDeleteFile(Path.Combine(SupplierQuoteService.CertificateRoot, dup.Id.ToString(), cert.FileName));
                    continue;
                }
                var from = Path.Combine(SupplierQuoteService.CertificateRoot, dup.Id.ToString(), cert.FileName);
                var folder = Path.Combine(SupplierQuoteService.CertificateRoot, keep.Id.ToString());
                if (File.Exists(from))
                {
                    Directory.CreateDirectory(folder);
                    File.Move(from, Path.Combine(folder, cert.FileName));
                }
                dup.Certificates.Remove(cert);
                cert.SupplierQuoteId = keep.Id;
                cert.SupplierQuote = keep;
                keep.Certificates.Add(cert);
            }

        var dupIds = removed.Select(r => r.Dup.Id).ToList();
        var keepOf = removed.ToDictionary(r => r.Dup.Id, r => r.Keep.Id);
        foreach (var line in await _db.Set<PendingSupplierQuote>()
                     .Where(l => l.SourceSupplierQuoteId != null && dupIds.Contains(l.SourceSupplierQuoteId.Value))
                     .ToListAsync())
            line.SourceSupplierQuoteId = keepOf[line.SourceSupplierQuoteId!.Value];
        await _db.SaveChangesAsync();

        _db.Set<ProcumentRecord>().RemoveRange(removed.Select(r => r.Dup));
        await _db.SaveChangesAsync();
        return removed.Count;
    }

    /// <summary>The kept quote takes what only the duplicate has (our own fields — the offer itself is the same).</summary>
    private static void MergeDuplicate(ProcumentRecord keep, ProcumentRecord dup)
    {
        keep.SupplierEmail ??= dup.SupplierEmail;
        keep.SourceEmail ??= dup.SourceEmail;
        keep.SourcePendingQuoteId ??= dup.SourcePendingQuoteId;
        keep.UserId ??= dup.UserId;
        keep.Note ??= dup.Note;
        keep.MyNotes ??= dup.MyNotes;
        keep.ShippingCost ??= dup.ShippingCost;
        keep.ShippingPoint ??= dup.ShippingPoint;
        keep.SellLeadTime ??= dup.SellLeadTime;
        if (keep.CommissionAt == null && dup.CommissionAt != null)
        {
            keep.CommissionAt = dup.CommissionAt;
            keep.CommissionByUserId = dup.CommissionByUserId;
            keep.Coef_1 = dup.Coef_1;
        }
        keep.Coef_1 ??= dup.Coef_1;
        keep.Coef_2 ??= dup.Coef_2;
        keep.Coef_3 ??= dup.Coef_3;
        keep.UnitPrice ??= dup.UnitPrice;
        keep.TotalPrice ??= dup.TotalPrice;
        keep.FixPrice ??= dup.FixPrice;
        keep.IsCertificated |= dup.IsCertificated;
        keep.CustomCertReferences = SupplierQuoteService.JoinCertReferences(
            SupplierQuoteService.SplitCertReferences(keep.CustomCertReferences)
                .Concat(SupplierQuoteService.SplitCertReferences(dup.CustomCertReferences)));
        var updated = dup.UpdatedAt ?? dup.CreatedAt;
        if (updated > (keep.UpdatedAt ?? keep.CreatedAt)) keep.UpdatedAt = updated; // the newest price age
    }

    /// <summary>Supplier quotes something else points at (customer quote, PO, procurement, CAP list, ILS, shop record).</summary>
    private async Task<HashSet<long>> UsedSupplierQuoteIdsAsync(List<long> ids)
    {
        var used = await _customerQuotes.UsedSupplierQuoteIdsAsync(ids);
        used.UnionWith(await _db.Set<POItem>().AsNoTracking()
            .Where(p => p.ProcumentId != null && ids.Contains(p.ProcumentId.Value))
            .Select(p => p.ProcumentId!.Value)
            .ToListAsync());
        used.UnionWith(await _db.Set<POItem>().AsNoTracking()
            .Where(p => p.SourceSupplierQuoteId != null && ids.Contains(p.SourceSupplierQuoteId.Value))
            .Select(p => p.SourceSupplierQuoteId!.Value)
            .ToListAsync());
        used.UnionWith(await _db.Set<ProcurementItem>().AsNoTracking()
            .Where(p => p.SourceProcumentRecordId != null && ids.Contains(p.SourceProcumentRecordId.Value))
            .Select(p => p.SourceProcumentRecordId!.Value)
            .ToListAsync());
        used.UnionWith(await _db.Set<ProcurementSupplierQuote>().AsNoTracking()
            .Where(p => p.SourceProcumentRecordId != null && ids.Contains(p.SourceProcumentRecordId.Value))
            .Select(p => p.SourceProcumentRecordId!.Value)
            .ToListAsync());
        used.UnionWith(await _db.Set<CapListItem>().AsNoTracking()
            .Where(p => p.ProcumentRecordId != null && ids.Contains(p.ProcumentRecordId.Value))
            .Select(p => p.ProcumentRecordId!.Value)
            .ToListAsync());
        used.UnionWith(await _db.Set<ILSItem>().AsNoTracking()
            .Where(p => p.ProcumentRecordId != null && ids.Contains(p.ProcumentRecordId.Value))
            .Select(p => p.ProcumentRecordId!.Value)
            .ToListAsync());
        used.UnionWith(await _db.Set<ProcumentRecord>().AsNoTracking()
            .Where(p => p.ParentProcumentId != null && ids.Contains(p.ParentProcumentId.Value))
            .Select(p => p.ParentProcumentId!.Value)
            .ToListAsync());
        return used;
    }

    /// <summary>
    /// Lines whose quotes a Procurement Plus edit took over and that own none any more: they stop writing their old values
    /// onto new RFQs (that would be the same supplier quote a second time).
    /// </summary>
    private async Task StopEmptyLinesAsync(List<long> lineIds)
    {
        if (lineIds.Count == 0) return;
        var stillOwning = (await _db.Set<ProcumentRecord>().AsNoTracking()
                .Where(q => q.SourcePendingQuoteId != null && lineIds.Contains(q.SourcePendingQuoteId.Value))
                .Select(q => q.SourcePendingQuoteId!.Value)
                .Distinct()
                .ToListAsync())
            .ToHashSet();
        var empty = lineIds.Where(id => !stillOwning.Contains(id)).ToList();
        if (empty.Count == 0) return;
        foreach (var line in await _db.Set<PendingSupplierQuote>().Where(l => empty.Contains(l.Id) && l.Source != null).ToListAsync())
            line.AutoApplyDisabled = true;
        await _db.SaveChangesAsync();
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
