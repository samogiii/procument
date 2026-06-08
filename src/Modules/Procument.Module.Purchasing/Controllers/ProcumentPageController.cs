using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;
using Procument.Module.RFQ.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/procument-page")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class ProcumentPageController : ControllerBase
{
    private readonly IProcumentPageService _service;
    private readonly DbContext _db;

    public ProcumentPageController(IProcumentPageService service, DbContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>Get all RFQ items with their supplier quotes for the Procument page (paginated).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcumentPageItemResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null,
        [FromQuery] List<string>? status = null, [FromQuery] List<string>? customerSearch = null,
        [FromQuery] List<long>? userIds = null, [FromQuery] string? pnSearch = null,
        [FromQuery] bool pendingOnly = false,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        [FromQuery] List<string>? conditions = null,
        [FromQuery] List<string>? colPartNames = null,
        [FromQuery] List<string>? customerCodes = null,
        [FromQuery] List<long>? rfqIds = null,
        [FromQuery] List<string>? rfqNames = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var (userId, isSuperAdmin, userBases) = GetUserContext();
        var result = await _service.GetAllItemsAsync(userId, isSuperAdmin, userBases, pq, status, customerSearch, userIds, pnSearch, pendingOnly, sortBy, sortDesc, conditions, colPartNames, customerCodes, rfqIds, rfqNames);
        return Ok(result);
    }

    /// <summary>Distinct filter options for the Procument page — cascades with active filters.</summary>
    [HttpGet("filter-options")]
    public async Task<ActionResult> GetFilterOptions(
        [FromQuery] List<string>? statuses = null,
        [FromQuery] List<long>? userIds = null,
        [FromQuery] List<string>? customerSearch = null)
    {
        var (userId, isSuperAdmin, userBases) = GetUserContext();

        IQueryable<RFQItem> query = _db.Set<RFQItem>()
            .AsNoTracking()
            .Include(i => i.RFQ).ThenInclude(r => r.Customer);

        if (!isSuperAdmin)
        {
            var permittedIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "RFQ")
                .Select(p => p.EntityId).ToListAsync();
            var permittedIds = permittedIdsStr.Select(id => long.TryParse(id, out var l) ? l : -1L).Where(l => l > 0).ToList();
            query = query.Where(i =>
                i.RFQ.Customer.Base == null ||
                userBases.Contains(i.RFQ.Customer.Base.Value) ||
                permittedIds.Contains(i.RFQId) ||
                i.RFQ.UserId == userId);
        }

        if (statuses?.Count > 0)
            query = query.Where(i => statuses.Contains(i.RFQ.Status ?? "Open"));

        if (customerSearch?.Count > 0)
        {
            var customers = customerSearch.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (customers.Count > 0)
            {
                var hasNullPlaceholder = customers.Contains("-") || customers.Contains("—");
                query = query.Where(i => 
                    customers.Contains(i.RFQ.Customer.Name) || 
                    (i.RFQ.Customer.CustomerCode != null && customers.Contains(i.RFQ.Customer.CustomerCode)) ||
                    (hasNullPlaceholder && (i.RFQ.Customer.CustomerCode == null || i.RFQ.Customer.CustomerCode == "")));
            }
        }

        if (userIds?.Count > 0)
        {
            var rfqIdStrs = await _db.Set<EntityPermission>()
                .Where(p => p.EntityName == "RFQ" && userIds.Contains(p.UserId))
                .Select(p => p.EntityId).ToListAsync();
            var assignedIds = rfqIdStrs.Select(id => long.TryParse(id, out var l) ? l : -1L).Where(l => l > 0).ToList();
            query = query.Where(i => assignedIds.Contains(i.RFQId));
        }

        var rfqIdList = await query.Select(i => i.RFQId.ToString()).Distinct().ToListAsync();

        var availableStatuses = await query
            .Select(i => i.RFQ.Status ?? "Open").Distinct().ToListAsync();

        var availableCustomers = await query
            .Select(i => new { name = i.RFQ.Customer.Name, code = i.RFQ.Customer.CustomerCode })
            .Distinct().ToListAsync();

        var availableUsers = await _db.Set<EntityPermission>()
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.EntityName == "RFQ" && rfqIdList.Contains(p.EntityId))
            .Select(p => new { id = p.User.Id, name = p.User.Name })
            .Distinct().ToListAsync();

        return Ok(new
        {
            statuses = availableStatuses,
            customers = availableCustomers.GroupBy(c => c.name).Select(g => g.First()).ToList(),
            users = availableUsers.GroupBy(u => u.id).Select(g => g.First()).ToList(),
        });
    }

    /// <summary>Get supplier suggestions for a part number based on history.</summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<SupplierSuggestionsResponse>> GetSuggestions(
        [FromQuery] long partNumberId, [FromQuery] long rfqId)
    {
        var result = await _service.GetSuggestionsAsync(partNumberId, rfqId);
        return Ok(result);
    }

    private (long userId, bool isSuperAdmin, int[] userBases) GetUserContext()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        long userId = 0;
        if (idClaim != null && long.TryParse(idClaim.Value, out var id))
            userId = id;
        bool isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        return (userId, isSuperAdmin, userBases);
    }
}
