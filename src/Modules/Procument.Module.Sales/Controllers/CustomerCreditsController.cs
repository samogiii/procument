using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/customer-credits")]
[Authorize(Roles = "SuperAdmin,Admin,Payment,AHM")]
public class CustomerCreditsController(DbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search = null)
    {
        var query = db.Set<Customer>().AsNoTracking().Where(c => c.CreditEnabled);
        if (!IsAdmin())
        {
            var bases = GetUserBases();
            query = bases.Length == 0
                ? query.Where(_ => false)
                : query.Where(c => c.Base.HasValue && bases.Contains(c.Base.Value));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(c => c.Name.Contains(value) || (c.CustomerCode != null && c.CustomerCode.Contains(value)));
        }

        var customers = await query.OrderBy(c => c.Name).Select(c => c.Id).ToListAsync();
        var results = new List<CreditAvailability>();
        foreach (var customerId in customers)
            results.Add(await InvoicePaymentTerms.GetCreditAsync(db, customerId));
        return Ok(results);
    }

    [HttpGet("{customerId:long}")]
    public async Task<IActionResult> Get(long customerId)
    {
        if (!await CanAccessCustomerAsync(customerId)) return Forbid();
        return Ok(await InvoicePaymentTerms.GetCreditAsync(db, customerId));
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check([FromQuery] long customerId, [FromQuery] decimal amount,
        [FromQuery] long? excludeInvoiceId = null)
    {
        if (!await CanAccessCustomerAsync(customerId)) return Forbid();
        var credit = await InvoicePaymentTerms.GetCreditAsync(db, customerId, excludeInvoiceId);
        return Ok(new { credit, isSufficient = credit.Enabled && amount <= credit.AvailableCredit, requestedAmount = amount });
    }

    private bool IsAdmin() => User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

    private int[] GetUserBases() => (User.FindFirst("bases")?.Value ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(value => int.TryParse(value, out var parsed) ? parsed : -1)
        .Where(value => value > 0)
        .ToArray();

    private async Task<bool> CanAccessCustomerAsync(long customerId)
    {
        if (IsAdmin()) return true;
        var bases = GetUserBases();
        return bases.Length > 0 && await db.Set<Customer>()
            .AnyAsync(customer => customer.Id == customerId && customer.Base.HasValue && bases.Contains(customer.Base.Value));
    }
}
