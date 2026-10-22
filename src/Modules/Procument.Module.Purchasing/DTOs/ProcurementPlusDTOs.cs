namespace Procument.Module.Purchasing.DTOs;

// ──── Part picking ────

public class PpRfqOption
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public string? CustomerCode { get; set; }
    public int ItemCount { get; set; }
    // By RFQ summary box only (coverage): more about the RFQ.
    public string? CustomerName { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? OwnerName { get; set; }
    public string? SenderMail { get; set; }
    public string? Notes { get; set; }
}

public class PpRfqItemOption
{
    public long RfqItemId { get; set; }
    public long RfqId { get; set; }
    public string RfqName { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    /// <summary>Already on the user's Procurement Plus list (shown or hidden).</summary>
    public bool AlreadyAdded { get; set; }
}

public class PpAddPartsRequest
{
    /// <summary>ILS (files), Project or WeChat — Project / WeChat parts need a target price each.</summary>
    public string? Track { get; set; }
    public List<PpAddPartItem> Items { get; set; } = new();
}

/// <summary>A part picked from an RFQ item, or a custom one typed by the user.</summary>
public class PpAddPartItem
{
    public long? RfqItemId { get; set; }
    /// <summary>Custom part (no RfqItemId): the P/N — matched to the catalog when it exists there.</summary>
    public string? PartNumber { get; set; }
    /// <summary>Custom parts; for RFQ items they override the item's values when given.</summary>
    public string? Description { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public decimal? TargetPrice { get; set; }
    /// <summary>Alternative P/Ns typed / pasted with the part — added to the catalog part's (or the RFQ item's) ones.</summary>
    public List<string>? Alternatives { get; set; }
}

public class PpAddPartsResponse
{
    public int Added { get; set; }
    /// <summary>Hidden parts that were picked again and are shown again.</summary>
    public int Restored { get; set; }
    public int AlreadyListed { get; set; }
    /// <summary>Ids of every part the request added, showed again or found already listed.</summary>
    public List<long> Ids { get; set; } = new();
}

/// <summary>Edit a preferred part (null = keep).</summary>
public class PpUpdatePartRequest
{
    public string? Description { get; set; }
    public List<string>? Alternatives { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public decimal? TargetPrice { get; set; }
    /// <summary>Move to ILS / Project / WeChat.</summary>
    public string? Track { get; set; }
}

/// <summary>Move parts to a track (e.g. "No supplier" parts → Project), with a target price each.</summary>
public class PpSetTrackRequest
{
    public string Track { get; set; } = "Project";
    public List<PpTrackItem> Items { get; set; } = new();
}

public class PpTrackItem
{
    public long Id { get; set; }
    public decimal? TargetPrice { get; set; }
}

/// <summary>A supplier added by hand to a part (Project / WeChat, or any part): preferred, catalog or just a name.</summary>
public class PpAddLineRequest
{
    public long? PreferredId { get; set; }
    public long? SupplierId { get; set; }
    /// <summary>Custom supplier name when it is neither preferred nor in the catalog.</summary>
    public string? SupplierName { get; set; }
    /// <summary>A row of the user's files: the line takes its company, price, qty, condition and alt.</summary>
    public long? OfferId { get; set; }
    /// <summary>How the part is searched with this supplier: ILS / Email / Search, one or more (null = the usual default).</summary>
    public List<string>? Methods { get; set; }
    /// <summary>Just the supplier (part history): no qty or condition copied onto the line.</summary>
    public bool NameOnly { get; set; }
}

public class PpLineMethodsRequest
{
    public List<string> Methods { get; set; } = new();
}

public class PpNoteRequest
{
    public string Text { get; set; } = string.Empty;
}

public class PpEventResponse
{
    public long Id { get; set; }
    public string Kind { get; set; } = "Note";
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? UserName { get; set; }
}

/// <summary>One part on the Sourcing / Project / WeChat boards: the part, its supplier lines and its timeline.</summary>
public class PpBoardPart
{
    public PpPartResponse Part { get; set; } = null!;
    /// <summary>Lines being priced, then lines Done in the last AutoApplyDays.</summary>
    public List<PpQuoteLineResponse> Lines { get; set; } = new();
    public List<PpEventResponse> Events { get; set; } = new();
    /// <summary>Rows for the part in the owner's current files (Suppliers available).</summary>
    public List<PpOfferRow> Offers { get; set; } = new();
    /// <summary>
    /// Email Quotes lines (MailFlow) for the part — pending, or accepted in the last AutoApplyDays — shown as
    /// editable rows under it. Lines that answer one of the part's own lines are on that line (EmailQuote) instead.
    /// </summary>
    public List<PpQuoteLineResponse> EmailLines { get; set; } = new();
}

public class PpPriceStat
{
    /// <summary>NE, OH, SV… ("" = not given).</summary>
    public string Condition { get; set; } = string.Empty;
    public decimal Average { get; set; }
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public int Count { get; set; }
    public DateTime LastDate { get; set; }
}

/// <summary>One page of a board (Parts / Project / WeChat) — paged and filtered on the server.</summary>
public class PpBoardPage
{
    public List<PpBoardPart> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    /// <summary>Per-status counts for the filter chips (after the search, before the status filter).</summary>
    public PpBoardCounts Counts { get; set; } = new();
}

public class PpBoardCounts
{
    public int All { get; set; }
    /// <summary>No supplier line at all.</summary>
    public int NoSupplier { get; set; }
    /// <summary>Lines being priced, some still missing a price / catalog supplier.</summary>
    public int Sourcing { get; set; }
    /// <summary>Every line priced and linked — Done can be clicked.</summary>
    public int Ready { get; set; }
    /// <summary>Only Done lines left.</summary>
    public int Done { get; set; }
    /// <summary>No row in the current files.</summary>
    public int NotInFiles { get; set; }
    /// <summary>Parts tab: how many come from each tab — parts / project / wechat / rfq (after the search).</summary>
    public Dictionary<string, int> Origins { get; set; } = new();
}

/// <summary>"By supplier" view: one supplier with all its lines (any part).</summary>
public class PpSupplierGroup
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? PreferredId { get; set; }
    public long? SupplierId { get; set; }
    public string? SourceMethod { get; set; }
    public int PendingCount { get; set; }
    public int PricedCount { get; set; }
    public int DoneCount { get; set; }
    public List<PpQuoteLineResponse> Lines { get; set; } = new();
}

public class PpSupplierPage
{
    public List<PpSupplierGroup> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>"By RFQ" view: every item of one RFQ — which have suppliers, which don't.</summary>
public class PpRfqCoverage
{
    public PpRfqOption Rfq { get; set; } = null!;
    public List<PpRfqCoverageItem> Items { get; set; } = new();
}

/// <summary>An RFQ on the user's saved "My RFQs" list.</summary>
public class PpMyRfq
{
    public long RfqId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public int ItemCount { get; set; }
    /// <summary>Items with a supplier (a quote on the RFQ, or a line here).</summary>
    public int CoveredCount { get; set; }
    /// <summary>Last opened / added.</summary>
    public DateTime AddedAt { get; set; }
}

public class PpRfqIdRequest
{
    public long RfqId { get; set; }
}

public class PpRfqCoverageItem
{
    public long RfqItemId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Alt { get; set; }
    /// <summary>Supplier quotes already on this RFQ item.</summary>
    public List<PpRfqQuote> RfqQuotes { get; set; } = new();
    /// <summary>Supplier quotes for the same part (name, new name, alternatives) on other RFQs, newest first.</summary>
    public List<PpRfqQuote> OlderQuotes { get; set; } = new();
    /// <summary>How many older quotes exist (OlderQuotes holds the newest MaxOlderQuotes).</summary>
    public int OlderQuoteCount { get; set; }
    /// <summary>The part on the user's Procurement Plus list (any track) with its lines; null = not on the list.</summary>
    public PpBoardPart? Board { get; set; }
    /// <summary>Not on the list: rows for it in the user's current files (on the list they are on Board).</summary>
    public List<PpOfferRow> Offers { get; set; } = new();
    public bool HasSupplier { get; set; }
}

public class PpRfqQuote
{
    /// <summary>The RFQ the quote is on (older quotes).</summary>
    public long? RfqId { get; set; }
    public string? RfqName { get; set; }
    public string? CustomerCode { get; set; }
    /// <summary>The P/N the quote was for (can be an alternative of the item).</summary>
    public string? PartNumber { get; set; }
    public string? Alt { get; set; }
    public string? CertName { get; set; }
    public DateOnly? TagDate { get; set; }
    /// <summary>Catalog supplier of the quote (part history: "add this supplier" under the part).</summary>
    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Condition { get; set; }
    public double Qty { get; set; }
    public string? LeadTime { get; set; }
    public DateTime Date { get; set; }
}

public class PpUserSummary
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PartCount { get; set; }
    public int SourcingCount { get; set; }
}

public class PpIdsRequest
{
    public List<long> Ids { get; set; } = new();
}

// ──── Parts list ────

public class PpPartResponse
{
    public long Id { get; set; }
    /// <summary>Linked catalog part, if any — a preferred part does not have to be in the catalog.</summary>
    public long? PartNumberId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Every alternative P/N it answers to (its own + the linked catalog part's).</summary>
    public List<string> Alternatives { get; set; } = new();
    /// <summary>Alternatives stored on the preferred part itself (editable).</summary>
    public List<string> OwnAlternatives { get; set; } = new();
    public DateTime AddedAt { get; set; }
    public bool Hidden { get; set; }
    /// <summary>ILS / Project / WeChat.</summary>
    public string Track { get; set; } = "ILS";
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public decimal? TargetPrice { get; set; }
    /// <summary>Average price per condition: supplier quotes on older RFQs + prices typed here.</summary>
    public List<PpPriceStat> PriceStats { get; set; } = new();
    /// <summary>Whose working list it is (SuperAdmin "All users" view).</summary>
    public long? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    /// <summary>RFQs a "Done" quote would be written to now (deadline at most a week ago).</summary>
    public List<PpRfqRef> OpenRfqs { get; set; } = new();
    /// <summary>Rows for this part in the files uploaded in the last FreshDays days.</summary>
    public int OfferCount { get; set; }
    public int PreferredSupplierCount { get; set; }
    public int OtherSupplierCount { get; set; }
    /// <summary>Lines being sourced (not Done yet).</summary>
    public int SourcingCount { get; set; }
    /// <summary>
    /// Quotes for this part that are still auto-filling new RFQs — Done here or accepted on Email Quotes
    /// within AutoApplyDays — with the RFQs they were written to.
    /// </summary>
    public List<PpLiveQuote> LiveQuotes { get; set; } = new();
    /// <summary>Parts tab, not on the list yet: the saved-RFQ item it stands for (Id is then -RfqItemId).</summary>
    public long? RfqItemId { get; set; }
    /// <summary>Not on the list yet: the user's saved RFQs (By RFQ) that ask for it.</summary>
    public List<PpRfqRef> FromRfqs { get; set; } = new();
}

/// <summary>A part's history (clock button on a part card): its supplier quotes on RFQs and its timeline.</summary>
public class PpPartHistory
{
    public string PartNumber { get; set; } = string.Empty;
    /// <summary>Supplier quotes for the part (name, alternatives, linked catalog part) on any RFQ, newest first.</summary>
    public List<PpRfqQuote> Quotes { get; set; } = new();
    /// <summary>How many exist (Quotes holds the newest MaxHistoryQuotes).</summary>
    public int QuoteCount { get; set; }
    public List<PpEventResponse> Events { get; set; } = new();
}

public class PpRfqRef
{
    public long RfqId { get; set; }
    public string RfqName { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public string? CustomerCode { get; set; }
    public double? Qty { get; set; }
    public decimal? Price { get; set; }
}

public class PpLiveQuote
{
    public long QuoteId { get; set; }
    /// <summary>"Email" (Email Quotes) or "ProcurementPlus".</summary>
    public string Source { get; set; } = "Email";
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal? Price { get; set; }
    public string? Condition { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTime? AutoFillUntil { get; set; }
    public List<PpRfqRef> Rfqs { get; set; } = new();
}

// ──── Uploads ────

public class PpUploadResponse
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int RowCount { get; set; }
    /// <summary>Rows for one of the user's parts.</summary>
    public int MatchedRows { get; set; }
}

public class PpUploadResult
{
    public List<PpUploadResponse> Uploads { get; set; } = new();
    /// <summary>Files that could not be read, with why.</summary>
    public List<PpUploadError> Errors { get; set; } = new();
}

public record PpUploadError(string FileName, string Message);

// ──── Availability (Suppliers Available modal) ────

public class PpAvailabilityResponse
{
    public int FreshDays { get; set; }
    public List<PpUploadResponse> Uploads { get; set; } = new();
    /// <summary>Tab 1 — rows from preferred suppliers.</summary>
    public List<PpOfferRow> Preferred { get; set; } = new();
    /// <summary>Tab 2 — rows from other catalog suppliers and from companies not found in the catalog.</summary>
    public List<PpOfferRow> Others { get; set; } = new();
    /// <summary>Tab 3 — the user's parts no file row lists.</summary>
    public List<PpMissingPart> Missing { get; set; } = new();
    /// <summary>Tab 4 — suppliers we worked with on the part before, not in the current files.</summary>
    public List<PpHistoryRow> History { get; set; } = new();
    /// <summary>Company names in the files no catalog supplier matched.</summary>
    public List<PpUnmatchedCompany> Unmatched { get; set; } = new();
}

public class PpOfferRow
{
    public long OfferId { get; set; }
    public long UploadId { get; set; }
    public long PartId { get; set; }
    /// <summary>The user's part this row answers.</summary>
    public string PartNumber { get; set; } = string.Empty;
    /// <summary>P/N as written in the file (can be one of the part's alternatives).</summary>
    public string FilePartNo { get; set; } = string.Empty;
    public string? AltPartNo { get; set; }
    /// <summary>Catalog supplier the row matched (may be null — then only the company name is known).</summary>
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierStatus { get; set; }
    /// <summary>The preferred supplier (Preferreds table) the row matched; null = Other suppliers tab.</summary>
    public long? PreferredId { get; set; }
    public string? PreferredName { get; set; }
    public string CompanyRaw { get; set; } = string.Empty;
    public string CompanyKey { get; set; } = string.Empty;
    public string? SupplierComment { get; set; }
    public string? QtyRaw { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? PriceRaw { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Distance { get; set; }
    public string? Cage { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    /// <summary>Preferred suppliers: their sending method.</summary>
    public string? SourceMethod { get; set; }
    /// <summary>The supplier main email (catalog) for Send Email.</summary>
    public string? SupplierEmail { get; set; }
    /// <summary>This supplier + part is already being sourced, or Done within AutoApplyDays.</summary>
    public string? SourcingState { get; set; }
}

public class PpMissingPart
{
    public long PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Alternatives { get; set; } = new();
    public int OpenRfqCount { get; set; }
}

public class PpHistoryRow
{
    public long PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    /// <summary>"RFQ quote" (supplier quote on an RFQ), "File" (an older upload) or "Procurement Plus".</summary>
    public string Kind { get; set; } = string.Empty;
    public decimal? LastPrice { get; set; }
    public string? Condition { get; set; }
    public double? Qty { get; set; }
    public DateTime LastDate { get; set; }
    public int Times { get; set; }
    public string? LastRfqName { get; set; }
    public string? Comment { get; set; }
    public bool IsPreferred { get; set; }
}

public class PpUnmatchedCompany
{
    public string CompanyKey { get; set; } = string.Empty;
    public string CompanyRaw { get; set; } = string.Empty;
    public List<string> Emails { get; set; } = new();
    public List<string> Phones { get; set; } = new();
    public string? Comment { get; set; }
    public List<string> PartNumbers { get; set; } = new();
    public List<string> Files { get; set; } = new();
    public int RowCount { get; set; }
}

public class PpResolveCompanyRequest
{
    public string CompanyRaw { get; set; } = string.Empty;
    public long SupplierId { get; set; }
}

public class PpResolveCompanyResponse
{
    /// <summary>File rows now linked to the supplier.</summary>
    public int Updated { get; set; }
    /// <summary>The company's addresses from the files added to the supplier's catalog contacts.</summary>
    public List<string> EmailsAdded { get; set; } = new();
    /// <summary>Addresses not added because another supplier already has them ("x@y.com (Other Co)").</summary>
    public List<string> EmailsOnOtherSupplier { get; set; } = new();
}

// ──── Preferred suppliers ────

public class PpPreferredSupplierResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Emails { get; set; } = new();
    public string? Phone { get; set; }
    public string SourceMethod { get; set; } = "Email";
    public string? Note { get; set; }
    /// <summary>Optional catalog supplier (needed only to put prices on RFQs).</summary>
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierStatus { get; set; }
    public DateTime AddedAt { get; set; }
    public string? AddedBy { get; set; }
}

/// <summary>
/// Add or edit a preferred supplier. It does not have to be in the catalog: a name (or a catalog
/// supplier to copy it from) is enough.
/// </summary>
public class PpSavePreferredSupplierRequest
{
    public string? Name { get; set; }
    public List<string> Emails { get; set; } = new();
    public string? Phone { get; set; }
    public string? SourceMethod { get; set; }
    public string? Note { get; set; }
    /// <summary>Optional catalog supplier link (0 / null = none).</summary>
    public long? SupplierId { get; set; }
}

public class PpLinkPreferredRequest
{
    /// <summary>Catalog supplier; null unlinks.</summary>
    public long? SupplierId { get; set; }
}

public class PpLinkPreferredResponse
{
    /// <summary>Lines being sourced that now point at the catalog supplier.</summary>
    public int LinesUpdated { get; set; }
    public List<string> EmailsAdded { get; set; } = new();
    public List<string> EmailsOnOtherSupplier { get; set; } = new();
}

public class PpUpdatePreferredRequest
{
    /// <summary>null = keep.</summary>
    public string? SourceMethod { get; set; }
    /// <summary>null = keep, "" = clear.</summary>
    public string? Note { get; set; }
}

public class PpSupplierOption
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Status { get; set; }
    public bool IsPreferred { get; set; }
    /// <summary>The search was an email/domain and this supplier uses that company domain.</summary>
    public bool DomainMatch { get; set; }
}

// ──── Sourcing / Done ────

public class PpStartSourcingRequest
{
    public List<PpSourceSupplier> Suppliers { get; set; } = new();
}

public class PpSourceSupplier
{
    /// <summary>The preferred supplier (Preferreds id); null = a non-preferred supplier (catalog or just a company in the file).</summary>
    public long? PreferredId { get; set; }
    /// <summary>null = the preferred supplier's method (Email for a non-preferred one).</summary>
    public string? SourceMethod { get; set; }
    /// <summary>
    /// Rows to source; empty = every fresh row matching this preferred supplier for the user's shown parts.
    /// A non-preferred supplier needs its rows listed.
    /// </summary>
    public List<long> OfferIds { get; set; } = new();
}

public class PpStartSourcingResponse
{
    public int Created { get; set; }
    /// <summary>Supplier + part + condition lines already being sourced (reused, not duplicated).</summary>
    public int Existing { get; set; }
    public List<long> LineIds { get; set; } = new();
}

/// <summary>A line being sourced or Done — the email-quote line plus who/what it came from.</summary>
public class PpQuoteLineResponse
{
    public PendingSupplierQuoteResponse Line { get; set; } = null!;
    /// <summary>The user's preferred part it prices.</summary>
    public long? PreferredPartId { get; set; }
    /// <summary>Pending (being priced) or Accepted (Done).</summary>
    public string Status { get; set; } = "Pending";
    /// <summary>The preferred supplier it was sourced from.</summary>
    public long? PreferredId { get; set; }
    public string? PreferredName { get; set; }
    public List<string> PreferredEmails { get; set; } = new();
    public string? SourceMethod { get; set; }
    public string? SupplierStatus { get; set; }
    public string? SupplierMainEmail { get; set; }
    public string? SupplierPhone { get; set; }
    public long? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    /// <summary>Done lines: the RFQs it was written to.</summary>
    public List<PpRfqRef> WrittenRfqs { get; set; } = new();
    /// <summary>Added from a row of an ILS / Excel file ("Available in your files").</summary>
    public bool FromFile { get; set; }
    /// <summary>An Email Quotes line (MailFlow), not a Procurement Plus one.</summary>
    public bool IsEmail { get; set; }
    /// <summary>Copied from the user's own supplier quote on this RFQ (last two weeks) — Done never writes back onto it.</summary>
    public PpRfqRef? FromRfq { get; set; }
    /// <summary>When that RFQ quote was added.</summary>
    public DateTime? FromRfqQuotedAt { get; set; }
    /// <summary>The supplier also quoted this line by email (same part + condition) — its price came from that mail.</summary>
    public PpEmailQuoteRef? EmailQuote { get; set; }
}

public class PpEmailQuoteRef
{
    public long Id { get; set; }
    public string? Subject { get; set; }
    public DateTime? Date { get; set; }
    public string? SupplierEmail { get; set; }
    public decimal? Price { get; set; }
    public string Status { get; set; } = "Pending";
}

public class PpDoneResponse
{
    public PpDoneSummary Summary { get; set; } = new();
    public List<PpQuoteLineResponse> Lines { get; set; } = new();
}

public class PpDoneSummary
{
    public int Accepted { get; set; }
    public int QuotesWritten { get; set; }
    public List<PendingQuoteFailure> Failed { get; set; } = new();
}

public class PpUserOption
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PpDoneListResponse
{
    public List<PpQuoteLineResponse> Lines { get; set; } = new();
    public List<PpUserOption> Users { get; set; } = new();
}
