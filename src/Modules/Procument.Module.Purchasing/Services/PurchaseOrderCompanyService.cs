using System.Data;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public interface IPurchaseOrderCompanyService
{
    /// <summary>Why the PO's buying company cannot be changed right now, or null when it can.</summary>
    Task<string?> GetChangeBlockedReasonAsync(PurchaseOrder po);

    /// <summary>
    /// Moves the PO to another of our companies before any money was recorded against it.
    /// Throws ArgumentException (shown to the user) or KeyNotFoundException.
    /// </summary>
    Task ChangeCompanyAsync(long poId, long companyPresetId, long? destinationWarehouseId);
}

/// <summary>
/// The buying company decides the PO/PR branding, the payment company and which supplier credit can
/// be used. It may change only while nothing has been paid — wallet payments or supplier credit —
/// and no wallet payment is in progress. Supplier Credits' suggestion uses the same rule.
/// </summary>
public sealed class PurchaseOrderCompanyService(DbContext db, IPaymentLedgerService ledger) : IPurchaseOrderCompanyService
{
    /// <summary>Statuses in which no payment can have been completed yet.</summary>
    private static readonly HashSet<string> PrePaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        PurchaseOrderStatusFlow.NotStarted,
        PurchaseOrderStatusFlow.WaitingForAdminApproval,
        PurchaseOrderStatusFlow.Sourcing,
        PurchaseOrderStatusFlow.WaitingForSupplierDocuments,
        PurchaseOrderStatusFlow.WaitingForPr,
        PurchaseOrderStatusFlow.WaitingForPayment,
        PurchaseOrderStatusFlow.PrRejected,
        "Draft",
    };

    public async Task<string?> GetChangeBlockedReasonAsync(PurchaseOrder po)
    {
        if (po.Status is PurchaseOrderStatusFlow.Cancelled or PurchaseOrderStatusFlow.Returned)
            return "The PO is cancelled or returned.";
        if (!PrePaymentStatuses.Contains(po.Status))
            return $"The PO has moved past payment ({po.Status}).";
        if (await db.Set<PaymentRequest>().AnyAsync(p => p.POId == po.Id && p.PendingUploadId != null))
            return "A wallet payment is in progress on this PO.";

        var prIds = await db.Set<PaymentRequest>().Where(p => p.POId == po.Id).Select(p => p.Id).ToListAsync();
        var paid = (await ledger.GetSupplierPaidAmountsAsync(prIds)).Values.Sum();
        return paid != 0 ? "Payments or supplier credit have already been recorded on this PO." : null;
    }

    public Task ChangeCompanyAsync(long poId, long companyPresetId, long? destinationWarehouseId) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            // Serializable, like the payment endpoints: a draft or credit application cannot slip in
            // between the "nothing paid" check and the switch.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var po = await db.Set<PurchaseOrder>().FirstOrDefaultAsync(p => p.Id == poId)
                ?? throw new KeyNotFoundException("Purchase order not found.");

            var preset = await db.Set<CompanyPreset>().AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == companyPresetId && p.IsActive)
                ?? throw new ArgumentException("Select one of our active companies.");
            if (po.CompanyPresetId == preset.Id && destinationWarehouseId is null) return;

            if (await GetChangeBlockedReasonAsync(po) is { } reason)
                throw new ArgumentException($"The company cannot be changed: {reason}");

            var warehouseId = destinationWarehouseId ?? po.DestinationWarehouseId;
            if (warehouseId.HasValue)
            {
                if (!await db.Set<Warehouse>().AnyAsync(w => w.Id == warehouseId.Value && w.IsActive))
                    throw new ArgumentException("The destination warehouse was not found or is inactive.");
                // Same rule as Stock POs: a company with warehouse links may only ship to its own warehouses.
                var hasLinks = await db.Set<CompanyPresetWarehouse>().AnyAsync(x => x.CompanyPresetId == preset.Id);
                if (hasLinks && !await db.Set<CompanyPresetWarehouse>()
                        .AnyAsync(x => x.CompanyPresetId == preset.Id && x.WarehouseId == warehouseId.Value))
                    throw new ArgumentException(destinationWarehouseId.HasValue
                        ? $"The destination warehouse is not linked to {preset.Name}."
                        : $"The current destination warehouse is not linked to {preset.Name}. Choose one of its warehouses.");
            }
            else if (po.Origin == "Stock")
            {
                throw new ArgumentException("A Stock PO needs a destination warehouse.");
            }

            po.CompanyPresetId = preset.Id;
            po.DestinationWarehouseId = warehouseId;

            // The legacy wallet preference is only a fallback for the company; drop it when it disagrees.
            if (po.PreferredWalletId.HasValue)
            {
                var walletCompany = await db.Database
                    .SqlQuery<long>($"SELECT CompanyPresetId AS [Value] FROM PaymentBoxes WHERE Id = {po.PreferredWalletId.Value}")
                    .FirstOrDefaultAsync();
                if (walletCompany != preset.Id) po.PreferredWalletId = null;
            }

            // Nothing is paid (checked above), so every PR of the PO still pays from the new company.
            var requests = await db.Set<PaymentRequest>().Where(p => p.POId == po.Id).ToListAsync();
            foreach (var request in requests) request.CompanyPresetId = preset.Id;

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
}
