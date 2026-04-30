using Procument.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Procument.Module.Purchasing.Entities
{
    public class PaymentRequest: AuditableEntity
    {
        public long? PRId { get; set; }
        public string? Status { get; set; }
        public long? POId { get; set; }
        public virtual PurchaseOrder? PO { get; set; }
    }
}
