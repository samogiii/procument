using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities
{
    public class PaymentRequest : AuditableEntity
    {
        public long? PRId { get; set; }
        public string? Status { get; set; }
        public long? POId { get; set; }
        public long? CompanyPresetId { get; set; }
        public decimal? Amount { get; set; }
        // Saved supplier-payment details. These remain pending until a POP is
        // uploaded and the bank's final withdrawn amount is confirmed.
        public long? PendingWalletId { get; set; }
        public decimal? PendingPaymentAmount { get; set; }
        public decimal? PendingExchangeRate { get; set; }
        public Guid? PendingUploadId { get; set; }
        // Paid from the warehouse app (synced): the POP file (in "Our POP to Supplier"), the rate,
        // the bank's handling and wire fees (wallet currency) and the final amount
        // (= amount × rate + fees). Waits here until a Payment user accepts or rejects it.
        public string? WarehousePopFileName { get; set; }
        public decimal? WarehouseHandlingFee { get; set; }
        public decimal? WarehouseBankWireFee { get; set; }
        public decimal? WarehouseFinalAmount { get; set; }
        public decimal? WarehouseExchangeRate { get; set; }
        public string? WarehousePaidBy { get; set; }
        public DateTime? WarehousePaidAt { get; set; }
        /// <summary>Why the office sent the last warehouse payment back; shown on the phone.</summary>
        public string? WarehouseRejectNote { get; set; }
        /// <summary>
        /// Set on a credit-line repayment (then POId is null): the Supplier Credits account being paid
        /// back. A plain id — the account lives in the Sales module; the FK is configured in AppDbContext.
        /// </summary>
        public long? SupplierCreditAccountId { get; set; }
        public virtual PurchaseOrder? PO { get; set; }
        public virtual CompanyPreset? CompanyPreset { get; set; }
    }
}
