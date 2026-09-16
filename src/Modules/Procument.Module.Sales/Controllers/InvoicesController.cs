using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Module.Identity.Entities;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;
using Procument.Shared.Services;
using System.Security.Claims;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class InvoicesController : ControllerBase, IAsyncActionFilter
{
    private static readonly HashSet<string> BlockedSalesOrderUsers = new(StringComparer.OrdinalIgnoreCase) { "AHM", "MOR" };
    private readonly IInvoiceService _invoiceService;
    private readonly DbContext _db;
    private readonly IFinalInvoiceLockGuard _lockGuard;

    public InvoicesController(IInvoiceService invoiceService, DbContext db, IFinalInvoiceLockGuard lockGuard)
    {
        _invoiceService = invoiceService;
        _db = db;
        _lockGuard = lockGuard;
    }

    async Task IAsyncActionFilter.OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userName = User.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(userName) && BlockedSalesOrderUsers.Contains(userName))
        {
            context.Result = NotFound();
            return;
        }

        await next();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null, [FromQuery] string? customer = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        [FromQuery] List<string>? customerCodes = null,
        [FromQuery] List<string>? statuses = null,
        [FromQuery] List<string>? invoiceNumbers = null,
        [FromQuery] List<string>? subjects = null,
        [FromQuery] List<string>? assignedUsers = null,
        [FromQuery] List<int>? bases = null,
        [FromQuery] string? pnSearch = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _invoiceService.GetAllAsync(pq, userId, isAdmin, status, customer, sortBy, sortDesc, customerCodes, statuses, invoiceNumbers, isSuperAdmin, userBases, pnSearch, createdFrom, createdTo, subjects, bases, assignedUsers);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<InvoiceResponse>> GetById(long id)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        var result = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("by-quote/{quoteId:long}")]
    public async Task<ActionResult<List<InvoiceResponse>>> GetByQuote(long quoteId)
    {
        var (userId, isAdmin, _, _) = GetUserContext();
        var ids = await _db.Set<Invoice>()
            .Where(i => i.QuoteId == quoteId)
            .Select(i => i.Id)
            .ToListAsync();
        var results = new List<InvoiceResponse>();
        foreach (var id in ids)
        {
            var inv = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
            if (inv != null) results.Add(inv);
        }
        return Ok(results);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Invoice", "Create", CaptureBody = true)]
    public async Task<ActionResult<InvoiceResponse>> Create([FromBody] CreateInvoiceRequest request)
    {
        var (userId, _, _, _) = GetUserContext();
        try
        {
            var result = await _invoiceService.CreateAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateInvoiceRequest request)
    {
        try
        {
            var success = await _invoiceService.UpdateAsync(id, request);
            return success ? Ok() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Set or clear the B1 proforma invoice number by hand. Fills one in where the source
    /// quote had none, or corrects the copy inherited from it.
    /// </summary>
    [HttpPatch("{id:long}/b1-number")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Invoice", "UpdateB1InvoiceNumber", CaptureBody = true)]
    public async Task<IActionResult> UpdateB1InvoiceNumber(long id, [FromBody] UpdateB1NumberRequest request)
    {
        var success = await _invoiceService.UpdateB1InvoiceNumberAsync(id, request.B1Number);
        return success ? Ok() : NotFound();
    }

    [HttpPatch("{id:long}/totals")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Invoice", "UpdateTotals")]
    public async Task<IActionResult> UpdateTotals(long id, [FromBody] UpdateInvoiceTotalsRequest request)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return NotFound();

        invoice.Tax = request.Tax;
        invoice.Shipping = request.Shipping;
        invoice.ProcessingFee = request.ProcessingFee;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPatch("{id:long}/items")]
    [Auditable("Invoice", "UpdateItems", CaptureBody = true)]
    public async Task<IActionResult> UpdateItems(long id, [FromBody] UpdateInvoiceItemsRequest request)
    {
        var (userId, isAdmin, _, _) = GetUserContext();
        var success = await _invoiceService.UpdateItemsAsync(id, request, userId, isAdmin);
        return success ? Ok() : Forbid();
    }

    [HttpDelete("{id:long}/items/{itemId:long}")]
    [Auditable("Invoice", "RemoveItem")]
    public async Task<ActionResult<RemoveInvoiceItemResponse>> RemoveItem(long id, long itemId)
    {
        var (userId, isAdmin, _, _) = GetUserContext();
        var result = await _invoiceService.RemoveItemAsync(id, itemId, userId, isAdmin);
        return result == null ? Forbid() : Ok(result);
    }

    [HttpPatch("{id:long}/status")]
    [Auditable("Invoice", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateInvoiceStatusRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        // Get invoice info for notification
        var invoice = await _invoiceService.GetByIdAsync(id, userId, isAdmin);
        if (invoice == null) return NotFound();

        bool success;
        try
        {
            success = await _invoiceService.UpdateStatusAsync(id, request.Status, userId, isAdmin, request.AutoFinalize);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        if (!success) return BadRequest(new { message = "Status change not allowed." });

        // Create notifications
        if (request.Status == "Finish")
        {
            // Notify the invoice owner that the invoice is finished/paid
            var ownerUserId = await _db.Set<Invoice>()
                .Where(i => i.Id == id)
                .Select(i => i.Quote.UserId)
                .FirstOrDefaultAsync();
            if (ownerUserId > 0)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = ownerUserId,
                    Type = "StatusChange",
                    EntityName = "Invoice",
                    EntityId = id,
                    EntityNumber = invoice.InvoiceNumber,
                    Message = $"Proforma Invoice {invoice.InvoiceNumber} has been marked as Finished."
                });
                await _db.SaveChangesAsync();
            }
        }
        else if (request.Status == "Pending")
        {
            // Notify admins
            var adminIds = await _db.Set<User>().Where(u => (u.Role == "Admin" || u.Role == "SuperAdmin") && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var aid in adminIds)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = aid,
                    Type = "PendingApproval",
                    EntityName = "Invoice",
                    EntityId = id,
                    EntityNumber = invoice.InvoiceNumber,
                    Message = $"Proforma Invoice {invoice.InvoiceNumber} is pending approval."
                });
            }
            await _db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Invoice", "Cancel")]
    public async Task<IActionResult> Cancel(long id)
    {
        if (await _lockGuard.IsInvoiceLocked(id))
            return BadRequest(new { message = "This Proforma Invoice is locked because a Final Invoice has been created." });

        var success = await _invoiceService.CancelAsync(id);
        return success ? Ok() : NotFound();
    }

    [HttpPatch("{id:long}/default-wallet")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> SetDefaultWallet(long id, [FromBody] SetDefaultWalletRequest request)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return NotFound();

        PaymentBox? wallet = null;
        if (request.WalletId.HasValue)
        {
            wallet = await _db.Set<PaymentBox>()
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == request.WalletId.Value);
            if (wallet == null)
                return BadRequest(new { message = "The selected deposit wallet does not exist." });
        }

        invoice.DefaultDepositWalletId = request.WalletId;
        await _db.SaveChangesAsync();
        return Ok(new
        {
            walletId = invoice.DefaultDepositWalletId,
            walletName = wallet?.Name,
            currency = wallet?.Currency,
        });
    }

    [HttpPatch("{id:long}/default-bank-account")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> SetDefaultBankAccount(long id, [FromBody] SetDefaultBankAccountRequest request)
    {
        var invoice = await _db.Set<Invoice>().FindAsync(id);
        if (invoice == null) return NotFound();
        invoice.DefaultBankAccountId = request.BankAccountId;
        await _db.SaveChangesAsync();
        return Ok(new { bankAccountId = invoice.DefaultBankAccountId });
    }

    [HttpGet("{id:long}/prepayment-check")]
    public async Task<IActionResult> GetPrepaymentCheck(long id)
    {
        var result = await _invoiceService.GetPrepaymentCheckAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("permissions")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Invoice", "GrantPermissions", CaptureBody = true)]
    public async Task<IActionResult> GrantPermissions([FromBody] GrantPermissionRequest request)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();
        if (!isAdmin) return Forbid();

        var success = await _invoiceService.GrantPermissionsAsync(request.InvoiceIds, request.TargetUserId, request.Permission);
        return success ? Ok() : BadRequest("Failed to grant permissions.");
    }

    /// <summary>
    /// Replace PI and per-item assignments. An item with no direct users inherits the PI;
    /// a PI with no direct users inherits its source RFQ/Quote contributors.
    /// </summary>
    [HttpPut("{id:long}/assignments")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Auditable("Invoice", "UpdateAssignments", CaptureBody = true)]
    public async Task<ActionResult<InvoiceResponse>> UpdateAssignments(long id, [FromBody] UpdateInvoiceAssignmentsRequest request)
    {
        var invoice = await _db.Set<Invoice>()
            .Include(item => item.InvoiceItems)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (invoice == null) return NotFound();

        var invoiceItemIds = invoice.InvoiceItems.Select(item => item.Id).ToHashSet();
        if (request.Items.Any(item => !invoiceItemIds.Contains(item.InvoiceItemId)))
            return BadRequest(new { message = "One or more assignment rows do not belong to this PI." });

        var requestedUserIds = request.InvoiceUserIds
            .Concat(request.Items.SelectMany(item => item.UserIds))
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();
        var validUserIds = requestedUserIds.Count == 0
            ? new HashSet<long>()
            : (await _db.Set<User>()
                .Where(user => requestedUserIds.Contains(user.Id) && user.IsActive)
                .Select(user => user.Id)
                .ToListAsync()).ToHashSet();
        if (validUserIds.Count != requestedUserIds.Count)
            return BadRequest(new { message = "One or more selected users are missing or inactive." });

        var itemIdStrings = invoiceItemIds.Select(itemId => itemId.ToString()).ToList();
        var oldAssignments = await _db.Set<EntityPermission>()
            .Where(permission =>
                (permission.EntityName == "Invoice" && permission.EntityId == id.ToString())
                || (permission.EntityName == "InvoiceItem" && itemIdStrings.Contains(permission.EntityId)))
            .ToListAsync();
        _db.Set<EntityPermission>().RemoveRange(oldAssignments);

        void AddAssignment(long userId, string entityName, long entityId) => _db.Set<EntityPermission>().Add(new EntityPermission
        {
            UserId = userId,
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Permission = "Edit",
            CreatedAt = DateTime.UtcNow,
        });

        foreach (var assignedUserId in request.InvoiceUserIds.Distinct())
            AddAssignment(assignedUserId, "Invoice", id);
        foreach (var item in request.Items)
            foreach (var assignedUserId in item.UserIds.Distinct())
                AddAssignment(assignedUserId, "InvoiceItem", item.InvoiceItemId);
        await _db.SaveChangesAsync();

        var (callerUserId, _, _, _) = GetUserContext();
        var response = await _invoiceService.GetByIdAsync(id, callerUserId, true);
        if (response == null) return NotFound();

        // Procurement permissions are the gate used by PO creation. Keep them aligned
        // with the effective item assignment so the assigned expert can purchase the part.
        var procurementItems = await _db.Set<Procument.Module.Purchasing.Entities.ProcurementItem>()
            .Where(item => invoiceItemIds.Contains(item.SourceInvoiceItemId))
            .Select(item => new { item.Id, item.SourceInvoiceItemId })
            .ToListAsync();
        if (procurementItems.Count > 0)
        {
            var procurementIdStrings = procurementItems.Select(item => item.Id.ToString()).ToList();
            var oldProcurementAssignments = await _db.Set<EntityPermission>()
                .Where(permission => permission.EntityName == "Procurement" && procurementIdStrings.Contains(permission.EntityId))
                .ToListAsync();
            _db.Set<EntityPermission>().RemoveRange(oldProcurementAssignments);

            var effectiveByItem = response.Items.ToDictionary(
                item => item.Id,
                item => item.AssignedUsers.Select(user => user.Id).Distinct().ToList());
            foreach (var procurementItem in procurementItems)
                if (effectiveByItem.TryGetValue(procurementItem.SourceInvoiceItemId, out var effectiveUsers))
                    foreach (var effectiveUserId in effectiveUsers)
                        AddAssignment(effectiveUserId, "Procurement", procurementItem.Id);
            await _db.SaveChangesAsync();
        }

        return Ok(response);
    }

    /// <summary>
    /// Cascading filter options for the Sales Order list. Each column is computed from the
    /// rows that survive the *other* active filters, so the second and third filter a user
    /// opens only offer values that still return rows. Called with no query string it
    /// returns the full lists, which the client caches behind the "Show all" toggle.
    /// </summary>
    [HttpGet("filter-options")]
    public async Task<ActionResult> GetInvoiceFilterOptions(
        [FromQuery] string? search = null,
        [FromQuery] string? pnSearch = null,
        [FromQuery] List<string>? statuses = null,
        [FromQuery] List<string>? customerCodes = null,
        [FromQuery] List<string>? invoiceNumbers = null,
        [FromQuery] List<string>? subjects = null,
        [FromQuery] List<string>? assignedUsers = null,
        [FromQuery] List<int>? bases = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetUserContext();

        IQueryable<Invoice> permitted = _db.Set<Invoice>().AsNoTracking();

        if (!isSuperAdmin && userBases != null)
        {
            var permittedIds = (await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Invoice")
                .Select(p => p.EntityId).ToListAsync())
                .Select(id => long.TryParse(id, out var l) ? l : -1).ToList();
            var permittedItemIds = (await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "InvoiceItem")
                .Select(p => p.EntityId).ToListAsync())
                .Select(id => long.TryParse(id, out var l) ? l : -1).ToList();
            var sourcePermissions = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Quote" || p.EntityName == "RFQ"))
                .Select(p => new { p.EntityName, p.EntityId }).ToListAsync();
            var permittedQuoteIds = sourcePermissions.Where(p => p.EntityName == "Quote")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();
            var permittedRfqIds = sourcePermissions.Where(p => p.EntityName == "RFQ")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();

            permitted = permitted.Where(i =>
                i.Customer == null ||
                i.Customer.Base == null ||
                userBases.Contains(i.Customer.Base.Value) ||
                permittedIds.Contains(i.Id) ||
                i.InvoiceItems.Any(item => permittedItemIds.Contains(item.Id)
                    || (item.QuoteItem != null && (item.QuoteItem.Quote.UserId == userId
                        || item.QuoteItem.Quote.RFQ.UserId == userId
                        || permittedQuoteIds.Contains(item.QuoteItem.QuoteId)
                        || permittedRfqIds.Contains(item.QuoteItem.Quote.RFQId)))));
        }
        else if (!isAdmin)
        {
            var permittedIds = (await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Invoice")
                .Select(p => p.EntityId).ToListAsync())
                .Select(id => long.TryParse(id, out var l) ? l : -1).ToList();
            var permittedItemIds = (await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "InvoiceItem")
                .Select(p => p.EntityId).ToListAsync())
                .Select(id => long.TryParse(id, out var l) ? l : -1).ToList();
            var sourcePermissions = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Quote" || p.EntityName == "RFQ"))
                .Select(p => new { p.EntityName, p.EntityId }).ToListAsync();
            var permittedQuoteIds = sourcePermissions.Where(p => p.EntityName == "Quote")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();
            var permittedRfqIds = sourcePermissions.Where(p => p.EntityName == "RFQ")
                .Select(p => long.TryParse(p.EntityId, out var id) ? id : -1).Where(id => id > 0).ToList();

            permitted = permitted.Where(i => permittedIds.Contains(i.Id)
                || i.InvoiceItems.Any(item => permittedItemIds.Contains(item.Id)
                    || (item.QuoteItem != null && (item.QuoteItem.Quote.UserId == userId
                        || item.QuoteItem.Quote.RFQ.UserId == userId
                        || permittedQuoteIds.Contains(item.QuoteItem.QuoteId)
                        || permittedRfqIds.Contains(item.QuoteItem.Quote.RFQId)))));
        }

        var statusValues = (statuses ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var codeValues = (customerCodes ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var numberValues = (invoiceNumbers ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var subjectValues = (subjects ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var assignedUserValues = (assignedUsers ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var assignedUserIds = assignedUserValues.Count == 0
            ? new List<long>()
            : await _db.Set<User>().Where(user => assignedUserValues.Contains(user.Name)).Select(user => user.Id).ToListAsync();
        var baseValues = bases ?? new List<int>();

        IQueryable<Invoice> Build(string? exclude)
        {
            var q = permitted;

            // Cancelled rows stay hidden unless the status filter explicitly asks for them.
            bool cancelledRequested = exclude != "status" && statusValues.Contains("Cancelled");
            if (!cancelledRequested)
                q = q.Where(i => !i.IsCancelled);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(i =>
                    i.InvoiceNumber.Contains(s) ||
                    i.Customer.Name.Contains(s) ||
                    (i.Customer.CustomerCode != null && i.Customer.CustomerCode.Contains(s)) ||
                    (i.Subject != null && i.Subject.Contains(s)) ||
                    (i.CustomerPONumber != null && i.CustomerPONumber.Contains(s)) ||
                    i.Status.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(pnSearch))
            {
                var s = pnSearch.Trim();
                q = q.Where(i => i.InvoiceItems.Any(ii =>
                    (ii.QuoteItem != null && ii.QuoteItem.PartNumber != null && ii.QuoteItem.PartNumber.Name.Contains(s)) ||
                    (ii.QuoteItem != null && ii.QuoteItem.Alt != null && ii.QuoteItem.Alt.Contains(s))));
            }

            if (exclude != "status" && statusValues.Count > 0)
                q = q.Where(i => statusValues.Contains(i.Status));

            if (exclude != "customerCode" && codeValues.Count > 0)
            {
                var hasNullPlaceholder = codeValues.Contains("-") || codeValues.Contains("—");
                q = q.Where(i =>
                    codeValues.Contains(i.Customer.CustomerCode) ||
                    (hasNullPlaceholder && (i.Customer.CustomerCode == null || i.Customer.CustomerCode == "")));
            }

            if (exclude != "invoiceNumber" && numberValues.Count > 0)
                q = q.Where(i => numberValues.Contains(i.InvoiceNumber));

            if (exclude != "subject" && subjectValues.Count > 0)
                q = q.Where(i => i.Subject != null && subjectValues.Contains(i.Subject));

            if (exclude != "assignedUsers" && assignedUserValues.Count > 0)
                q = q.Where(invoice =>
                    _db.Set<EntityPermission>().Any(permission => permission.EntityName == "Invoice"
                        && permission.EntityId == invoice.Id.ToString()
                        && assignedUserIds.Contains(permission.UserId))
                    || (!_db.Set<EntityPermission>().Any(permission => permission.EntityName == "Invoice"
                            && permission.EntityId == invoice.Id.ToString())
                        && invoice.InvoiceItems.Any(item => item.QuoteItem != null
                            && (assignedUserIds.Contains(item.QuoteItem.Quote.UserId)
                                || (item.QuoteItem.Quote.RFQ.UserId.HasValue && assignedUserIds.Contains(item.QuoteItem.Quote.RFQ.UserId.Value))
                                || _db.Set<EntityPermission>().Any(permission => assignedUserIds.Contains(permission.UserId)
                                    && ((permission.EntityName == "Quote" && permission.EntityId == item.QuoteItem.QuoteId.ToString())
                                        || (permission.EntityName == "RFQ" && permission.EntityId == item.QuoteItem.Quote.RFQId.ToString())))))));

            if (exclude != "customerBase" && baseValues.Count > 0)
                q = q.Where(i => i.Customer != null && i.Customer.Base != null && baseValues.Contains(i.Customer.Base.Value));

            if (createdFrom.HasValue)
                q = q.Where(i => i.CreatedAt >= createdFrom.Value);

            if (createdTo.HasValue)
                q = q.Where(i => i.CreatedAt <= createdTo.Value.AddDays(1).AddTicks(-1));

            return q;
        }

        var availableStatuses = await Build("status")
            .Select(i => i.Status ?? "Draft").Distinct().OrderBy(s => s).ToListAsync();

        var availableCustomers = await Build("customerCode")
            .Where(i => i.Customer != null)
            .Select(i => new { code = i.Customer!.CustomerCode, name = i.Customer!.Name })
            .Distinct().ToListAsync();

        var availableNumbers = await Build("invoiceNumber")
            .Select(i => i.InvoiceNumber).Distinct().OrderBy(n => n).ToListAsync();

        var availableSubjects = await Build("subject")
            .Where(i => i.Subject != null && i.Subject != "")
            .Select(i => i.Subject!).Distinct().OrderBy(s => s).ToListAsync();

        var availableBases = await Build("customerBase")
            .Where(i => i.Customer != null && i.Customer.Base != null)
            .Select(i => i.Customer!.Base!.Value).Distinct().OrderBy(b => b).ToListAsync();

        var assignmentInvoiceIds = await Build("assignedUsers").Select(invoice => invoice.Id).ToListAsync();
        var assignmentInvoiceIdStrings = assignmentInvoiceIds.Select(id => id.ToString()).ToList();
        var directAssignmentRows = await _db.Set<EntityPermission>()
            .Where(permission => permission.EntityName == "Invoice" && assignmentInvoiceIdStrings.Contains(permission.EntityId))
            .Select(permission => new { permission.EntityId, permission.UserId })
            .ToListAsync();
        var directInvoiceIds = directAssignmentRows.Select(row => row.EntityId).ToHashSet();
        var fallbackSources = await _db.Set<InvoiceItem>()
            .Where(item => assignmentInvoiceIds.Contains(item.InvoiceId)
                && !directInvoiceIds.Contains(item.InvoiceId.ToString())
                && item.QuoteItem != null)
            .Select(item => new
            {
                item.InvoiceId,
                QuoteId = item.QuoteItem!.QuoteId,
                QuoteOwnerId = item.QuoteItem.Quote.UserId,
                RfqId = item.QuoteItem.Quote.RFQId,
                RfqOwnerId = item.QuoteItem.Quote.RFQ.UserId,
            })
            .ToListAsync();
        var fallbackQuoteIds = fallbackSources.Select(source => source.QuoteId.ToString()).Distinct().ToList();
        var fallbackRfqIds = fallbackSources.Select(source => source.RfqId.ToString()).Distinct().ToList();
        var fallbackPermissionUserIds = await _db.Set<EntityPermission>()
            .Where(permission =>
                (permission.EntityName == "Quote" && fallbackQuoteIds.Contains(permission.EntityId))
                || (permission.EntityName == "RFQ" && fallbackRfqIds.Contains(permission.EntityId)))
            .Select(permission => permission.UserId)
            .ToListAsync();
        var effectiveAssignmentUserIds = directAssignmentRows.Select(row => row.UserId)
            .Concat(fallbackSources.Select(source => source.QuoteOwnerId))
            .Concat(fallbackSources.Where(source => source.RfqOwnerId.HasValue).Select(source => source.RfqOwnerId!.Value))
            .Concat(fallbackPermissionUserIds)
            .Distinct()
            .ToList();
        var availableAssignedUsers = await _db.Set<User>()
            .Where(user => effectiveAssignmentUserIds.Contains(user.Id))
            .Select(user => user.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        return Ok(new
        {
            statuses = availableStatuses,
            customers = availableCustomers.GroupBy(c => c.code).Select(g => g.First()).OrderBy(c => c.code).ToList(),
            invoiceNumbers = availableNumbers,
            subjects = availableSubjects,
            assignedUsers = availableAssignedUsers,
            bases = availableBases
        });
    }

    private (long userId, bool isAdmin, bool isSuperAdmin, int[] userBases) GetUserContext()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        long userId = 0;
        if (idClaim != null && long.TryParse(idClaim.Value, out var id))
            userId = id;
        bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        bool isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        return (userId, isAdmin, isSuperAdmin, userBases);
    }
}
