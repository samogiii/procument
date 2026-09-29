using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;

namespace Procument.Module.Sales.Controllers;

/// <summary>
/// Credit we hold with suppliers, per buying company. Reads and applying credit to a PO follow the
/// supplier-payment roles; adding credit and admin corrections are narrower (see each action).
/// </summary>
[ApiController]
[Route("api/supplier-credits")]
[Authorize(Roles = "SuperAdmin,Admin,Payment,AHM")]
public class SupplierCreditsController(ISupplierCreditService credits) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] SupplierCreditAccountQuery query) =>
        Ok(await credits.GetAccountsAsync(query));

    [HttpGet("filter-options")]
    public async Task<IActionResult> FilterOptions([FromQuery] SupplierCreditAccountQuery query) =>
        Ok(await credits.GetFilterOptionsAsync(query));

    /// <summary>Credit per company with one supplier (balance &gt; 0) — for the PO company picker.</summary>
    [HttpGet("balances")]
    public Task<IActionResult> Balances([FromQuery] long supplierId, [FromQuery] string? kind = null) =>
        Run(async () => Ok(await credits.GetBalancesAsync(supplierId, kind)));

    /// <summary>Tab badges: prepaid total, number of credit lines with a debt, total owed.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary() => Ok(await credits.GetSummaryAsync());

    [HttpGet("lookup/suppliers")]
    public async Task<IActionResult> LookupSuppliers([FromQuery] string? q) =>
        Ok(await credits.SearchSuppliersAsync(q));

    [HttpGet("lookup/companies")]
    public async Task<IActionResult> LookupCompanies() =>
        Ok(await credits.GetCompaniesAsync());

    /// <summary>The supplier's POs, offered as the origin of a credit note.</summary>
    [HttpGet("lookup/purchase-orders")]
    public async Task<IActionResult> LookupPurchaseOrders([FromQuery] long supplierId, [FromQuery] string? q) =>
        Ok(await credits.SearchPurchaseOrdersAsync(supplierId, q));

    [HttpGet("po/{poId:long}/suggestion")]
    public async Task<IActionResult> Suggestion(long poId) =>
        await credits.GetSuggestionAsync(poId) is { } suggestion ? Ok(suggestion) : NotFound();

    [HttpGet("{accountId:long}")]
    public async Task<IActionResult> Get(long accountId) =>
        await credits.GetAccountAsync(accountId) is { } account ? Ok(account) : NotFound();

    [HttpGet("{accountId:long}/entries")]
    public async Task<IActionResult> Entries(long accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        await credits.GetEntriesAsync(accountId, page, pageSize) is { } entries ? Ok(entries) : NotFound();

    [HttpGet("documents/{documentId:long}/file")]
    public async Task<IActionResult> File(long documentId)
    {
        var file = await credits.OpenDocumentAsync(documentId);
        if (file == null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.FileName, out var mime);
        return base.File(file.Value.Stream, mime ?? "application/octet-stream", file.Value.FileName);
    }

    [HttpPost("credit-note")]
    [Authorize(Roles = "SuperAdmin,Admin,Payment")]
    [RequestSizeLimit(104_857_600)]
    [Auditable("SupplierCredit", "CreditNote")]
    public Task<IActionResult> AddCreditNote([FromForm] AddSupplierCreditNoteRequest request) => Run(async () =>
    {
        var uploads = new List<SupplierCreditUpload>();
        if (request.SupplierPi is { Length: > 0 } pi)
            uploads.Add(Upload(SupplierCreditDocumentCategories.SupplierPI, pi));
        uploads.AddRange((request.Documents ?? []).Where(f => f.Length > 0)
            .Select(f => Upload(SupplierCreditDocumentCategories.Other, f)));
        return Ok(await credits.AddCreditNoteAsync(request, uploads, UserId()));
    });

    /// <summary>Attach more proof to an entry (supplier PI, credit memo scan, other files).</summary>
    [HttpPost("entries/{id:long}/documents")]
    [Authorize(Roles = "SuperAdmin,Admin,Payment")]
    [RequestSizeLimit(104_857_600)]
    [Auditable("SupplierCredit", "AddDocuments")]
    public Task<IActionResult> AddDocuments(long id, [FromForm] AddSupplierCreditDocumentsRequest request) => Run(async () =>
    {
        var uploads = (request.Files ?? []).Where(f => f.Length > 0)
            .Select(f => Upload(request.Category, f)).ToList();
        return Ok(await credits.AddDocumentsAsync(id, uploads, UserId()));
    });

    [HttpPost("wallet-top-up")]
    [Authorize(Roles = "SuperAdmin,Admin,Payment")]
    [Auditable("SupplierCredit", "WalletTopUp", CaptureBody = true)]
    public Task<IActionResult> AddWalletTopUp([FromBody] AddSupplierCreditTopUpRequest request) =>
        Run(async () => Ok(await credits.AddWalletTopUpAsync(request, UserId())));

    [HttpPost("adjustment")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Auditable("SupplierCredit", "Adjustment", CaptureBody = true)]
    public Task<IActionResult> AddAdjustment([FromBody] AddSupplierCreditAdjustmentRequest request) =>
        Run(async () => Ok(await credits.AddAdjustmentAsync(request, UserId())));

    [HttpPost("po/{poId:long}/apply")]
    [Auditable("PurchaseOrder", "ApplySupplierCredit", EntityIdParam = "poId", CaptureBody = true)]
    public Task<IActionResult> Apply(long poId, [FromBody] ApplySupplierCreditRequest request) =>
        Run(async () => Ok(await credits.ApplyToPoAsync(poId, request, UserId())));

    [HttpPost("entries/{id:long}/reverse")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Auditable("SupplierCredit", "Reverse", CaptureBody = true)]
    public Task<IActionResult> Reverse(long id, [FromBody] ReverseSupplierCreditRequest request) =>
        Run(async () => Ok(await credits.ReverseAsync(id, request, UserId())));

    // ── Credit lines (E10): buy now, pay back later ─────────────────────────

    /// <summary>Create a credit line or change its limit (never below what is owed).</summary>
    [HttpPut("credit-lines")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Auditable("SupplierCredit", "SetCreditLine", CaptureBody = true)]
    public Task<IActionResult> SetCreditLine([FromBody] SetSupplierCreditLineRequest request) =>
        Run(async () => Ok(await credits.SetCreditLineAsync(request, UserId())));

    [HttpGet("{accountId:long}/repayments")]
    public async Task<IActionResult> Repayments(long accountId) =>
        await credits.GetRepaymentsAsync(accountId) is { } rows ? Ok(rows) : NotFound();

    /// <summary>Pay back, step 1: amount, wallet and rate (a payment request without a PO).</summary>
    [HttpPost("{accountId:long}/repayments/draft")]
    [Auditable("SupplierCredit", "RepaymentDraft", EntityIdParam = "accountId", CaptureBody = true)]
    public Task<IActionResult> SaveRepaymentDraft(long accountId, [FromBody] SaveSupplierCreditRepaymentRequest request) =>
        Run(async () => Ok(await credits.SaveRepaymentDraftAsync(accountId, request, UserId())));

    /// <summary>Pay back, step 2: the POP — the wallet is debited and the owed amount goes down.</summary>
    [HttpPost("{accountId:long}/repayments/{paymentRequestId:long}/pop")]
    [RequestSizeLimit(104_857_600)]
    [Auditable("SupplierCredit", "RepaymentPop", EntityIdParam = "accountId")]
    public Task<IActionResult> UploadRepaymentPop(long accountId, long paymentRequestId, [FromForm] IFormFile file) => Run(async () =>
    {
        if (file is not { Length: > 0 })
            return BadRequest(new { message = "Choose a POP image or PDF first." });
        return Ok(await credits.UploadRepaymentPopAsync(accountId, paymentRequestId,
            Upload(SupplierCreditDocumentCategories.Other, file), UserId()));
    });

    /// <summary>Pay back, step 3: the bank's final debit; the difference becomes "Bank Fee and Others".</summary>
    [HttpPost("{accountId:long}/repayments/{paymentRequestId:long}/final-amount")]
    [Auditable("SupplierCredit", "RepaymentFinalAmount", EntityIdParam = "accountId", CaptureBody = true)]
    public Task<IActionResult> FinalizeRepayment(long accountId, long paymentRequestId, [FromBody] FinalizeSupplierCreditRepaymentRequest request) =>
        Run(async () => Ok(await credits.FinalizeRepaymentAsync(accountId, paymentRequestId, request)));

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (SupplierCreditException ex) { return BadRequest(new { message = ex.Message }); }
        catch (SupplierCreditConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private static SupplierCreditUpload Upload(string category, IFormFile file) =>
        new(category, file.FileName, file.Length, file.OpenReadStream);

    private long? UserId() =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
