using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Identity.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;
using System.Security.Claims;

namespace Procument.Module.Sales.Controllers;

/// <summary>
/// Total P/N (TPP) report — flat denormalized view of every POItem in the system,
/// joined across PO + Procurement + Invoice + Quote + FinalInvoice + CustomerPayments.
/// Mirrors the columns of the user's totalpn.xlsx.
/// </summary>
[ApiController]
[Route("api/po-items/total-pn")]
[Authorize]
public class TotalPNController : ControllerBase
{
    private const string TotalPnMenuFeature = "totalPnMenu";
    private const string HiddenColumnFeaturePrefix = "totalPnHiddenColumn:";
    private static readonly IReadOnlyDictionary<string, string> TotalPnColumns = new Dictionary<string, string>
    {
        ["poNumber"] = "PO#", ["poRef"] = "PO Ref#", ["experts"] = "Expert",
        ["customer"] = "Customer", ["supplier"] = "Supplier", ["partNumber"] = "P/N",
        ["description"] = "Description", ["qty"] = "QTY", ["condition"] = "CD",
        ["priority"] = "Priority", ["warehouse"] = "Warehouse", ["serialNumber"] = "SN#",
        ["customerInvoiceNumber"] = "PI# to Customer",
        ["purchasingUnitPriceUsd"] = "Purchasing Unit Price (USD)",
        ["purchasingTotalPriceUsd"] = "Purchasing Total Price (USD)",
        ["supplierDeliveryTime"] = "Supplier Delivery Time", ["status"] = "Status",
        ["sellingUnitPriceUsd"] = "Selling Unit Price (USD)",
        ["sellingTotalPriceUsd"] = "Selling Total Price (USD)",
        ["sellingUnitPriceYuan"] = "Selling Unit Price (Yuan)",
        ["sellingTotalPriceYuan"] = "Selling Total Price (Yuan)", ["poDate"] = "PO Date",
        ["invDate"] = "INV Date", ["received"] = "Received", ["receivedDate"] = "Received Date",
        ["paymentTerm"] = "Payment Term", ["customerDeliveryTime"] = "Customer Delivery Time",
        ["rate"] = "Rate", ["trackNumbers"] = "Track#", ["shippingStatus"] = "Shipping Status",
        ["shippingCost"] = "Shipping Cost", ["note"] = "NOTE 02"
    };

    private readonly ITotalPNService _service;
    private readonly DbContext _db;
    public TotalPNController(ITotalPNService service, DbContext db)
    {
        _service = service;
        _db = db;
    }

    private (long userId, bool isAdmin, bool isSuperAdmin, int[] userBases) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        return (userId, isAdmin, isSuperAdmin, userBases);
    }

    /// <summary>
    /// Paged Total P/N rows. For non-SuperAdmins, item assignments override PI assignments;
    /// RFQ ownership and downstream permissions are used only when no direct assignment exists.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TotalPNRowResponse>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        [FromQuery] List<string>? customers = null,
        [FromQuery] List<string>? invoiceNumbers = null,
        [FromQuery] List<string>? partNumbers = null,
        [FromQuery] List<string>? conditions = null,
        [FromQuery] List<string>? poNumbers = null,
        [FromQuery] List<string>? suppliers = null,
        [FromQuery] List<string>? paymentTerms = null,
        [FromQuery] List<string>? poStatuses = null,
        [FromQuery] List<string>? shippingStatuses = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        if (!await CanAccessTotalPnAsync(isSuperAdmin)) return NotFound();
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var result = await _service.GetAsync(pq, userId, isAdmin, sortBy, sortDesc, isSuperAdmin, userBases,
            customers, invoiceNumbers, partNumbers, conditions, poNumbers, suppliers, paymentTerms, poStatuses, shippingStatuses);
        return Ok(result);
    }

    /// <summary>Returns unique values for each filterable column — used to populate filter dropdowns.</summary>
    [HttpGet("filter-options")]
    public async Task<ActionResult<TotalPNFilterOptions>> GetFilterOptions(
        [FromQuery] List<string>? customers = null,
        [FromQuery] List<string>? invoiceNumbers = null,
        [FromQuery] List<string>? partNumbers = null,
        [FromQuery] List<string>? conditions = null,
        [FromQuery] List<string>? poNumbers = null,
        [FromQuery] List<string>? suppliers = null,
        [FromQuery] List<string>? paymentTerms = null,
        [FromQuery] List<string>? poStatuses = null,
        [FromQuery] List<string>? shippingStatuses = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        if (!await CanAccessTotalPnAsync(isSuperAdmin)) return NotFound();
        var result = await _service.GetFilterOptionsAsync(userId, isAdmin, isSuperAdmin, userBases,
            customers, invoiceNumbers, partNumbers, conditions, poNumbers, suppliers, paymentTerms, poStatuses, shippingStatuses);
        return Ok(result);
    }

    /// <summary>Total Order view — one row per POItem that has at least one Track Number, with SN/TID/AWB data.</summary>
    [HttpGet("/api/po-items/total-order")]
    public async Task<ActionResult<PagedResult<TotalPNRowResponse>>> GetTotalOrder(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var result = await _service.GetTotalOrderAsync(pq, userId, isAdmin, isSuperAdmin, userBases);
        return Ok(result);
    }

    /// <summary>Inline edit — sets POItem.Status and/or POItem.Note from the Total P/N grid.</summary>
    [HttpPatch("{poItemId:long}")]
    [Auditable("POItem", "UpdateTotalPN", CaptureBody = true)]
    public async Task<IActionResult> Update(long poItemId, [FromBody] UpdatePOItemTotalPNRequest request)
    {
        var (_, _, isSuperAdmin, _) = GetCurrentUser();
        if (!await CanAccessTotalPnAsync(isSuperAdmin)) return NotFound();
        var ok = await _service.UpdateAsync(poItemId, request);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Column keys that the current user is allowed to know about and display.</summary>
    [HttpGet("columns")]
    public async Task<ActionResult<List<string>>> GetVisibleColumns()
    {
        var (_, _, isSuperAdmin, _) = GetCurrentUser();
        if (!await CanAccessTotalPnAsync(isSuperAdmin)) return NotFound();
        if (isSuperAdmin) return Ok(TotalPnColumns.Keys.ToList());

        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var hidden = await _db.Set<MenuPermission>()
            .Where(permission => permission.UserName == userName && permission.Feature.StartsWith(HiddenColumnFeaturePrefix))
            .Select(permission => permission.Feature)
            .ToListAsync();
        var hiddenKeys = hidden.Select(feature => feature[HiddenColumnFeaturePrefix.Length..]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Ok(TotalPnColumns.Keys.Where(key => !hiddenKeys.Contains(key)).ToList());
    }

    /// <summary>SuperAdmin editor data for one user's Total P/N columns.</summary>
    [HttpGet("column-access/{targetUserId:long}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<TotalPnColumnAccessResponse>> GetColumnAccess(long targetUserId)
    {
        var target = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(user => user.Id == targetUserId);
        if (target == null) return NotFound();

        var hidden = (await _db.Set<MenuPermission>()
            .Where(permission => permission.UserName == target.Name && permission.Feature.StartsWith(HiddenColumnFeaturePrefix))
            .Select(permission => permission.Feature)
            .ToListAsync())
            .Select(feature => feature[HiddenColumnFeaturePrefix.Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Ok(new TotalPnColumnAccessResponse
        {
            UserId = target.Id,
            UserName = target.Name,
            Columns = TotalPnColumns.Select(column => new TotalPnColumnOption
            {
                Key = column.Key,
                Label = column.Value,
                IsVisible = !hidden.Contains(column.Key)
            }).ToList()
        });
    }

    /// <summary>Replace one user's visible Total P/N column set.</summary>
    [HttpPut("column-access/{targetUserId:long}")]
    [Authorize(Roles = "SuperAdmin")]
    [Auditable("MenuPermission", "UpdateTotalPnColumns", CaptureBody = true)]
    public async Task<IActionResult> UpdateColumnAccess(long targetUserId, [FromBody] UpdateTotalPnColumnAccessRequest request)
    {
        var target = await _db.Set<User>().FirstOrDefaultAsync(user => user.Id == targetUserId);
        if (target == null) return NotFound();

        var visible = request.VisibleColumns
            .Where(TotalPnColumns.ContainsKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await _db.Set<MenuPermission>()
            .Where(permission => permission.UserName == target.Name && permission.Feature.StartsWith(HiddenColumnFeaturePrefix))
            .ToListAsync();
        _db.Set<MenuPermission>().RemoveRange(existing);

        foreach (var key in TotalPnColumns.Keys.Where(key => !visible.Contains(key)))
        {
            _db.Set<MenuPermission>().Add(new MenuPermission
            {
                UserName = target.Name,
                Feature = HiddenColumnFeaturePrefix + key
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> CanAccessTotalPnAsync(bool isSuperAdmin)
    {
        if (isSuperAdmin) return true;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value;
        return !string.IsNullOrWhiteSpace(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(permission => permission.Feature == TotalPnMenuFeature && permission.UserName == userName);
    }
}

public class TotalPnColumnAccessResponse
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public List<TotalPnColumnOption> Columns { get; set; } = [];
}

public class TotalPnColumnOption
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
}

public class UpdateTotalPnColumnAccessRequest
{
    public List<string> VisibleColumns { get; set; } = [];
}
