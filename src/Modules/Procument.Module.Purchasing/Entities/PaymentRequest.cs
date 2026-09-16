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
        public virtual PurchaseOrder? PO { get; set; }
        public virtual CompanyPreset? CompanyPreset { get; set; }
    }
}
