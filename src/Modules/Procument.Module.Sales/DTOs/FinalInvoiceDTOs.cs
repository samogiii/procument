namespace Procument.Module.Sales.DTOs;

public class FinalInvoiceResponse
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    /// <summary>Base 1 invoice number inherited from the proforma (e.g. "I101-60701-10"); null for every other base. Stored as FinalInvoices.B1FinalInvoiceNumber.</summary>
    public string? B1InvoiceNumber { get; set; }
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
    public string? B1ProformaInvoiceNumber { get; set; }
    public string? CustomerPONumber { get; set; } = null;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int? CustomerBase { get; set; }
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
    public string? CustomerTermsAndConditions { get; set; }
    public string? CustomerCurrencyType { get; set; }
    public string? CustomerContacts { get; set; }
    public long? DefaultDepositWalletId { get; set; }
    public long? DefaultBankAccountId { get; set; }
    // Yuan pricing settings from the source Quote
    public decimal? QuoteCoefYuan { get; set; }
    public decimal? QuoteExchangeRateYuan { get; set; }
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
    /// <summary>Alt part number from the source QuoteItem. When set, this is the effective part number shown to the customer.</summary>
    public string? Alt { get; set; }
    /// <summary>Effective part number for display: Alt if set, otherwise PartNumberName.</summary>
    public string EffectivePartNumber => !string.IsNullOrWhiteSpace(Alt) ? Alt : PartNumberName;
    public string? Description { get; set; }
}

public class CreateFinalInvoiceRequest
{
    public long ProformaInvoiceId { get; set; }
    /// <summary>
    /// Sales Order lines to put on this Final Invoice. Each must have a track received in the
    /// warehouse (or further along) and not be on another Final Invoice yet.
    /// </summary>
    public List<long> InvoiceItemIds { get; set; } = new();
}

/// <summary>One Sales Order line as offered in the Create Final Invoice part picker.</summary>
public class FinalInvoiceCandidateItem
{
    public long InvoiceItemId { get; set; }
    public string PartNumber { get; set; } = "";
    public string? Alt { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    /// <summary>Sales Order line status (mirrors the PO line).</summary>
    public string Status { get; set; } = "";
    /// <summary>Track numbers of the live PO line received in the warehouse (or further along).</summary>
    public List<string> FinishedTrackNumbers { get; set; } = new();
    /// <summary>Tracks still on the way — shown so the user knows why a part is not selectable yet.</summary>
    public List<string> OpenTrackNumbers { get; set; } = new();
    /// <summary>Final Invoice already holding this line, if any.</summary>
    public long? FinalInvoiceId { get; set; }
    public string? FinalInvoiceNumber { get; set; }
    public bool Eligible { get; set; }
    /// <summary>Why the line can't be selected; null when <see cref="Eligible"/>.</summary>
    public string? Reason { get; set; }
}

/// <summary>A Sales Order line that sits on a Final Invoice — offered in the Unlock parts dialog.</summary>
public class InvoicedPartResponse
{
    public long InvoiceItemId { get; set; }
    public long FinalInvoiceItemId { get; set; }
    public string PartNumber { get; set; } = "";
    public int Qty { get; set; }
    public decimal TotalPrice { get; set; }
    public string? TrackNumber { get; set; }
    public long FinalInvoiceId { get; set; }
    public string FinalInvoiceNumber { get; set; } = "";
    public string? B1FinalInvoiceNumber { get; set; }
    public string FinalInvoiceStatus { get; set; } = "";
    /// <summary>How many parts the Final Invoice holds in total, so the UI can warn when removing the last ones deletes it.</summary>
    public int FinalInvoiceItemCount { get; set; }
}

public class ReleaseFinalInvoicePartsRequest
{
    /// <summary>Sales Order lines to take off their Final Invoice.</summary>
    public List<long> InvoiceItemIds { get; set; } = new();
}

public class ReleaseFinalInvoicePartsResponse
{
    public int ReleasedCount { get; set; }
    /// <summary>Final Invoices that kept other parts (total recalculated).</summary>
    public List<string> UpdatedFinalInvoices { get; set; } = new();
    /// <summary>Final Invoices deleted because no part was left on them.</summary>
    public List<string> DeletedFinalInvoices { get; set; } = new();
}

public class FinalInvoiceCandidatesResponse
{
    public long ProformaInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string? B1ProformaInvoiceNumber { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerCode { get; set; }
    public List<FinalInvoiceCandidateItem> Items { get; set; } = new();
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
    public string? B1ProformaInvoiceNumber { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerCode { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>Lines that can go on a Final Invoice right now (finished track, not invoiced yet).</summary>
    public int EligibleItemCount { get; set; }
    /// <summary>Lines already on a Final Invoice.</summary>
    public int InvoicedItemCount { get; set; }
    /// <summary>All live (non-cancelled) lines.</summary>
    public int TotalItemCount { get; set; }
}

/// <summary>
/// Lightweight row used by the Final Invoices list page. Skips line items and the
/// deep RFQ-traversal that the detail DTO needs — list queries stay a single flat SELECT.
/// </summary>
/// <summary>
/// Ids of the final invoices whose parts go onto one merged packing list.
/// They must all belong to the same customer — a packing list ships to a single destination.
/// </summary>
public class MergedPackingListDataRequest
{
    public List<long> InvoiceIds { get; set; } = new();
}

public class FinalInvoiceListItem
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string? B1InvoiceNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public long ProformaInvoiceId { get; set; }
    public string ProformaInvoiceNumber { get; set; } = "";
    public string? B1ProformaInvoiceNumber { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerCode { get; set; }
    public int ItemCount { get; set; }
}
