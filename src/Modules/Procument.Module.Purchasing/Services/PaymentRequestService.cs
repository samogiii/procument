using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Identity.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public class PaymentRequestService : IPaymentRequestService
{
    private readonly DbContext _db;
    private readonly IPaymentLedgerService _ledger;

    public PaymentRequestService(DbContext db, IPaymentLedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
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
            .Include(x => x.PO).ThenInclude(x => x.ImportDetail)
            .Include(x => x.PO).ThenInclude(x => x.POItems)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var responses = new List<PaymentRequestResponse>();
        foreach (var pr in prs)
        {
            responses.Add(await MapToResponse(pr));
        }
        return responses;
    }

    public async Task<PaymentRequestResponse> CreateAsync(long poId, long? companyPresetId = null, decimal? amount = null)
    {
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var po = await _db.Set<PurchaseOrder>().Include(p => p.ImportDetail).FirstOrDefaultAsync(p => p.Id == poId)
            ?? throw new ArgumentException("Purchase order not found.");
        var total = SupplierPaymentAmounts.Total(po);
        var allocated = await _db.Set<PaymentRequest>().Where(p => p.POId == poId)
            .SumAsync(p => p.Amount ?? total);
        var requested = amount ?? (total - allocated);
        SupplierPaymentAmounts.ValidateRequest(requested, 0, total - allocated);

        var maxPrId = await _db.Set<PaymentRequest>().MaxAsync(x => (long?)x.PRId) ?? 1500;

        var pr = new PaymentRequest
        {
            POId = poId,
            PRId = maxPrId + 1,
            Status = "PENDING APPROVAL",
            CompanyPresetId = companyPresetId,
            Amount = requested,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<PaymentRequest>().Add(pr);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await GetByIdAsync(pr.Id);
        });
    }

    public async Task<PaymentRequestResponse> UpdateAmountAsync(long id, decimal amount)
    {
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var pr = await _db.Set<PaymentRequest>().Include(p => p.PO).ThenInclude(p => p.ImportDetail)
            .FirstOrDefaultAsync(p => p.Id == id) ?? throw new ArgumentException("Payment request not found.");
        var total = SupplierPaymentAmounts.Total(pr.PO!);
        var allocated = await _db.Set<PaymentRequest>().Where(p => p.POId == pr.POId && p.Id != id)
            .SumAsync(p => p.Amount ?? total);
        var paid = (await _ledger.GetSupplierPaidAmountsAsync(new[] { id })).GetValueOrDefault(id);
        SupplierPaymentAmounts.ValidateRequest(amount, paid, total - allocated);
        pr.Amount = amount;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await GetByIdAsync(id);
        });
    }

    public async Task<bool> UpdateStatusAsync(long id, string status)
    {
        var pr = await _db.Set<PaymentRequest>().Include(p => p.PO).FirstOrDefaultAsync(p => p.Id == id);
        if (pr == null) return false;

        pr.Status = status;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var pr = await _db.Set<PaymentRequest>().Include(p => p.PO).FirstOrDefaultAsync(p => p.Id == id);
        if (pr == null) return false;

        if ((await _ledger.GetSupplierPaidAmountsAsync(new[] { id })).GetValueOrDefault(id) > 0)
            throw new ArgumentException("A payment request with recorded payments cannot be deleted.");
        _db.Set<PaymentRequest>().Remove(pr);
        if (pr.PO != null && pr.PO.Status == PurchaseOrderStatusFlow.PrRejected)
        {
            pr.PO.PaymentApproval = "Pending";
            pr.PO.PaymentApprovalNote = null;
            await PurchaseOrderStatusFlow.ApplyAsync(_db, pr.PO, PurchaseOrderStatusFlow.WaitingForPr);
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PaymentRequestResponse?> GetByPoIdAsync(long poId)
    {
        var pr = await _db.Set<PaymentRequest>().OrderByDescending(x => x.Id).FirstOrDefaultAsync(x => x.POId == poId);
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

        CompanyPreset? preset = null;
        if (pr.CompanyPresetId.HasValue)
            preset = await _db.Set<CompanyPreset>().FindAsync(pr.CompanyPresetId.Value);

        var response = new PaymentRequestResponse
        {
            Id = pr.Id,
            PrNumber = pr.PRId,
            Status = pr.Status,
            POId = pr.POId,
            PONumber = po?.PONumber,
            CreatedAt = pr.CreatedAt,
            SupplierId = po?.SupplierId,
            SupplierName = po?.Supplier?.Name,
            WireFee = po?.ImportDetail?.Wirefee ?? 0,
            CompanyPresetId = pr.CompanyPresetId,
            CompanyPayingFrom = preset?.Name,
            PendingWalletId = pr.PendingWalletId,
            PendingPaymentAmount = pr.PendingPaymentAmount,
            PendingExchangeRate = pr.PendingExchangeRate,
            PendingUploadId = pr.PendingUploadId,
        };

        if (po != null)
        {
            response.POTotalAmount = SupplierPaymentAmounts.Total(po);
            response.Amount = pr.Amount ?? response.POTotalAmount;
            response.PaidAmount = (await _ledger.GetSupplierPaidAmountsAsync(new[] { pr.Id })).GetValueOrDefault(pr.Id);
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
