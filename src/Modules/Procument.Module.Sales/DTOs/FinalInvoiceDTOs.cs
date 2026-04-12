namespace Procument.Module.Sales.DTOs;

public class FinalInvoiceResponse
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public string? ShippingMethod { get; set; }
    public decimal? ShippingCost { get; set; }
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public long ProformaInvoiceId { get; set; }
    public string ProformaInvoiceNumber { get; set; } = "";
    public string? CustomerPONumber { get; set; } = null;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerCode { get; set; }
    public string? CustomerContactPerson { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerBillToEmail { get; set; }
    public string? CustomerBillToPhone { get; set; }
    public string? CustomerShipTo { get; set; }
    public string? CustomerShipToContactPerson { get; set; }
    public string? CustomerBillToContactPerson { get; set; }
    public string? CustomerShipToEmail { get; set; }
    public string? CustomerShipToPhone { get; set; }
    public string? CustomerShipToAccount { get; set; }
    public List<FinalInvoiceItemResponse> Items { get; set; } = new();
}

public class FinalInvoiceItemResponse
{
    public long Id { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? Discount { get; set; }
    public decimal FinalPrice => Discount.HasValue ? TotalPrice - Discount.Value : TotalPrice;
    public string? Condition { get; set; }
    public string? CertName { get; set; }
    public string? TrackNumber { get; set; }
    public string? Carrier { get; set; }
    public long? PartNumberId { get; set; }
    public string? RFQReference { get; set; }
    public string PartNumberName { get; set; } = "";
    public string? Description { get; set; }
}

public class CreateFinalInvoiceRequest
{
    public long ProformaInvoiceId { get; set; }
}

public class UpdateFinalInvoiceStatusRequest
{
    public string Status { get; set; } = "";
    public string? RejectionNote { get; set; }
}

public class UpdateFinalInvoiceRequest
{
    public decimal? ShippingCost { get; set; }
    public string? ShippingMethod { get; set; }
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
}

public class EligibleProformaResponse
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string? CustomerCode { get; set; }
    public decimal TotalAmount { get; set; }
}
