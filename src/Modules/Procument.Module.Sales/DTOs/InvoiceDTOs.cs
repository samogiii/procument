using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.DTOs;

public class CreateInvoiceRequest
{
    public long QuoteId { get; set; }
    public DateTime? DueDate { get; set; }
    public List<CreateInvoiceItemRequest> Items { get; set; } = new();
}

public class CreateInvoiceItemRequest
{
    public long QuoteItemId { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

public class UpdateInvoiceStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class InvoiceResponse
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public long QuoteId { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public List<InvoiceItemResponse> Items { get; set; } = new();
}

public class InvoiceItemResponse
{
    public long Id { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public long? QuoteItemId { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
