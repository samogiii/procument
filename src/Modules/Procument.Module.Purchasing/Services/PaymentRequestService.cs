using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Identity.Entities;

namespace Procument.Module.Purchasing.Services;

public class PaymentRequestService : IPaymentRequestService
{
    private readonly DbContext _db;

    public PaymentRequestService(DbContext db)
    {
        _db = db;
    }

    public async Task<PaymentRequestResponse> GetByIdAsync(long id)
    {
        var pr = await _db.Set<PaymentRequest>()
            .Include(x => x.PO)
            .ThenInclude(x => x.Supplier)
            .Include(x => x.PO)
            .ThenInclude(x => x.ImportDetail)
            .Include(x => x.PO)
            .ThenInclude(x => x.POItems)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (pr == null) throw new Exception("Payment Request not found");

        return await MapToResponse(pr);
    }

    public async Task<List<PaymentRequestResponse>> GetAllAsync()
    {
        var prs = await _db.Set<PaymentRequest>()
            .Include(x => x.PO)
            .ThenInclude(x => x.Supplier)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var responses = new List<PaymentRequestResponse>();
        foreach (var pr in prs)
        {
            responses.Add(await MapToResponse(pr));
        }
        return responses;
    }

    public async Task<PaymentRequestResponse> CreateAsync(long poId)
    {
        // Logic: if already exists for this PO, return it? Or create new?
        // Usually, one PO has one PR.
        var existing = await _db.Set<PaymentRequest>().FirstOrDefaultAsync(x => x.POId == poId);
        if (existing != null) return await MapToResponse(existing);

        // Get max PRId for numbering
        var maxPrId = await _db.Set<PaymentRequest>().MaxAsync(x => (long?)x.PRId) ?? 1500;
        
        var pr = new PaymentRequest
        {
            POId = poId,
            PRId = maxPrId + 1,
            Status = "PENDING APPROVAL",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<PaymentRequest>().Add(pr);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(pr.Id);
    }

    public async Task<bool> UpdateStatusAsync(long id, string status)
    {
        var pr = await _db.Set<PaymentRequest>().FindAsync(id);
        if (pr == null) return false;

        pr.Status = status;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var pr = await _db.Set<PaymentRequest>().FindAsync(id);
        if (pr == null) return false;

        _db.Set<PaymentRequest>().Remove(pr);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PaymentRequestResponse?> GetByPoIdAsync(long poId)
    {
        var pr = await _db.Set<PaymentRequest>().FirstOrDefaultAsync(x => x.POId == poId);
        if (pr == null) return null;
        return await MapToResponse(pr);
    }

    private async Task<PaymentRequestResponse> MapToResponse(PaymentRequest pr)
    {
        var po = pr.PO;
        if (po == null)
        {
            po = await _db.Set<PurchaseOrder>()
                .Include(x => x.Supplier)
                .Include(x => x.ImportDetail)
                .Include(x => x.POItems)
                .FirstOrDefaultAsync(x => x.Id == pr.POId);
        }

        var response = new PaymentRequestResponse
        {
            Id = pr.Id,
            PrNumber = pr.PRId,
            Status = pr.Status,
            POId = pr.POId,
            PONumber = po?.PONumber,
            CreatedAt = pr.CreatedAt,
            SupplierName = po?.Supplier?.Name,
            WireFee = po?.ImportDetail?.Wirefee ?? 0
        };

        if (po != null)
        {
            // Company Paying From: depends on Customer Base. 
            // I need to find the Customer related to this PO.
            // PO -> POItems -> ProcumentRecord -> RFQItem -> RFQ -> Customer
            var customer = await (from p in _db.Set<PurchaseOrder>()
                                  where p.Id == po.Id
                                  join pi in _db.Set<POItem>() on p.Id equals pi.POId
                                  join prrec in _db.Set<ProcumentRecord>() on pi.ProcumentId equals prrec.Id
                                  join ri in _db.Set<RFQItem>() on prrec.RFQItemId equals ri.Id
                                  join r in _db.Set<RFQHeader>() on ri.RFQId equals r.Id
                                  join c in _db.Set<Customer>() on r.CustomerId equals c.Id
                                  select c).FirstOrDefaultAsync();

            var preset = await _db.Set<CompanyPreset>()
                .OrderBy(p => p.SortOrder)
                .FirstOrDefaultAsync(p => p.SortOrder == 105)
                ?? await _db.Set<CompanyPreset>().OrderBy(p => p.SortOrder).FirstOrDefaultAsync();

            response.CompanyPayingFrom = preset?.Name ?? "JETRUX";

            response.CompanyPayingTo = po.Supplier?.Name;
            response.AccountNumber = po.ImportDetail?.BankAccountNumber;
            response.BankName = po.ImportDetail?.BankName;
            response.SwiftCode = po.ImportDetail?.SwiftCode;
            response.ABA = po.ImportDetail?.ABA;
            response.CompanyAddress = po.Supplier?.Address;
            response.BankAddress = po.ImportDetail?.BankAddress;

            response.ItemsTotal = po.POItems.Sum(x => x.TotalPrice);
            }

        return response;
    }
}
