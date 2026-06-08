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
        public virtual PurchaseOrder? PO { get; set; }
        public virtual CompanyPreset? CompanyPreset { get; set; }
    }
}
