using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinalInvoicesController : ControllerBase
{
    private readonly IFinalInvoiceService _service;

    public FinalInvoicesController(IFinalInvoiceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
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
            customerName = fi.CustomerName,
            customerBillTo = fi.CustomerBillTo,
            customerShipTo = fi.CustomerShipTo,
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
