using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales.Controllers;

/// <summary>The local POP reader's results (see PopReading): read a POP again after a failure or a doubtful answer.</summary>
[ApiController]
[Route("api/pop-readings")]
[Authorize(Roles = "SuperAdmin,Admin,Payment,AHM")]
public class PopReadingsController(DbContext db, PopReaderSignal signal) : ControllerBase
{
    /// <param name="kind">Deposit, Transfer, Withdraw or Payment (supplier payment, refId = PaymentRequest id).</param>
    [HttpPost("{kind}/{refId:long}/read-again")]
    public async Task<IActionResult> ReadAgain(string kind, long refId)
    {
        var k = kind.ToLowerInvariant() switch
        {
            "deposit" => PopReading.Deposit,
            "transfer" => PopReading.Transfer,
            "withdraw" => PopReading.Withdraw,
            "payment" => PopReading.Payment,
            _ => null,
        };
        if (k == null) return BadRequest(new { message = "Unknown kind." });
        var hasPop = k switch
        {
            PopReading.Payment => await db.Set<PaymentRequest>().AnyAsync(p => p.Id == refId && p.WarehousePaidAt != null && p.WarehousePopFileName != null),
            PopReading.Deposit => await db.Set<IncomingDeposit>().AnyAsync(d => d.Id == refId && d.BankPopFileName != null && d.Status == IncomingDeposit.Pending),
            PopReading.Transfer => await db.Set<WalletTransferPending>().AnyAsync(t => t.Id == refId && t.PopFileName != null && t.Status == WalletTransferService.Review),
            _ => await db.Set<WalletWithdrawPending>().AnyAsync(w => w.Id == refId && w.PopFileName != null && w.Status == WalletWithdrawPending.Review),
        };
        if (!hasPop) return BadRequest(new { message = "There is no POP waiting for acceptance here." });
        await PopReading.QueueAsync(db, k, refId);
        await db.SaveChangesAsync();
        signal.Poke();
        return Ok(new { queued = true });
    }
}
