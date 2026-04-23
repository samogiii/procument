using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IAvailabilityService
{
    Task<List<PartAvailabilityResponse>> GetPartAvailabilityAsync(List<long> partNumberIds);
}

public class AvailabilityService : IAvailabilityService
{
    private readonly DbContext _db;

    public AvailabilityService(DbContext db) => _db = db;

    public async Task<List<PartAvailabilityResponse>> GetPartAvailabilityAsync(List<long> partNumberIds)
    {
        if (partNumberIds == null || partNumberIds.Count == 0)
            return new List<PartAvailabilityResponse>();

        // Inventory: actual records with company name
        var inventoryItems = await _db.Set<InventoryItem>()
            .Where(i => partNumberIds.Contains(i.PartNumberId))
            .Include(i => i.Company)
            .ToListAsync();

        // CapList: actual records with company name
        var capListItems = await _db.Set<CapListItem>()
            .Where(c => partNumberIds.Contains(c.PartNumberId))
            .Include(c => c.Company)
            .ToListAsync();

        // ILS: actual records (use part number as label since ILS has no supplier field)
        var ilsItems = await _db.Set<ILSItem>()
            .Where(i => partNumberIds.Contains(i.PartNumberId))
            .ToListAsync();

        // Fast Import: procurement records (non-Shop) with supplier name
        var fastImportItems = await _db.Set<ProcumentRecord>()
            .Where(r => (r.Type ?? "Procument") != "Shop" && partNumberIds.Contains(r.RFQItem.PartNumberId))
            .Include(r => r.Supplier)
            .Include(r => r.RFQItem)
            .ToListAsync();

        // Known Suppliers via PartNumberSupplier join table
        var knownSupplierItems = await _db.Set<PartNumberSupplier>()
            .Where(ps => partNumberIds.Contains(ps.PartNumberId))
            .Include(ps => ps.Supplier)
            .ToListAsync();

        return partNumberIds.Select(pid => new PartAvailabilityResponse
        {
            PartNumberId = pid,

            InventoryRecords = inventoryItems
                .Where(i => i.PartNumberId == pid)
                .Select(i => new AvailabilityRecord
                {
                    Label = i.Company?.Name ?? "Inventory",
                    Price = i.Price,
                    Qty = i.Qty,
                    Condition = i.Condition,
                })
                .ToList(),

            CapListRecords = capListItems
                .Where(c => c.PartNumberId == pid)
                .Select(c => new AvailabilityRecord
                {
                    Label = c.Company?.Name ?? "Cap List",
                    Condition = null,
                })
                .ToList(),

            ILSRecords = ilsItems
                .Where(i => i.PartNumberId == pid)
                .Select(i => new AvailabilityRecord
                {
                    Label = "ILS",
                    Price = i.Price,
                    Qty = i.Qty,
                    Condition = i.Condition,
                    CertName = i.CertName,
                    LeadTime = i.LeadTime,
                    AltPartNumber = i.AltPartNumber,
                    TagDate = i.TagDate?.ToString("yyyy-MM-dd"),
                })
                .ToList(),

            FastImportRecords = fastImportItems
                .Where(r => r.RFQItem?.PartNumberId == pid)
                // deduplicate by supplier name, take latest per supplier
                .GroupBy(r => r.Supplier?.Name ?? "")
                .Select(g => g.OrderByDescending(r => r.Id).First())
                .Select(r => new AvailabilityRecord
                {
                    Label = r.Supplier?.Name ?? "Unknown",
                    Price = r.Price,
                    Qty = r.Qty,
                    Condition = r.Condition,
                    CertName = r.CertName,
                    LeadTime = r.LeadTime,
                    AltPartNumber = r.Alt,
                    TagDate = r.TagDate?.ToString("yyyy-MM-dd"),
                })
                .ToList(),

            KnownSupplierRecords = knownSupplierItems
                .Where(ps => ps.PartNumberId == pid)
                .Select(ps => new AvailabilityRecord
                {
                    Label = ps.Supplier?.Name ?? "Unknown",
                })
                .ToList(),
        }).ToList();
    }
}
