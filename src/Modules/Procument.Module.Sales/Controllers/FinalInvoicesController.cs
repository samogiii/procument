using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Procument.Shared.DTOs;

using Procument.Module.Sales.DTOs;

using Procument.Module.Sales.Services;

using Procument.Shared.Services;



namespace Procument.Module.Sales.Controllers;



[ApiController]

[Route("api/final-invoices")]

[Authorize(Roles = "Admin,SuperAdmin")]

public class FinalInvoicesController : ControllerBase

{

    private readonly IFinalInvoiceService _service;

    private readonly IFinalInvoiceLockGuard _lockGuard;



    public FinalInvoicesController(IFinalInvoiceService service, IFinalInvoiceLockGuard lockGuard)

    {

        _service = service;

        _lockGuard = lockGuard;

    }



    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null, [FromQuery] string? customerSearch = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        bool isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        var result = await _service.GetAllAsync(pq, customerSearch, isSuperAdmin, userBases);
        return Ok(result);
    }



    /// <summary>Check if an entity is locked by a Final Invoice. entityType: rfq, quote, invoice, po</summary>

    [HttpGet("is-locked")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Payment,Inventory")]
    public async Task<IActionResult> IsLocked([FromQuery] string entityType, [FromQuery] long entityId)

    {

        var locked = entityType?.ToLower() switch

        {

            "rfq" => await _lockGuard.IsRfqLocked(entityId),

            "quote" => await _lockGuard.IsQuoteLocked(entityId),

            "invoice" => await _lockGuard.IsInvoiceLocked(entityId),

            "po" => await _lockGuard.IsPurchaseOrderLocked(entityId),

            _ => false

        };

        return Ok(new { locked });

    }



    [HttpGet("{id:long}")]

    public async Task<IActionResult> GetById(long id)

    {

        var result = await _service.GetByIdAsync(id);

        return result == null ? NotFound() : Ok(result);

    }



    [HttpGet("eligible-proformas")]

    public async Task<IActionResult> GetEligibleProformas()

    {

        var result = await _service.GetEligibleProformasAsync();

        return Ok(result);

    }



    /// <summary>Check if a final invoice can be created for the given proforma invoice.</summary>

    [HttpGet("check-eligibility/{proformaInvoiceId:long}")]

    public async Task<IActionResult> CheckEligibility(long proformaInvoiceId)

    {

        var canCreate = await _service.CanCreateFinalInvoice(proformaInvoiceId);

        return Ok(new { eligible = canCreate });

    }



    /// <summary>Create a Final Invoice from a Proforma Invoice.</summary>

    [HttpPost]

    public async Task<IActionResult> Create([FromBody] CreateFinalInvoiceRequest request)

    {

        try

        {

            var canCreate = await _service.CanCreateFinalInvoice(request.ProformaInvoiceId);

            if (!canCreate)

                return BadRequest(new { message = "Cannot create final invoice. Either not all POs are completed, or a final invoice already exists." });



            var result = await _service.CreateFromProformaAsync(request.ProformaInvoiceId);

            return Ok(result);

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { message = ex.Message });

        }

    }



    [HttpPut("{id:long}/status")]

    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateFinalInvoiceStatusRequest request)

    {

        var ok = await _service.UpdateStatusAsync(id, request.Status);

        return ok ? Ok() : NotFound();

    }



    [HttpPut("{id:long}")]

    public async Task<IActionResult> Update(long id, [FromBody] UpdateFinalInvoiceRequest request)

    {

        var ok = await _service.UpdateAsync(id, request);

        return ok ? Ok() : NotFound();

    }



    /// <summary>Get enriched data for Final Invoice PDF generation.</summary>

    [HttpGet("{id:long}/pdf-data")]

    public async Task<IActionResult> GetPdfData(long id)

    {

        var fi = await _service.GetByIdAsync(id);

        if (fi == null) return NotFound();



        return Ok(new

        {

            invoiceNumber = fi.InvoiceNumber,

            status = fi.Status,

            totalAmount = fi.TotalAmount,

            shippingMethod = fi.ShippingMethod,

            shippingCost = fi.ShippingCost,

            notes = fi.Notes,

            dueDate = fi.DueDate,

            paidDate = fi.PaidDate,

            createdAt = fi.CreatedAt,

            proformaInvoiceNumber = fi.ProformaInvoiceNumber,

            customerPONumber = fi.CustomerPONumber,

            customerName = fi.CustomerName,

            customerContactPerson = fi.CustomerContactPerson,

            customerBillTo = fi.CustomerBillTo,

            customerBillToEmail = fi.CustomerBillToEmail,

            customerBillToPhone = fi.CustomerBillToPhone,

            customerBillToContactPerson = fi.CustomerBillToContactPerson,

            customerShipTo = fi.CustomerShipTo,

            customerShipToContactPerson = fi.CustomerShipToContactPerson,

            customerShipToEmail = fi.CustomerShipToEmail,

            customerShipToPhone = fi.CustomerShipToPhone,

            customerShipToAccount = fi.CustomerShipToAccount,

            customerTermsAndConditions = fi.CustomerTermsAndConditions,

            customerCurrencyType = fi.CustomerCurrencyType,

            defaultDepositWalletId = fi.DefaultDepositWalletId,

            coefYuan = fi.QuoteCoefYuan,

            exchangeRateYuan = fi.QuoteExchangeRateYuan,

            items = fi.Items.Select(i => new

            {

                partNumber = i.PartNumberName,

                description = i.Description ?? "",

                qty = i.Qty,

                condition = i.Condition ?? "",

                certification = i.CertName ?? "",

                unitPrice = i.UnitPrice,

                totalPrice = i.TotalPrice,

                trackNumber = i.TrackNumber ?? "",

                carrier = i.Carrier ?? "",

            }).ToList(),

        });

    }

}

