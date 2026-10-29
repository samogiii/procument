using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

    [HttpGet("settings")]
    public async Task<ActionResult<WalletSettingsResponse>> GetSettings()
        => Ok(await _service.GetSettingsAsync());

    [HttpPut("settings")]
    public async Task<ActionResult<WalletSettingsResponse>> UpdateSettings([FromBody] UpdateWalletSettingsRequest req)
        => Ok(await _service.UpdateSettingsAsync(req, CurrentUserId()));

    /// <summary>Base tags for a transaction from its PR's PI customers, its PI's customer, or its customer.</summary>
    [HttpGet("suggest-bases")]
    public async Task<ActionResult<SuggestedBasesResponse>> SuggestBases(
        [FromQuery] long? paymentRequestId = null, [FromQuery] long? invoiceId = null, [FromQuery] long? customerId = null)
        => Ok(await _service.SuggestBasesAsync(paymentRequestId, invoiceId, customerId));

    [HttpGet("upcoming-withdraws")]
    public async Task<ActionResult<List<UpcomingWithdrawResponse>>> GetUpcomingWithdraws()
        => Ok(await _service.GetUpcomingWithdrawsAsync());

    [HttpPost("upcoming-withdraws")]
    public async Task<ActionResult<UpcomingWithdrawResponse>> AddUpcomingWithdraw([FromBody] CreateUpcomingWithdrawRequest req)
    {
        try
        {
            return Ok(await _service.AddUpcomingWithdrawAsync(req, CurrentUserId()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("upcoming-withdraws/{withdrawId:long}")]
    public async Task<IActionResult> DeleteUpcomingWithdraw(long withdrawId)
    {
        if (!await _service.DeleteUpcomingWithdrawAsync(withdrawId)) return NotFound();
        return Ok();
    }

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
        var result = await _service.RenameBoxAsync(id, req);
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
        try
        {
            var result = await _service.UpdateTransactionAsync(txId, req);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:long}/transactions/{txId:long}/exchange-rate")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<ActionResult<UpdateExchangeRateResponse>> UpdateExchangeRate(
        long id, long txId, [FromBody] UpdateExchangeRateRequest req)
    {
        try
        {
            var result = await _service.UpdateExchangeRateAsync(id, txId, req.ExchangeRate, req.Note, CurrentUserId());
            return result == null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:long}/transactions/{txId:long}/review")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<ActionResult<PaymentTransactionRow>> SetReviewed(
        long id, long txId, [FromBody] SetTransactionReviewRequest req)
    {
        var result = await _service.SetReviewedAsync(id, txId, req.Reviewed, CurrentUserId());
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("unreviewed-count")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<ActionResult<UnreviewedTransactionCountResponse>> GetUnreviewedCount()
        => Ok(await _service.GetUnreviewedCountAsync());

    [HttpDelete("{id:long}/transactions/{txId:long}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteTransaction(long id, long txId)
    {
        try
        {
            if (!await _service.DeleteTransactionAsync(txId)) return NotFound();
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/transfer")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Transfer(long id, [FromBody] WalletTransferRequest req)
    {
        if (!await _service.TransferAsync(id, req)) return NotFound();
        return Ok();
    }

    private long CurrentUserId()
        => long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
}
