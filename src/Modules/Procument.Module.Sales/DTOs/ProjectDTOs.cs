namespace Procument.Module.Sales.DTOs;

public class ProjectItemResponse
{
    public long InvoiceItemId { get; set; }
    public long InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCode { get; set; }
    public string? PartNumberName { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }      // from ProcurementItem if available, else InvoiceItem
    public string? SupplierName { get; set; }   // from ProcurementItem.CurrentSupplier
    public long? POId { get; set; }
    public string? PONumber { get; set; }
    public string Status { get; set; } = "Not Started";
    public DateTime? InvoiceCreatedAt { get; set; }
}
