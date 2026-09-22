using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Data;
using System.Security.Claims;

namespace Procument.API.Controllers;

/// <summary>
/// Dashboard controller — returns aggregated stats + chart data.
/// Admins see global stats; users see only their scoped data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get([FromQuery] long? userId = null)
    {
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var currentUserId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();

        // Determine effective scope:
        // - SuperAdmin with no filter → global (all users/bases)
        // - Admin/SuperAdmin with userId → scoped to that user
        // - Admin (non-super) with no filter → scoped to their bases
        // - Non-admin → always scoped to self
        long? scopeUserId = null;
        if (!isAdmin)
            scopeUserId = currentUserId;
        else if (userId.HasValue && userId.Value > 0)
            scopeUserId = userId.Value;

        // Build scoped queryables
        var rfqs = _db.RFQs.AsQueryable();
        var quotes = _db.Quotes.AsQueryable();
        var invoices = _db.Invoices.AsQueryable();
        var pos = _db.PurchaseOrders.AsQueryable();

        // Base filter for non-SuperAdmin admin users
        if (!isSuperAdmin && isAdmin && userBases.Length > 0)
        {
            rfqs = rfqs.Where(r => r.Customer.Base == null || userBases.Contains(r.Customer.Base.Value));
            quotes = quotes.Where(q => q.Customer.Base == null || userBases.Contains(q.Customer.Base.Value));
            invoices = invoices.Where(i => i.Customer.Base == null || userBases.Contains(i.Customer.Base.Value));
            var allowedInvoiceIds = _db.Invoices
                .Where(i => i.Customer.Base == null || userBases.Contains(i.Customer.Base.Value))
                .Select(i => i.Id);
            pos = pos.Where(p => p.InvoiceId == null || allowedInvoiceIds.Contains(p.InvoiceId.Value));
        }

        if (scopeUserId.HasValue)
        {
            var uid = scopeUserId.Value;
            rfqs = rfqs.Where(r => r.UserId == uid);
            quotes = quotes.Where(q => q.UserId == uid);
            invoices = invoices.Where(i => i.Quote.UserId == uid);
            // POs scoped via ProcumentRecord.UserId
            var scopedPOIds = _db.POItems
                .Where(pi => pi.ProcumentRecord != null && pi.ProcumentRecord.UserId == uid)
                .Select(pi => pi.POId).Distinct();
            pos = pos.Where(p => scopedPOIds.Contains(p.Id));
        }

        // ── Core counts ──
        var totalRfqs = await rfqs.CountAsync();
        var totalQuotes = await quotes.CountAsync();
        var pendingQuotes = await quotes.CountAsync(q => q.Status == "Draft");
        var totalUsers = await _db.Users.CountAsync();
        var rfqIds = rfqs.Select(r => r.Id);
        var pendingRfqs = await rfqs.Where(r => !_db.Quotes.Any(q => q.RFQId == r.Id)).CountAsync();

        // ── PO stats ──
        var totalPOs = await pos.CountAsync();
        var totalPOValue = await pos.SumAsync(p => p.TotalAmount ?? 0);
        var acceptedPOs = await pos.CountAsync(p => p.Status == "Completed" || p.Status == "Accept" || p.Status == "Payment Done");

        // ── Invoice stats ──
        var totalInvoices = await invoices.CountAsync();
        var totalInvoiceValue = await invoices.SumAsync(i => i.TotalAmount);
        var paidInvoiceValue = await invoices.Where(i => i.Status == "Paid").SumAsync(i => i.TotalAmount);

        // ── Quote revenue ──
        var totalQuoteValue = await quotes.Where(q => q.Status == "Sent").SumAsync(q => q.TotalAmount ?? 0);
        var acceptedQuoteValue = await quotes.Where(q => q.Status == "Accepted").SumAsync(q => q.TotalAmount ?? 0);

        // ── RFQ count for the scoped user ──
        int rfqCount = 0;
        if (scopeUserId.HasValue)
        {
            rfqCount = await _db.EntityPermissions
                .Where(p => p.EntityName == "RFQ" && p.UserId == scopeUserId.Value)
                .CountAsync();
        }

        // ── PO status distribution ──
        var poStatusDist = await pos
            .GroupBy(p => p.Status)
            .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // ── Quote status distribution ──
        var quoteStatusDist = await quotes
            .GroupBy(q => q.Status)
            .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // ── Invoice status distribution ──
        var invoiceStatusDist = await invoices
            .GroupBy(i => i.Status)
            .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // ── Monthly trends (last 6 months) ──
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var monthlyQuotes = await quotes
            .Where(q => q.Status == "Sent" && q.CreatedAt >= sixMonthsAgo)
            .GroupBy(q => new { q.CreatedAt.Year, q.CreatedAt.Month })
            .Select(g => new MonthlyDataPoint
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count(),
                TotalValue = g.Sum(q => q.TotalAmount ?? 0)
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToListAsync();

        var monthlyPOs = await pos
            .Where(p => p.CreatedAt >= sixMonthsAgo)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new MonthlyDataPoint
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count(),
                TotalValue = g.Sum(p => p.TotalAmount ?? 0)
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToListAsync();

        var monthlyInvoices = await invoices
            .Where(i => i.CreatedAt >= sixMonthsAgo)
            .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
            .Select(g => new MonthlyDataPoint
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count(),
                TotalValue = g.Sum(i => i.TotalAmount)
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToListAsync();

        var response = new DashboardResponse
        {
            TotalRfqs = totalRfqs,
            TotalQuotes = totalQuotes,
            PendingRfqs = pendingRfqs,
            PendingQuotes = pendingQuotes,
            TotalUsers = totalUsers,
            TotalPOs = totalPOs,
            TotalPOValue = totalPOValue,
            AcceptedPOs = acceptedPOs,
            TotalInvoices = totalInvoices,
            TotalInvoiceValue = totalInvoiceValue,
            PaidInvoiceValue = paidInvoiceValue,
            TotalQuoteValue = totalQuoteValue,
            AcceptedQuoteValue = acceptedQuoteValue,
            RFQCount = rfqCount,
            POStatusDistribution = poStatusDist,
            QuoteStatusDistribution = quoteStatusDist,
            InvoiceStatusDistribution = invoiceStatusDist,
            MonthlyQuotes = monthlyQuotes,
            MonthlyPOs = monthlyPOs,
            MonthlyInvoices = monthlyInvoices,
        };

        // ── Admin extras (only when viewing global / all users) ──
        if (isAdmin)
        {
            response.RecentActivity = await _db.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(20)
                .Select(a => new AuditActivityItem
                {
                    Id = a.Id,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    Details = a.Details,
                    UserName = a.UserName,
                    Timestamp = a.Timestamp,
                })
                .ToListAsync();

            // ── Per-user quote stats (always global for the chart) ──
            var allUsers = await _db.Users.Select(u => new { u.Id, u.Name }).ToListAsync();
            
            // Get all quotes grouped by user
            var quoteGroups = await _db.Quotes
                .GroupBy(q => q.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalValue = g.Where(q => q.Status == "Sent" || q.Status == "Accepted").Sum(q => q.TotalAmount ?? 0),
                    TotalCount = g.Count(),
                    AcceptedCount = g.Count(q => q.Status == "Accepted"),
                    RejectedCount = g.Count(q => q.Status == "Rejected")
                })
                .ToListAsync();

            var rfqAssignmentGroups = await _db.EntityPermissions
                .Where(p => p.EntityName == "RFQ")
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToListAsync();

            var userStatsList = allUsers.Select(u => {
                var q = quoteGroups.FirstOrDefault(qg => qg.UserId == u.Id);
                var r = rfqAssignmentGroups.FirstOrDefault(rg => rg.UserId == u.Id);
                return new UserStatItem
                {
                    UserId = u.Id,
                    UserName = u.Name,
                    Count = q?.TotalCount ?? 0,
                    TotalValue = q?.TotalValue ?? 0,
                    AcceptedCount = q?.AcceptedCount ?? 0,
                    RejectedCount = q?.RejectedCount ?? 0,
                    RFQCount = r?.Count ?? 0
                };
            }).ToList();

            response.UserQuoteStats = userStatsList
                .OrderByDescending(u => u.TotalValue)
                .ToList();

            // Top suppliers by PO value (scoped)
            response.TopSuppliers = await pos
                .GroupBy(p => new { p.SupplierId, p.Supplier.Name })
                .Select(g => new SupplierStatItem
                {
                    SupplierName = g.Key.Name,
                    POCount = g.Count(),
                    TotalValue = g.Sum(p => p.TotalAmount ?? 0),
                })
                .OrderByDescending(s => s.TotalValue)
                .Take(10)
                .ToListAsync();
        }

        if (isAdmin && (isSuperAdmin || HasFeature("ourInventoryMenu")))
            response.OurStock = await BuildOurStockStatsAsync();

        return Ok(response);
    }

    private bool HasFeature(string feature)
    {
        var name = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        return _db.MenuPermissions.Any(m => m.Feature == feature && m.UserName == name);
    }

    /// <summary>Stock value, lots below their minimum and value still on order (approved, not yet received).</summary>
    private async Task<OurStockStats> BuildOurStockStatsAsync()
    {
        var closed = new[] { "Draft", "Cancelled", "Returned", "Completed" };
        var incoming = await _db.POItems.AsNoTracking()
            .Where(i => i.ReturnedAt == null && i.PurchaseOrder!.Origin == "Stock" && i.PurchaseOrder.AdminApproval == "Approved"
                && !closed.Contains(i.PurchaseOrder.Status))
            .Select(i => new
            {
                i.Qty, i.UnitPrice,
                Received = _db.OurStockMovements.Where(m => m.POItemId == i.Id && (m.Type == "Receipt" || m.Type == "Adjust"))
                    .Sum(m => (decimal?)m.Qty) ?? 0,
            })
            .ToListAsync();
        return new OurStockStats
        {
            StockValue = await _db.OurStockItems.SumAsync(l => (decimal?)(l.QtyOnHand * l.AvgUnitCost)) ?? 0,
            Lots = await _db.OurStockItems.CountAsync(l => l.QtyOnHand > 0),
            LowStockLots = await _db.OurStockItems.CountAsync(l => l.MinQty != null && l.QtyAvailable < l.MinQty),
            ReservedUnits = await _db.OurStockItems.SumAsync(l => (decimal?)l.QtyReserved) ?? 0,
            IncomingValue = incoming.Sum(i => Math.Max(0, i.Qty - i.Received) * i.UnitPrice),
        };
    }

    /// <summary>
    /// Our Inventory blocks: Stock POs waiting for a PR, Stock POs waiting to be received (Inventory users see
    /// their own warehouses) and lots below their minimum quantity.
    /// </summary>
    private async Task AddOurInventoryAttentionAsync(List<AttentionGroup> groups, bool isAdmin, bool isInventory, long[] inventoryWarehouseIds)
    {
        if (isAdmin)
        {
            var waitingForPr = await _db.PurchaseOrders.AsNoTracking()
                .Where(po => po.Origin == "Stock" && po.AdminApproval == "Approved"
                    && (po.Status == "Waiting For Supplier Documents" || po.Status == "Waiting For PR")
                    && !_db.Set<Procument.Module.Purchasing.Entities.PaymentRequest>().Any(pr => pr.POId == po.Id))
                .OrderBy(po => po.AdminApprovalAt ?? po.CreatedAt).Take(50)
                .Select(po => new { po.Id, po.PONumber, SupplierName = po.Supplier.Name, Since = po.AdminApprovalAt ?? po.CreatedAt, po.Status })
                .ToListAsync();
            AddGroup(groups, "Stock PO Waiting For PR", "warning", "mdi-file-export-outline",
                waitingForPr.Select(po => new AttentionItem
                {
                    Title = po.PONumber, Detail = po.Status, SupplierName = po.SupplierName, Since = po.Since,
                    Route = $"/purchase-orders/{po.Id}", EntityType = "PurchaseOrder", EntityId = po.Id,
                }).ToList());
        }

        if (isAdmin || isInventory)
        {
            var receivable = new[] { "Payment Done", "Waiting For Shipment", "Ship to Warehouse", "Received in Warehouse", "Waiting for Expert Approval Shipment" };
            var q = _db.PurchaseOrders.AsNoTracking()
                .Where(po => po.Origin == "Stock" && po.AdminApproval == "Approved" && receivable.Contains(po.Status));
            if (isInventory && !isAdmin)
                q = q.Where(po => po.DestinationWarehouseId.HasValue && inventoryWarehouseIds.Contains(po.DestinationWarehouseId.Value));
            var awaiting = await q
                .Select(po => new
                {
                    po.Id, po.PONumber, SupplierName = po.Supplier.Name, po.CreatedAt, po.ExpectedDeliveryDate,
                    Warehouse = po.DestinationWarehouse != null ? po.DestinationWarehouse.DisplayName ?? po.DestinationWarehouse.Name : "",
                    Ordered = po.POItems.Where(i => i.ReturnedAt == null).Sum(i => (int?)i.Qty) ?? 0,
                    Received = _db.OurStockMovements.Where(m => m.POId == po.Id && m.POItemId != null && (m.Type == "Receipt" || m.Type == "Adjust"))
                        .Sum(m => (decimal?)m.Qty) ?? 0,
                })
                .Where(x => x.Received < x.Ordered)
                .OrderBy(x => x.ExpectedDeliveryDate ?? x.CreatedAt).Take(50)
                .ToListAsync();
            AddGroup(groups, "Stock PO Waiting For Receipt", "info", "mdi-truck-check-outline",
                awaiting.Select(po => new AttentionItem
                {
                    Title = po.PONumber, SupplierName = po.SupplierName,
                    Detail = $"{po.Received:0.##} of {po.Ordered} received → {po.Warehouse}"
                        + (po.ExpectedDeliveryDate.HasValue ? $" · expected {po.ExpectedDeliveryDate:dd MMM}" : ""),
                    Since = po.ExpectedDeliveryDate ?? po.CreatedAt,
                    Route = $"/purchase-orders/{po.Id}", EntityType = "PurchaseOrder", EntityId = po.Id,
                }).ToList());
        }

        if (isAdmin)
        {
            var low = await _db.OurStockItems.AsNoTracking()
                .Where(l => l.MinQty != null && l.QtyAvailable < l.MinQty)
                .OrderBy(l => l.QtyAvailable - l.MinQty).Take(50)
                .Select(l => new
                {
                    l.Id, l.PartNumber.Name, l.Condition, l.QtyAvailable, l.MinQty, l.UpdatedAt,
                    Warehouse = l.Warehouse.DisplayName ?? l.Warehouse.Name,
                })
                .ToListAsync();
            AddGroup(groups, "Low Stock", "info", "mdi-alert-outline",
                low.Select(l => new AttentionItem
                {
                    Title = $"{l.Name} · {l.Condition}", PartNumbers = [l.Name],
                    Detail = $"{l.QtyAvailable:0.##} available, minimum {l.MinQty:0.##} · {l.Warehouse}",
                    Since = l.UpdatedAt, Route = $"/our-inventory/{l.Id}", EntityType = "OurStockItem", EntityId = l.Id,
                }).ToList());
        }
    }

    private static void AddGroup(List<AttentionGroup> groups, string category, string severity, string icon, List<AttentionItem> items)
    {
        if (items.Count == 0) return;
        foreach (var item in items) { item.Category = category; item.Severity = severity; }
        groups.Add(new AttentionGroup { Category = category, Severity = severity, Icon = icon, Count = items.Count, Items = items });
    }

    /// <summary>
    /// Admin-only: Returns list of users for the dashboard filter dropdown.
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> GetUsers()
    {
        var users = await _db.Users
            .Select(u => new { u.Id, u.Name, u.Role })
            .OrderBy(u => u.Name)
            .ToListAsync();
        return Ok(users);
    }

    /// <summary>
    /// Admin-only: Returns all PO items with full joined data for the "All PO Items" tab.
    /// </summary>
    [HttpGet("po-items")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<POItemFlatRow>>> GetAllPOItems()
    {
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();

        IQueryable<Procument.Module.Purchasing.Entities.POItem> poItemsQ = _db.POItems;
        if (!isSuperAdmin && userBases.Length > 0)
        {
            poItemsQ = poItemsQ.Where(pi =>
                pi.ProcumentRecord == null ||
                pi.ProcumentRecord.RFQItem == null ||
                pi.ProcumentRecord.RFQItem.RFQ.Customer.Base == null ||
                userBases.Contains(pi.ProcumentRecord.RFQItem.RFQ.Customer.Base.Value));
        }

        var rows = await poItemsQ
            .Include(pi => pi.PurchaseOrder).ThenInclude(po => po.Supplier)
            .Include(pi => pi.PartNumber).ThenInclude(pn => pn!.Alternatives)
            .Include(pi => pi.ProcumentRecord).ThenInclude(pr => pr!.RFQItem).ThenInclude(ri => ri.RFQ).ThenInclude(r => r.Customer)
            .Include(pi => pi.ProcumentRecord).ThenInclude(pr => pr!.RFQItem).ThenInclude(ri => ri.RFQ).ThenInclude(r => r.User)
            .Include(pi => pi.ProcumentRecord).ThenInclude(pr => pr!.Supplier)
            .OrderByDescending(pi => pi.PurchaseOrder!.CreatedAt)
            .Select(pi => new POItemFlatRow
            {
                Id = pi.Id,
                POId = pi.POId,
                PONumber = pi.PurchaseOrder != null ? pi.PurchaseOrder.PONumber : "",
                POStatus = pi.PurchaseOrder != null ? pi.PurchaseOrder.Status : "",
                PartNumber = pi.PartNumber != null ? pi.PartNumber.Name : "",
                PartDescription = pi.PartNumber != null ? pi.PartNumber.Description : "",
                SupplierName = pi.ProcumentRecord != null && pi.ProcumentRecord.Supplier != null
                    ? pi.ProcumentRecord.Supplier.Name
                    : (pi.PurchaseOrder != null ? pi.PurchaseOrder.Supplier.Name : ""),
                RFQReference = pi.ProcumentRecord != null && pi.ProcumentRecord.RFQItem != null
                    ? pi.ProcumentRecord.RFQItem.RFQ.Name : "",
                LeadTime = pi.ProcumentRecord != null ? pi.ProcumentRecord.LeadTime : null,
                BuyPrice = pi.UnitPrice,
                TotalBuyPrice = pi.TotalPrice,
                Qty = pi.Qty,
                Condition = pi.Condition,
                AltPart = pi.PartNumber != null
                    ? string.Join(", ", pi.PartNumber.Alternatives.Select(a => a.Name))
                    : null,
                Priority = pi.ProcumentRecord != null && pi.ProcumentRecord.RFQItem != null
                    ? pi.ProcumentRecord.RFQItem.Priority : null,
                Notes = pi.ProcumentRecord != null && pi.ProcumentRecord.RFQItem != null
                    ? pi.ProcumentRecord.RFQItem.Note : null,
                CustomerName = pi.ProcumentRecord != null && pi.ProcumentRecord.RFQItem != null
                    ? pi.ProcumentRecord.RFQItem.RFQ.Customer.Name : "",
                AssignedTo = pi.ProcumentRecord != null && pi.ProcumentRecord.RFQItem != null && pi.ProcumentRecord.RFQItem.RFQ.User != null
                    ? pi.ProcumentRecord.RFQItem.RFQ.User.Name : "",
            })
            .ToListAsync();

        // Resolve sell price from QuoteItems linked to same PartNumber + RFQItem
        // We need to do this separately because the join is complex
        var partNumberIds = rows.Where(r => !string.IsNullOrEmpty(r.PartNumber)).Select(r => r.PartNumber).Distinct().ToList();

        var quoteItemLookup = await _db.QuoteItems
            .Include(qi => qi.Quote)
            .Include(qi => qi.PartNumber)
            .Where(qi => qi.PartNumber != null && qi.Quote.Status == "Accepted")
            .GroupBy(qi => qi.PartNumber!.Name)
            .Select(g => new { PartName = g.Key, SellPrice = g.Max(qi => qi.UnitPrice), TotalSellPrice = g.Sum(qi => qi.TotalPrice) })
            .ToDictionaryAsync(x => x.PartName, x => new { x.SellPrice, x.TotalSellPrice });

        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.PartNumber) && quoteItemLookup.TryGetValue(row.PartNumber, out var qd))
            {
                row.SellPrice = qd.SellPrice;
                row.TotalSellPrice = qd.TotalSellPrice;
            }
        }

        return Ok(rows);
    }

    /// <summary>
    /// Returns attention groups for the current user's role —
    /// each group is a category of items that need action.
    /// </summary>
    [HttpGet("attention")]
    public async Task<ActionResult<List<AttentionGroup>>> GetAttention()
    {
        var isAdmin      = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var isPayment    = User.IsInRole("Payment") || User.IsInRole("AHM");
        var isExpert     = User.IsInRole("Expert");
        var isInventory  = User.IsInRole("Inventory");
        var currentUserId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var userName      = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var isSyd         = isExpert && userName == "SYD";

        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();

        // Expert's allowed RFQ ids (stored as strings in EntityPermission)
        long[] expertRfqIds = Array.Empty<long>();
        if (isExpert)
        {
            var rfqIdStrings = await _db.EntityPermissions
                .Where(ep => ep.UserId == currentUserId && ep.EntityName == "RFQ")
                .Select(ep => ep.EntityId)
                .ToArrayAsync();
            expertRfqIds = rfqIdStrings
                .Select(s => long.TryParse(s, out var id) ? id : -1L)
                .Where(id => id > 0)
                .ToArray();
        }

        // Inventory warehouse ids
        long[] inventoryWarehouseIds = Array.Empty<long>();
        if (isInventory)
            inventoryWarehouseIds = await _db.UserWarehouses
                .Where(uw => uw.UserId == currentUserId)
                .Select(uw => uw.WarehouseId)
                .ToArrayAsync();

        // Base-scoped allowed invoice ids (for non-SuperAdmin admins with bases assigned)
        IQueryable<long> allowedInvoiceIds = _db.Invoices.Select(i => i.Id);
        if (!isSuperAdmin && isAdmin && userBases.Length > 0)
            allowedInvoiceIds = _db.Invoices
                .Where(i => i.Customer.Base == null || userBases.Contains(i.Customer.Base.Value))
                .Select(i => i.Id);

        var groups = new List<AttentionGroup>();

        // ── Cat 1: PO Awaiting Admin Approval (Admin only) ──────────────────
        if (isAdmin)
        {
            var q = _db.PurchaseOrders
                .Where(po => po.AdminApproval == "Pending"
                    && po.Status != "Cancelled"
                    && po.Status != "Returned"
                    && !(po.Origin == "Stock" && po.Status == "Draft"));
            if (!isSuperAdmin && userBases.Length > 0)
                q = q.Where(po => po.POItems.Any(pi =>
                    pi.ProcumentRecord == null ||
                    pi.ProcumentRecord.RFQItem == null ||
                    pi.ProcumentRecord.RFQItem.RFQ.Customer.Base == null ||
                    userBases.Contains(pi.ProcumentRecord.RFQItem.RFQ.Customer.Base.Value)));

            var items = await q
                .OrderBy(po => po.CreatedAt)
                .Take(50)
                .Select(po => new { po.Id, po.PONumber, SupplierName = po.Supplier.Name, po.CreatedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "PO Awaiting Admin Approval",
                    Severity = "urgent",
                    Icon = "mdi-shield-alert-outline",
                    Count = items.Count,
                    Items = items.Select(po => new AttentionItem
                    {
                        Category = "PO Awaiting Admin Approval",
                        Severity = "urgent",
                        Title = po.PONumber,
                        SupplierName = po.SupplierName,
                        Since = po.CreatedAt,
                        Route = $"/purchase-orders/{po.Id}",
                        EntityType = "PurchaseOrder",
                        EntityId = po.Id
                    }).ToList()
                });
        }

        // ── Cat 2: PO Awaiting Payment (Admin + Payment/AHM) ─────────────────
        if (isAdmin || isPayment)
        {
            var q = _db.PurchaseOrders
                .Where(po => po.AdminApproval == "Approved"
                    && po.PaymentApproval == "Pending"
                    && po.PaymentStatus == "NotStarted"
                    && po.Status != "Cancelled"
                    && po.Status != "Returned");
            if (!isSuperAdmin && isAdmin && userBases.Length > 0)
                q = q.Where(po => po.POItems.Any(pi =>
                    pi.ProcumentRecord == null ||
                    pi.ProcumentRecord.RFQItem == null ||
                    pi.ProcumentRecord.RFQItem.RFQ.Customer.Base == null ||
                    userBases.Contains(pi.ProcumentRecord.RFQItem.RFQ.Customer.Base.Value)));

            var items = await q
                .OrderBy(po => po.CreatedAt)
                .Take(50)
                .Select(po => new { po.Id, po.PONumber, SupplierName = po.Supplier.Name, po.CreatedAt, po.AdminApprovalAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "PO Awaiting Payment",
                    Severity = "urgent",
                    Icon = "mdi-cash-clock",
                    Count = items.Count,
                    Items = items.Select(po => new AttentionItem
                    {
                        Category = "PO Awaiting Payment",
                        Severity = "urgent",
                        Title = po.PONumber,
                        SupplierName = po.SupplierName,
                        Since = po.AdminApprovalAt ?? po.CreatedAt,
                        Route = $"/purchase-orders/{po.Id}",
                        EntityType = "PurchaseOrder",
                        EntityId = po.Id
                    }).ToList()
                });
        }

        // ── Cat 3: Payment Submitted, Awaiting Acceptance (Admin + Payment/AHM) ──
        if (isAdmin || isPayment)
        {
            var q = _db.PurchaseOrders
                .Where(po => po.PaymentStatus == "Submitted"
                    && po.PaymentApproval == "Pending"
                    && po.Status != "Cancelled"
                    && po.Status != "Returned");
            if (!isSuperAdmin && isAdmin && userBases.Length > 0)
                q = q.Where(po => po.POItems.Any(pi =>
                    pi.ProcumentRecord == null ||
                    pi.ProcumentRecord.RFQItem == null ||
                    pi.ProcumentRecord.RFQItem.RFQ.Customer.Base == null ||
                    userBases.Contains(pi.ProcumentRecord.RFQItem.RFQ.Customer.Base.Value)));

            var items = await q
                .OrderBy(po => po.CreatedAt)
                .Take(50)
                .Select(po => new { po.Id, po.PONumber, SupplierName = po.Supplier.Name, po.CreatedAt, po.PaymentSubmittedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "Payment Submitted — Awaiting Acceptance",
                    Severity = "warning",
                    Icon = "mdi-clock-check-outline",
                    Count = items.Count,
                    Items = items.Select(po => new AttentionItem
                    {
                        Category = "Payment Submitted — Awaiting Acceptance",
                        Severity = "warning",
                        Title = po.PONumber,
                        SupplierName = po.SupplierName,
                        Since = po.PaymentSubmittedAt ?? po.CreatedAt,
                        Route = $"/purchase-orders/{po.Id}",
                        EntityType = "PurchaseOrder",
                        EntityId = po.Id
                    }).ToList()
                });
        }

        // ── Cat 4: Invoice Has POP — Not Marked Paid (Admin + Payment/AHM) ──
        if (isAdmin || isPayment)
        {
            var q = _db.Invoices
                .Where(i => i.Status != "Paid" && !i.IsCancelled
                    && _db.CustomerPayments.Any(cp => cp.InvoiceId == i.Id));
            if (!isSuperAdmin && isAdmin && userBases.Length > 0)
                q = q.Where(i => allowedInvoiceIds.Contains(i.Id));

            var items = await q
                .OrderBy(i => i.CreatedAt)
                .Take(50)
                .Select(i => new { i.Id, i.InvoiceNumber, CustomerName = i.Customer.Name, i.CreatedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "Invoice Has POP — Not Marked Paid",
                    Severity = "urgent",
                    Icon = "mdi-receipt-text-check-outline",
                    Count = items.Count,
                    Items = items.Select(i => new AttentionItem
                    {
                        Category = "Invoice Has POP — Not Marked Paid",
                        Severity = "urgent",
                        Title = i.InvoiceNumber,
                        CustomerName = i.CustomerName,
                        Since = i.CreatedAt,
                        Route = $"/invoices/{i.Id}",
                        EntityType = "Invoice",
                        EntityId = i.Id
                    }).ToList()
                });
        }

        // ── Cat 5: Track Number In Transit — Not Received (Admin + Inventory) ──
        if (isAdmin || isInventory)
        {
            var q = _db.POItemTrackNumbers
                .Where(t => t.Status == "Ship to Warehouse");
            if (isInventory)
            {
                if (inventoryWarehouseIds.Length == 0)
                    q = q.Where(_ => false);
                else
                    q = q.Where(t => t.WarehouseId.HasValue && inventoryWarehouseIds.Contains(t.WarehouseId.Value));
            }

            var items = await q
                .OrderBy(t => t.CreatedAt)
                .Take(50)
                .Select(t => new { t.Id, t.TrackNumber, t.Carrier, t.CreatedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "Track Number In Transit — Not Received",
                    Severity = "info",
                    Icon = "mdi-truck-fast-outline",
                    Count = items.Count,
                    Items = items.Select(t => new AttentionItem
                    {
                        Category = "Track Number In Transit — Not Received",
                        Severity = "info",
                        Title = t.TrackNumber,
                        Detail = t.Carrier ?? "",
                        Since = t.CreatedAt,
                        Route = $"/shipping/track-numbers/{t.Id}",
                        EntityType = "TrackNumber",
                        EntityId = t.Id
                    }).ToList()
                });
        }

        // ── Cat 6: Parts Awaiting Inventory Review (Admin + Inventory) ──────
        if (isAdmin || isInventory)
        {
            var q = _db.POItemTrackNumbers
                .Where(t => t.Status == "Received in Warehouse"
                    && t.Items.Any(i => i.Status == "Pending"));
            if (isInventory)
            {
                if (inventoryWarehouseIds.Length == 0)
                    q = q.Where(_ => false);
                else
                    q = q.Where(t => t.WarehouseId.HasValue && inventoryWarehouseIds.Contains(t.WarehouseId.Value));
            }

            var items = await q
                .OrderBy(t => t.CreatedAt)
                .Take(50)
                .Select(t => new { t.Id, t.TrackNumber, t.CreatedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "Parts Awaiting Inventory Review",
                    Severity = "warning",
                    Icon = "mdi-package-variant-closed-check",
                    Count = items.Count,
                    Items = items.Select(t => new AttentionItem
                    {
                        Category = "Parts Awaiting Inventory Review",
                        Severity = "warning",
                        Title = t.TrackNumber,
                        Since = t.CreatedAt,
                        Route = $"/shipping/track-numbers/{t.Id}",
                        EntityType = "TrackNumber",
                        EntityId = t.Id
                    }).ToList()
                });
        }

        // ── Cat 7: Parts Ready for SN# Creation (Admin + SYD Expert) ────────
        if (isAdmin || isSyd)
        {
            // Pre-materialise: track numbers already in an SN# to avoid correlated subquery
            var linkedTrackIds = await _db.ShipmentNoteTrackNumbers
                .Select(snt => snt.TrackNumberId)
                .Distinct()
                .ToListAsync();

            var items = await _db.POItemTrackNumbers
                .Where(t => t.Items.Any(i => i.Status == "Accepted")
                    && !linkedTrackIds.Contains(t.Id))
                .OrderBy(t => t.CreatedAt)
                .Take(50)
                .Select(t => new { t.Id, t.TrackNumber, t.CreatedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "Parts Ready for SN# Creation",
                    Severity = "warning",
                    Icon = "mdi-package-check",
                    Count = items.Count,
                    Items = items.Select(t => new AttentionItem
                    {
                        Category = "Parts Ready for SN# Creation",
                        Severity = "warning",
                        Title = t.TrackNumber,
                        Since = t.CreatedAt,
                        Route = "/shipping/ready-for-sn",
                        EntityType = "TrackNumber",
                        EntityId = t.Id
                    }).ToList()
                });
        }

        // ── Cat 8: SN# Awaiting Customs Upload (Admin + SYD Expert) ──────────
        if (isAdmin || isSyd)
        {
            var items = await _db.ShipmentNotes
                .Where(sn => sn.Status == "Ship To USA"
                    && (sn.CustomsFileName == null || sn.CustomsFileName == ""))
                .OrderBy(sn => sn.CreatedAt)
                .Take(50)
                .Select(sn => new { sn.Id, sn.SNNumber, sn.CreatedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "SN# Awaiting Customs Upload",
                    Severity = "warning",
                    Icon = "mdi-file-upload-outline",
                    Count = items.Count,
                    Items = items.Select(sn => new AttentionItem
                    {
                        Category = "SN# Awaiting Customs Upload",
                        Severity = "warning",
                        Title = sn.SNNumber,
                        Since = sn.CreatedAt,
                        Route = "/shipment-notes",
                        EntityType = "ShipmentNote",
                        EntityId = sn.Id
                    }).ToList()
                });
        }

        // ── Cat 9: SN# In Customs / Received in Office (Admin + SYD Expert) ──
        if (isAdmin || isSyd)
        {
            var items = await _db.ShipmentNotes
                .Where(sn => sn.Status == "Clearing Customs" || sn.Status == "Received in Office")
                .OrderBy(sn => sn.CreatedAt)
                .Take(50)
                .Select(sn => new { sn.Id, sn.SNNumber, sn.Status, sn.CreatedAt })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "SN# In Customs / Received in Office",
                    Severity = "info",
                    Icon = "mdi-airplane-landing",
                    Count = items.Count,
                    Items = items.Select(sn => new AttentionItem
                    {
                        Category = "SN# In Customs / Received in Office",
                        Severity = "info",
                        Title = sn.SNNumber,
                        Detail = sn.Status,
                        Since = sn.CreatedAt,
                        Route = "/shipment-notes",
                        EntityType = "ShipmentNote",
                        EntityId = sn.Id
                    }).ToList()
                });
        }

        // ── Cat 10: RFQ With No Active Quote (Admin + Expert) ───────────────
        if (isAdmin || isExpert)
        {
            var q = _db.RFQs
                .Where(r => r.Status == "Open"
                    && !_db.Quotes.Any(q2 => q2.RFQId == r.Id && q2.Status != "Rejected"));

            if (isExpert)
            {
                if (expertRfqIds.Length == 0)
                    q = q.Where(_ => false);
                else
                    q = q.Where(r => expertRfqIds.Contains(r.Id));
            }
            else if (!isSuperAdmin && userBases.Length > 0)
                q = q.Where(r => r.Customer.Base == null || userBases.Contains(r.Customer.Base.Value));

            var items = await q
                .OrderBy(r => r.ReceivedDate)
                .Take(50)
                .Select(r => new { r.Id, r.Name, CustomerName = r.Customer.Name, r.ReceivedDate })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "RFQ With No Active Quote",
                    Severity = "warning",
                    Icon = "mdi-file-question-outline",
                    Count = items.Count,
                    Items = items.Select(r => new AttentionItem
                    {
                        Category = "RFQ With No Active Quote",
                        Severity = "warning",
                        Title = r.Name,
                        CustomerName = r.CustomerName,
                        Since = r.ReceivedDate,
                        Route = $"/rfqs/{r.Id}",
                        EntityType = "RFQ",
                        EntityId = r.Id
                    }).ToList()
                });
        }

        // ── Cat 11: Rejected PO (Admin + Expert) ─────────────────────────────
        if (isAdmin || isExpert)
        {
            var q = _db.PurchaseOrders
                .Where(po => po.AdminApproval == "Rejected"
                    && po.Status != "Cancelled"
                    && po.Status != "Returned");

            if (isExpert)
            {
                if (expertRfqIds.Length == 0)
                    q = q.Where(_ => false);
                else
                    q = q.Where(po => po.POItems.Any(pi =>
                        pi.ProcumentRecord != null &&
                        pi.ProcumentRecord.RFQItem != null &&
                        expertRfqIds.Contains(pi.ProcumentRecord.RFQItem.RFQId)));
            }
            else if (!isSuperAdmin && userBases.Length > 0)
                q = q.Where(po => po.POItems.Any(pi =>
                    pi.ProcumentRecord == null ||
                    pi.ProcumentRecord.RFQItem == null ||
                    pi.ProcumentRecord.RFQItem.RFQ.Customer.Base == null ||
                    userBases.Contains(pi.ProcumentRecord.RFQItem.RFQ.Customer.Base.Value)));

            var items = await q
                .OrderBy(po => po.CreatedAt)
                .Take(50)
                .Select(po => new
                {
                    po.Id, po.PONumber, SupplierName = po.Supplier.Name,
                    po.RejectionNote, po.CreatedAt, po.AdminApprovalAt
                })
                .ToListAsync();

            if (items.Count > 0)
                groups.Add(new AttentionGroup
                {
                    Category = "Rejected PO",
                    Severity = "urgent",
                    Icon = "mdi-close-circle-outline",
                    Count = items.Count,
                    Items = items.Select(po => new AttentionItem
                    {
                        Category = "Rejected PO",
                        Severity = "urgent",
                        Title = po.PONumber,
                        Detail = po.RejectionNote ?? "",
                        SupplierName = po.SupplierName,
                        Since = po.AdminApprovalAt ?? po.CreatedAt,
                        Route = $"/purchase-orders/{po.Id}",
                        EntityType = "PurchaseOrder",
                        EntityId = po.Id
                    }).ToList()
                });
        }

        await AddOurInventoryAttentionAsync(groups, isAdmin, isInventory, inventoryWarehouseIds);

        return Ok(groups
            .Where(g => g.Count > 0)
            .OrderByDescending(g => g.Severity == "urgent" ? 2 : g.Severity == "warning" ? 1 : 0)
            .ToList());
    }
}

// ─── Response DTOs ──────────────────────────────────────────

public class OurStockStats
{
    public decimal StockValue { get; set; }
    public int Lots { get; set; }
    public int LowStockLots { get; set; }
    public decimal ReservedUnits { get; set; }
    public decimal IncomingValue { get; set; }
}

public class DashboardResponse
{
    /// <summary>Our Inventory summary; only for admins who can see Our Inventory.</summary>
    public OurStockStats? OurStock { get; set; }
    public int TotalRfqs { get; set; }
    public int TotalQuotes { get; set; }
    public int PendingRfqs { get; set; }
    public int PendingQuotes { get; set; }
    public int TotalUsers { get; set; }
    public int TotalPOs { get; set; }
    public decimal TotalPOValue { get; set; }
    public int AcceptedPOs { get; set; }
    public int TotalInvoices { get; set; }
    public decimal TotalInvoiceValue { get; set; }
    public decimal PaidInvoiceValue { get; set; }
    public decimal TotalQuoteValue { get; set; }
    public decimal AcceptedQuoteValue { get; set; }
    public int RFQCount { get; set; }
    public List<StatusCount> POStatusDistribution { get; set; } = new();
    public List<StatusCount> QuoteStatusDistribution { get; set; } = new();
    public List<StatusCount> InvoiceStatusDistribution { get; set; } = new();
    public List<MonthlyDataPoint> MonthlyQuotes { get; set; } = new();
    public List<MonthlyDataPoint> MonthlyPOs { get; set; } = new();
    public List<MonthlyDataPoint> MonthlyInvoices { get; set; } = new();
    public List<UserStatItem>? UserQuoteStats { get; set; }
    public List<SupplierStatItem>? TopSuppliers { get; set; }
    public List<AuditActivityItem>? RecentActivity { get; set; }
}

public class StatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class MonthlyDataPoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public class UserStatItem
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public int RFQCount { get; set; }
}

public class SupplierStatItem
{
    public string SupplierName { get; set; } = string.Empty;
    public int POCount { get; set; }
    public decimal TotalValue { get; set; }
}

public class POItemFlatRow
{
    public long Id { get; set; }
    public long? POId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string POStatus { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string? PartDescription { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string RFQReference { get; set; } = string.Empty;
    public string? LeadTime { get; set; }
    public decimal BuyPrice { get; set; }
    public decimal TotalBuyPrice { get; set; }
    public decimal? SellPrice { get; set; }
    public decimal? TotalSellPrice { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? AltPart { get; set; }
    public string? Priority { get; set; }
    public string? Notes { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
}

public class AuditActivityItem
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? UserName { get; set; }
    public DateTime Timestamp { get; set; }
}

// ─── Attention Center DTOs ───────────────────────────────────

public class AttentionItem
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public List<string> PartNumbers { get; set; } = new();
    public string CustomerName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime Since { get; set; }
    public string Route { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
}

public class AttentionGroup
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Icon { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<AttentionItem> Items { get; set; } = new();
}
