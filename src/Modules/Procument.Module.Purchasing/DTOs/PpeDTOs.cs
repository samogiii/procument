namespace Procument.Module.Purchasing.DTOs;

// New Procurement Plus page (api/ppe): parts assigned to a user (the PPE) from Email RFQs.

/// <summary>One row of the Parts tab: a part assigned to the user, across every RFQ asking for it.</summary>
public class PpePartRow
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Alternatives { get; set; } = new();
    public double? Qty { get; set; }
    /// <summary>Condition asked for (the part's, then the RFQs').</summary>
    public string? Condition { get; set; }
    public List<string> Conditions { get; set; } = new();
    /// <summary>Average supplier quote price — for the asked condition when quoted in it, else every condition.</summary>
    public decimal? AvgPrice { get; set; }
    public string? AvgPriceCondition { get; set; }
    public List<PpPriceStat> PriceStats { get; set; } = new();
    /// <summary>RFQs asking for it whose deadline is at most 14 days ago (or later).</summary>
    public int RfqCount { get; set; }
    public DateTime? NearestDeadline { get; set; }
    /// <summary>Supplier lines added here without a price yet (warning rows).</summary>
    public int DraftCount { get; set; }
    /// <summary>Suppliers with a price for the part (on its RFQs, or priced here and not written yet).</summary>
    public int PricedCount { get; set; }
    /// <summary>Supplier quotes on those RFQs.</summary>
    public int QuoteCount { get; set; }
    public DateTime AssignedAt { get; set; }
    /// <summary>No open RFQ and assigned more than 14 days ago — listed only with "Show older".</summary>
    public bool Old { get; set; }
    /// <summary>My folders the part is in (none = only the public folder, where every part is listed).</summary>
    public List<long> FolderIds { get; set; } = new();
}

/// <summary>A user's own folder of parts (Parts tab chips).</summary>
public class PpeFolder
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Parts in it on the list (Show older applies).</summary>
    public int PartCount { get; set; }
}

public class PpeFolderRequest
{
    public string? Name { get; set; }
}

/// <summary>Parts into (Add) / out of (Remove) my folders — a part can be in several.</summary>
public class PpeSetFoldersRequest
{
    public List<long> Ids { get; set; } = new();
    public List<long> Add { get; set; } = new();
    public List<long> Remove { get; set; } = new();
}

/// <summary>
/// Add part by hand: a catalog part (PartNumberId) or a typed P/N (linked to the catalog when it is there).
/// It is the user's own PPE part, worked like an assigned one.
/// </summary>
public class PpeAddPartRequest
{
    public long? PartNumberId { get; set; }
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public List<string>? Alternatives { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    /// <summary>My folders to put it in (none = public folder only).</summary>
    public List<long>? FolderIds { get; set; }
}

public class PpeAddPartResponse
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    /// <summary>Already on my list (shown) — only qty / condition / alternatives were updated.</summary>
    public bool AlreadyListed { get; set; }
    /// <summary>It was removed from my list and is back.</summary>
    public bool Restored { get; set; }
}

/// <summary>Bulk import: pasted rows (Part Number, Qty, Condition).</summary>
public class PpeBulkAddPartsRequest
{
    public List<PpeAddPartRequest> Items { get; set; } = new();
    /// <summary>My folders every imported part goes into (none = public folder only).</summary>
    public List<long>? FolderIds { get; set; }
}

public class PpeBulkAddPartsResponse
{
    public int Added { get; set; }
    /// <summary>Were removed from my list and are back.</summary>
    public int Restored { get; set; }
    /// <summary>On my list already — qty / condition updated.</summary>
    public int AlreadyListed { get; set; }
}

/// <summary>Add part dialog: a part from the database (PartNumbers).</summary>
public class PpeCatalogPart
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Alternatives { get; set; } = new();
    /// <summary>Already on my Parts list.</summary>
    public bool AlreadyAdded { get; set; }
}

public class PpePartPage
{
    public List<PpePartRow> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int OldCount { get; set; }
    /// <summary>My folders, with their part counts (Show older applies, search does not).</summary>
    public List<PpeFolder> Folders { get; set; } = new();
    /// <summary>Parts in the public folder = every part on the list (Show older applies, search does not).</summary>
    public int PublicCount { get; set; }
}

/// <summary>An RFQ asking for the part (deadline at most 14 days ago).</summary>
public class PpeRfqRef
{
    public long RfqId { get; set; }
    public string RfqName { get; set; } = string.Empty;
    public long RfqItemId { get; set; }
    public DateTime Deadline { get; set; }
    public long CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public int? CustomerBase { get; set; }
    /// <summary>Customer's minimum commission coef with a Trusted supplier (null = none → at least 1.13).</summary>
    public decimal? MinCoef { get; set; }
    /// <summary>The P/N on the RFQ item (the part or one of its alternatives).</summary>
    public string PartNumber { get; set; } = string.Empty;
    public double Qty { get; set; }
    public string? Condition { get; set; }
    /// <summary>Set on a quote row's chips: the supplier quote on that RFQ item, with its own price / qty / coef.</summary>
    public long? QuoteId { get; set; }
    public decimal? QuotePrice { get; set; }
    public double? QuoteQty { get; set; }
    public double? QuoteShippingCost { get; set; }
    public double? Coef1 { get; set; }
    /// <summary>Selling unit price saved on the quote (typed by hand on the commission / Add Quote page), if any.</summary>
    public double? QuoteUnitPrice { get; set; }
    /// <summary>A commission was saved for this quote (Coef 1 locked on the Add Quote page).</summary>
    public DateTime? CommissionAt { get; set; }
}

public static class PpeRowKinds
{
    /// <summary>A supplier line added here, not on the RFQs yet (no price, or not a catalog supplier).</summary>
    public const string Draft = "Draft";
    /// <summary>A supplier quote on the part's RFQs (one row per supplier quote across RFQs).</summary>
    public const string Quote = "Quote";
    /// <summary>An accepted Email Quotes line — edited here too (the line, and every RFQ it wrote on).</summary>
    public const string Email = "Email";
}

/// <summary>A supplier row under a part. Field names follow the RFQ supplier quote so SupplierQuoteEditDialog edits it.</summary>
public class PpeQuoteRow
{
    public string Key { get; set; } = string.Empty;
    public string Kind { get; set; } = PpeRowKinds.Quote;
    public bool ReadOnly { get; set; }
    /// <summary>The Procurement Plus line behind the row (drafts, and quotes written from here).</summary>
    public long? LineId { get; set; }
    /// <summary>Email rows on none of the part's RFQs: the accepted Email Quotes line behind it (edits go to it).</summary>
    public long? EmailLineId { get; set; }
    /// <summary>The supplier quotes the row stands for (one per RFQ); the first is the newest.</summary>
    public List<long> QuoteIds { get; set; } = new();
    public List<PpeRfqRef> Rfqs { get; set; } = new();
    /// <summary>Drafts: why it is not on the RFQs yet.</summary>
    public string? Reason { get; set; }
    /// <summary>Drafts from an ILS file: the price the file listed (a hint — the user types the price).</summary>
    public string? FilePrice { get; set; }
    public string? FileName { get; set; }
    public List<string> Methods { get; set; } = new();
    public DateTime UpdatedAt { get; set; }

    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierStatus { get; set; }
    /// <summary>Trusted supplier — commission may use the customer's MinCoef.</summary>
    public bool SupplierTrusted { get; set; }
    public string? SupplierEmail { get; set; }
    public decimal? Price { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public string? Unit { get; set; }
    public string? LeadTime { get; set; }
    public string? SellLeadTime { get; set; }
    public string? CertName { get; set; }
    public DateOnly? TagDate { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    public string? Note { get; set; }
    public string? MyNotes { get; set; }
    public List<string> CustomCertReferences { get; set; } = new();
    public string? SerialNumber { get; set; }
    public string? TraceTo { get; set; }
    public string? TagType { get; set; }
    public string? PriceType { get; set; }
    public string? TaggedBy { get; set; }
    public string? MoQ { get; set; }
    public string? Warranty { get; set; }
    public string? CR { get; set; }
}

public class PpePartDetail
{
    public PpePartRow Part { get; set; } = new();
    public List<PpeRfqRef> Rfqs { get; set; } = new();
    public List<PpeQuoteRow> Rows { get; set; } = new();
}

/// <summary>
/// Save from SupplierQuoteEditDialog: the RFQ supplier quote payload plus which row it was opened on —
/// LineId (a draft / a row written from here), QuoteIds (a quote row typed elsewhere), or neither (Add Supplier).
/// </summary>
public class PpeSaveQuoteRequest : SaveSupplierQuoteRequest
{
    public long? LineId { get; set; }
    public List<long> QuoteIds { get; set; } = new();
    /// <summary>An Email row on no RFQ: the accepted Email Quotes line to edit.</summary>
    public long? EmailLineId { get; set; }
}

public class PpeSaveQuoteResponse
{
    public long LineId { get; set; }
    /// <summary>On the RFQs now (price + catalog supplier).</summary>
    public bool Written { get; set; }
    public int QuotesWritten { get; set; }
    /// <summary>Not written — why (missing price, supplier not in the catalog / not approved, no open RFQ).</summary>
    public string? Message { get; set; }
    /// <summary>A supplier quote the line owns (its id + RFQ), for cert PDF uploads from the dialog.</summary>
    public long? QuoteId { get; set; }
    public long? RfqId { get; set; }
}

/// <summary>ILS rows picked (Available suppliers / ILS tab) → draft lines on the parts they answer (PartId = that part only).</summary>
public class PpeAddOffersRequest
{
    public List<long> OfferIds { get; set; } = new();
    public long? PartId { get; set; }
    public List<string>? Methods { get; set; }
}

public class PpeAddOffersResponse
{
    public int Added { get; set; }
    /// <summary>Already there (the supplier has a line without a price on the part).</summary>
    public int Existing { get; set; }
}

/// <summary>Add Commission: one coef per supplier per customer, for that supplier's quotes on that customer's RFQs.</summary>
public class PpeCommissionRequest
{
    public List<PpeCommissionItem> Items { get; set; } = new();
}

public class PpeCommissionItem
{
    public List<long> QuoteIds { get; set; } = new();
    /// <summary>Written as Coef 1 (the only coef here; Coef 2 / 3 stay for the Add Quote page).</summary>
    public double Coef { get; set; }
    /// <summary>Unit price typed by hand (kept as typed, the coef is not changed); null = from the coef.</summary>
    public double? UnitPrice { get; set; }
    /// <summary>Shipping cost typed per RFQ quote in the dialog (null = none); quotes not listed keep theirs.</summary>
    public List<PpeCommissionShipping> Shipping { get; set; } = new();
}

public class PpeCommissionShipping
{
    public long QuoteId { get; set; }
    public double? ShippingCost { get; set; }
}

public class PpeCommissionResponse
{
    public int QuotesUpdated { get; set; }
    /// <summary>The part left the list (commission saved).</summary>
    public bool PartRemoved { get; set; }
}

public class PpeIdsRequest
{
    public List<long> Ids { get; set; } = new();
}

/// <summary>ILS tab: the user's current files and every row answering one of their parts.</summary>
public class PpeIlsResponse
{
    public int FreshDays { get; set; }
    public List<PpUploadResponse> Uploads { get; set; } = new();
    public List<PpOfferRow> Rows { get; set; } = new();
    /// <summary>
    /// The user's preferred suppliers (the same list as ProMax) for the Preferred suppliers tab — each shows the
    /// rows of <see cref="Rows"/> it matched (<see cref="PpOfferRow.PreferredId"/>), or none.
    /// </summary>
    public List<PpPreferredSupplierResponse> Preferred { get; set; } = new();
    /// <summary>Lines already on the parts (draft or Done), so picked rows show as added.</summary>
    public List<PpeLineHead> Lines { get; set; } = new();
    /// <summary>The listed parts (description, qty and condition asked) — for the supplier quotation mail.</summary>
    public List<PpePartBrief> Parts { get; set; } = new();
}

public class PpePartBrief
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    /// <summary>Every condition asked (the part's and its RFQs') — bold on the suppliers' chips.</summary>
    public List<string> Conditions { get; set; } = new();
}

/// <summary>Enough of a line to tell whether a file row is already on the part (offerAdded on the client).</summary>
public class PpeLineHead
{
    public long Id { get; set; }
    public long PartId { get; set; }
    public long? SupplierId { get; set; }
    public long? PreferredId { get; set; }
    public string? SupplierNameRaw { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}

public class PpeOffersResponse
{
    public List<PpOfferRow> Rows { get; set; } = new();
    public List<PpeLineHead> Lines { get; set; } = new();
}
