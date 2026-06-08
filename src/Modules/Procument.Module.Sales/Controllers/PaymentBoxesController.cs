using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Services;
using WalletSelectionResponse = Procument.Module.Sales.DTOs.WalletSelectionResponse;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/payment-boxes")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class PaymentBoxesController : ControllerBase
{
    private readonly IPaymentBoxService _service;

    public PaymentBoxesController(IPaymentBoxService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<PaymentBoxSummaryResponse>>> GetAll([FromQuery] long? presetId = null)
        => Ok(await _service.GetAllAsync(presetId));

    [HttpGet("all-transactions")]
    public async Task<ActionResult<List<AllTransactionRow>>> GetAllTransactions()
        => Ok(await _service.GetAllTransactionsAsync());

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PaymentBoxDetailResponse>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<PaymentBoxSummaryResponse>> CreateBox([FromBody] CreatePaymentBoxRequest req)
        => Ok(await _service.CreateBoxAsync(req));

    [HttpPatch("{id:long}/rename")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<ActionResult<PaymentBoxSummaryResponse>> RenameBox(long id, [FromBody] RenamePaymentBoxRequest req)
    {
        var result = await _service.RenameBoxAsync(id, req.Name);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPatch("{id:long}/bank-details")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<ActionResult<PaymentBoxSummaryResponse>> UpdateBankDetails(long id, [FromBody] UpdateWalletBankDetailsRequest req)
    {
        var result = await _service.UpdateBankDetailsAsync(id, req);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("for-customer/{customerId:long}")]
    public async Task<ActionResult<List<PaymentBoxSummaryResponse>>> GetForCustomer(long customerId)
        => Ok(await _service.GetBoxesForCustomerAsync(customerId));

    /// <summary>Lightweight wallet list for selection dropdowns — available to Payment and Expert roles.</summary>
    [HttpGet("simple-list")]
    [Authorize(Roles = "Payment,Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<List<WalletSelectionResponse>>> GetSimpleList()
        => Ok(await _service.GetSimpleListAsync());

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteBox(long id)
    {
        if (!await _service.DeleteBoxAsync(id)) return NotFound();
        return Ok();
    }

    [HttpPost("{id:long}/transactions")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<PaymentTransactionRow>> AddTransaction(long id, [FromBody] CreateTransactionRequest req)
    {
        var result = await _service.AddTransactionAsync(id, req);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPatch("{id:long}/transactions/{txId:long}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<PaymentTransactionRow>> UpdateTransaction(long id, long txId, [FromBody] UpdateTransactionRequest req)
    {
        var result = await _service.UpdateTransactionAsync(txId, req);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:long}/transactions/{txId:long}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteTransaction(long id, long txId)
    {
        if (!await _service.DeleteTransactionAsync(txId)) return NotFound();
        return Ok();
    }

    [HttpPost("{id:long}/transfer")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Transfer(long id, [FromBody] WalletTransferRequest req)
    {
        if (!await _service.TransferAsync(id, req)) return NotFound();
        return Ok();
    }
}
