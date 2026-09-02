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
        [FromQuery] string? search = null, [FromQuery] string? customerSearch = null,
        [FromQuery] string? pnSearch = null,
        [FromQuery] DateTime? createdFrom = null, [FromQuery] DateTime? createdTo = null,
        [FromQuery] List<string>? customerCodes = null,
        [FromQuery] List<string>? statuses = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        bool isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        var result = await _service.GetAllAsync(pq, customerSearch, isSuperAdmin, userBases, pnSearch, createdFrom, createdTo, customerCodes, statuses, sortBy, sortDesc);
        return Ok(result);
    }

    /// <summary>
    /// Cascading filter options. Pass the active filters to get only the values that still
    /// return rows; call it bare for the full lists the client keeps behind "Show all".
    /// </summary>
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(
        [FromQuery] string? search = null,
        [FromQuery] string? customerSearch = null,
        [FromQuery] string? pnSearch = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        [FromQuery] List<string>? customerCodes = null,
        [FromQuery] List<string>? statuses = null)
    {
        bool isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        var result = await _service.GetFilterOptionsAsync(search, customerSearch, isSuperAdmin, userBases, pnSearch, createdFrom, createdTo, customerCodes, statuses);
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



    /// <summary>
    /// Set or clear the B1 invoice number by hand. Fills one in where the source
    /// proforma had none, or corrects the copy inherited from it.
    /// </summary>

    [HttpPatch("{id:long}/b1-number")]

    public async Task<IActionResult> UpdateB1FinalInvoiceNumber(long id, [FromBody] UpdateB1NumberRequest request)

    {

        var ok = await _service.UpdateB1FinalInvoiceNumberAsync(id, request.B1Number);

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

            b1InvoiceNumber = fi.B1InvoiceNumber,

            status = fi.Status,

            totalAmount = fi.TotalAmount,

            shippingMethod = fi.ShippingMethod,

            shippingCost = fi.ShippingCost,

            notes = fi.Notes,

            dueDate = fi.DueDate,

            paidDate = fi.PaidDate,

            createdAt = fi.CreatedAt,

            proformaInvoiceNumber = fi.ProformaInvoiceNumber,

            b1ProformaInvoiceNumber = fi.B1ProformaInvoiceNumber,

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

            customerContacts = fi.CustomerContacts ?? "",

            defaultDepositWalletId = fi.DefaultDepositWalletId,

            defaultBankAccountId = fi.DefaultBankAccountId,

            coefYuan = fi.QuoteCoefYuan,

            exchangeRateYuan = fi.QuoteExchangeRateYuan,

            items = fi.Items.Select(i => new

            {

                id = i.Id,

                partNumber = i.PartNumberName,

                alt = i.Alt,

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

    /// <summary>
    /// Enriched data for a merged packing list covering several final invoices.
    /// Every requested invoice must belong to the same customer; the header details come from
    /// the earliest one, and each invoice's items are returned separately so the client can
    /// let the user pick which parts actually ship.
    /// </summary>
    [HttpPost("packing-list-data")]
    public async Task<IActionResult> GetMergedPackingListData([FromBody] MergedPackingListDataRequest request)
    {
        var ids = (request?.InvoiceIds ?? new List<long>()).Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest(new { message = "Select at least one final invoice." });

        var invoices = new List<FinalInvoiceResponse>();
        foreach (var id in ids)
        {
            var fi = await _service.GetByIdAsync(id);
            if (fi == null) return NotFound(new { message = $"Final invoice {id} was not found." });
            invoices.Add(fi);
        }

        // The one hard rule: a merged packing list is for a single customer.
        var customers = invoices
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Name = g.First().CustomerName })
            .ToList();
        if (customers.Count > 1)
            return BadRequest(new
            {
                message = "All selected final invoices must belong to the same customer. "
                        + $"Got: {string.Join(", ", customers.Select(c => c.Name))}."
            });

        // Oldest invoice supplies the addresses and contact details for the merged document.
        var ordered = invoices.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList();
        var primary = ordered[0];

        return Ok(new
        {
            customerId = primary.CustomerId,
            customerName = primary.CustomerName,
            customerCode = primary.CustomerCode,
            customerContactPerson = primary.CustomerContactPerson,
            customerBillTo = primary.CustomerBillTo,
            customerBillToEmail = primary.CustomerBillToEmail,
            customerBillToPhone = primary.CustomerBillToPhone,
            customerBillToContactPerson = primary.CustomerBillToContactPerson,
            customerShipTo = primary.CustomerShipTo,
            customerShipToContactPerson = primary.CustomerShipToContactPerson,
            customerShipToEmail = primary.CustomerShipToEmail,
            customerShipToPhone = primary.CustomerShipToPhone,
            customerShipToAccount = primary.CustomerShipToAccount,
            customerContacts = primary.CustomerContacts ?? "",
            primaryInvoiceId = primary.Id,
            invoices = ordered.Select(fi => new
            {
                id = fi.Id,
                invoiceNumber = fi.InvoiceNumber,
                b1InvoiceNumber = fi.B1InvoiceNumber,
                // What the customer knows the document by — B1 number when the base has one.
                displayNumber = string.IsNullOrWhiteSpace(fi.B1InvoiceNumber) ? fi.InvoiceNumber : fi.B1InvoiceNumber,
                proformaInvoiceNumber = fi.ProformaInvoiceNumber,
                b1ProformaInvoiceNumber = fi.B1ProformaInvoiceNumber,
                customerPONumber = fi.CustomerPONumber,
                createdAt = fi.CreatedAt,
                items = fi.Items.Select(i => new
                {
                    id = i.Id,
                    partNumber = i.PartNumberName,
                    alt = i.Alt,
                    description = i.Description ?? "",
                    qty = i.Qty,
                    condition = i.Condition ?? "",
                    certification = i.CertName ?? "",
                }).ToList(),
            }).ToList(),
        });
    }

}

