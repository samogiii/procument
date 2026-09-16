using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public static class SupplierPaymentAmounts
{
    public static decimal Total(PurchaseOrder po) => (po.TotalAmount ?? 0)
        + (po.ProcessingFee ?? 0) + (po.Shipping ?? 0) + (po.Tax ?? 0)
        + (po.ImportDetail?.Wirefee ?? 0);

    public static void ValidateRequest(decimal amount, decimal paid, decimal available)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Enter a positive payment amount with at most two decimal places.");
        if (amount < paid) throw new ArgumentException("The amount cannot be less than the amount already paid.");
        if (amount > available) throw new ArgumentException("The payment request exceeds the unallocated PO amount.");
    }
}
