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
        var currentUserId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        // Determine effective scope:
        // - Admin with no filter or userId=0 → global (all users)
        // - Admin with userId → scoped to that user
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

        return Ok(response);
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
        var rows = await _db.POItems
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
}

// ─── Response DTOs ──────────────────────────────────────────

public class DashboardResponse
{
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
