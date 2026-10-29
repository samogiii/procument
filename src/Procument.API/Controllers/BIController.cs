using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using System.Security.Claims;

namespace Procument.API.Controllers;

/// <summary>
/// BI page — per-customer activity over the last N days: RFQs, sent quotes, proforma invoices,
/// what was actually bought for those proformas (sell value, supplier cost, shipping / wire fee / other
/// costs from the wallet withdraws, profit), and the time from proforma to first final invoice.
/// Read-only, nothing is stored. Granted per user in Menu Access ("bi"); SuperAdmin always passes.
/// Lives in the API project because it reads RFQ, Sales and Purchasing tables together.
/// </summary>
[ApiController]
[Route("api/bi")]
[Authorize]
public class BIController : ControllerBase
{
    public const string Feature = "bi";
    private const int MaxDays = 3650;
    private const int MaxCustomerIds = 2000;

    /// <summary>POs in these statuses no longer cost anything against the proforma.</summary>
    private static readonly string[] ClosedPoStatuses = ["Cancelled", "Returned"];
    private const string CancelledLineStatus = "Cancelled";
    /// <summary>Wallet withdraw category shown as "Bank Fee and Others" (bank fees, shipping, other costs).</summary>
    private const string BankFeeToType = "BankFee";
    /// <summary>A PO counts as paid once wallet payments + credit reach its total within a cent.</summary>
    private const decimal PaidTolerance = 0.01m;
    /// <summary>Only these users see Base 1 customers in BI (other SuperAdmins see every other base).</summary>
    private static readonly string[] Base1Users = ["KZM", "System Admin", "MA"];
    private const int Base1 = 1;

    private bool SeesBase1 => Base1Users.Contains(User.Identity?.Name);

    private readonly AppDbContext _db;

    public BIController(AppDbContext db) => _db = db;

    private async Task<bool> HasAccessAsync()
    {
        if (User.IsInRole(UserRoles.SuperAdmin)) return true;
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName)
            && await _db.MenuPermissions.AnyAsync(p => p.Feature == Feature && p.UserName == userName);
    }

    /// <summary>
    /// Customers the caller may report on: Base 1 only for <see cref="Base1Users"/>; SuperAdmin sees the rest; everyone else sees null-base customers,
    /// their base customers and the customers individually assigned to them (same rule as /customers).
    /// </summary>
    private IQueryable<Customer> ScopedCustomers()
    {
        var query = _db.Customers.AsNoTracking();
        if (!SeesBase1) query = query.Where(c => c.Base != Base1);
        if (User.IsInRole(UserRoles.SuperAdmin)) return query;

        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        var userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        return query.Where(c =>
            c.Base == null ||
            userBases.Contains(c.Base.Value) ||
            _db.UserCustomers.Any(uc => uc.UserId == userId && uc.CustomerId == c.Id));
    }

    /// <summary>
    /// Customer ids the report covers, or null for "every customer, no filter" (SuperAdmin + all).
    /// Bases are resolved here, on every request, so a customer added to a base later is included too.
    /// </summary>
    private IQueryable<long>? CustomerFilter(BIQuery request)
    {
        var customerIds = request.CustomerIds is { Count: > 0 } ids ? ids : null;
        var bases = request.Bases is { Count: > 0 } b ? b : null;
        if (customerIds == null && bases == null && User.IsInRole(UserRoles.SuperAdmin) && SeesBase1) return null;

        var scoped = ScopedCustomers();
        if (customerIds != null) scoped = scoped.Where(c => customerIds.Contains(c.Id));
        if (bases != null) scoped = scoped.Where(c => c.Base != null && bases.Contains(c.Base.Value));
        return scoped.Select(c => c.Id);
    }

    /// <summary>First UTC day of a window of <paramref name="days"/> days ending today.</summary>
    private static DateTime WindowStart(int days) => DateTime.UtcNow.Date.AddDays(-(days - 1));

    private static string? ValidateWindow(BIQuery request)
    {
        if (request.Days < 1 || request.Days > MaxDays) return $"Days must be between 1 and {MaxDays}.";
        if (request.CustomerIds is { Count: > MaxCustomerIds }) return $"Pick at most {MaxCustomerIds} customers, or choose All customers.";
        if (request.Bases is { Count: > 50 }) return "Too many bases.";
        return null;
    }

    /// <summary>The customer bases the caller can see, with how many active customers each has.</summary>
    [HttpGet("bases")]
    public async Task<ActionResult<List<BIBaseOption>>> Bases()
    {
        if (!await HasAccessAsync()) return Forbid();
        return Ok(await ScopedCustomers()
            .Where(c => c.IsActive && c.Base != null)
            .GroupBy(c => c.Base!.Value)
            .Select(g => new BIBaseOption { Base = g.Key, CustomerCount = g.Count() })
            .OrderBy(b => b.Base)
            .ToListAsync());
    }

    /// <summary>Proformas (not cancelled) created in the window for the covered customers.</summary>
    private IQueryable<Invoice> WindowInvoices(DateTime from, IQueryable<long>? filter)
    {
        var invoices = _db.Invoices.AsNoTracking().Where(i => !i.IsCancelled && i.CreatedAt >= from);
        return filter == null ? invoices : invoices.Where(i => filter.Contains(i.CustomerId));
    }

    /// <summary>Lazy customer search for the picker — at most <paramref name="take"/> matches.</summary>
    [HttpGet("customers")]
    public async Task<ActionResult<List<BICustomerOption>>> SearchCustomers([FromQuery] string? search = null, [FromQuery] int take = 30)
    {
        if (!await HasAccessAsync()) return Forbid();
        take = Math.Clamp(take, 1, 100);

        var query = ScopedCustomers().Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c => c.Name.Contains(s) || (c.CustomerCode != null && c.CustomerCode.Contains(s)));
        }

        return Ok(await query
            .OrderBy(c => c.Name)
            .Take(take)
            .Select(c => new BICustomerOption { Id = c.Id, Name = c.Name, Code = c.CustomerCode, Base = c.Base })
            .ToListAsync());
    }

    /// <summary>Per-customer metrics, the totals and a time trend for the window.</summary>
    [HttpPost("summary")]
    public async Task<ActionResult<BISummaryResponse>> Summary([FromBody] BIQuery request)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (ValidateWindow(request) is { } error) return BadRequest(error);

        var from = WindowStart(request.Days);
        var filter = CustomerFilter(request);

        var rfqs = _db.RFQs.AsNoTracking().Where(r => r.CreatedAt >= from);
        // A quote counts as sent once it has a SentAt stamp, or carries Sent/Accepted from before SentAt existed.
        var quotes = _db.Quotes.AsNoTracking()
            .Where(q => (q.SentAt != null || q.Status == "Sent" || q.Status == "Accepted") && (q.SentAt ?? q.CreatedAt) >= from);
        var invoices = WindowInvoices(from, filter);
        if (filter != null)
        {
            rfqs = rfqs.Where(r => filter.Contains(r.CustomerId));
            quotes = quotes.Where(q => filter.Contains(q.CustomerId));
        }

        // Grouped by customer and day in SQL — one small row per (customer, day) with activity.
        var rfqDays = await rfqs
            .GroupBy(r => new { r.CustomerId, Day = r.CreatedAt.Date })
            .Select(g => new { g.Key.CustomerId, g.Key.Day, Count = g.Count() })
            .ToListAsync();
        var quoteDays = await quotes
            .GroupBy(q => new { q.CustomerId, Day = (q.SentAt ?? q.CreatedAt).Date })
            .Select(g => new { g.Key.CustomerId, g.Key.Day, Count = g.Count(), Sum = g.Sum(q => q.TotalAmount ?? 0m) })
            .ToListAsync();
        var invoiceRows = await invoices
            .Select(i => new
            {
                i.Id, i.CustomerId, i.CreatedAt,
                Lines = i.InvoiceItems.Count(x => x.Status != CancelledLineStatus),
            })
            .ToListAsync();

        var invoiceIds = invoices.Select(i => i.Id);
        var economics = await EconomicsAsync(invoiceIds);
        var firstFinals = await _db.FinalInvoices.AsNoTracking()
            .Where(f => invoiceIds.Contains(f.ProformaInvoiceId))
            .GroupBy(f => f.ProformaInvoiceId)
            .Select(g => new { InvoiceId = g.Key, First = g.Min(f => f.CreatedAt) })
            .ToDictionaryAsync(f => f.InvoiceId, f => f.First);

        // ── Per customer ──
        var rows = new Dictionary<long, BICustomerRow>();
        BICustomerRow Row(long customerId)
        {
            if (!rows.TryGetValue(customerId, out var row)) rows[customerId] = row = new BICustomerRow { CustomerId = customerId };
            return row;
        }
        var finalDays = new Dictionary<long, List<double>>();
        var unpaidPos = new Dictionary<long, HashSet<long>>();
        var allUnpaidPos = new HashSet<long>();

        foreach (var r in rfqDays) Row(r.CustomerId).RfqCount += r.Count;
        foreach (var q in quoteDays)
        {
            var row = Row(q.CustomerId);
            row.SentQuoteCount += q.Count;
            row.SentQuoteSum += q.Sum;
        }
        foreach (var i in invoiceRows)
        {
            var row = Row(i.CustomerId);
            row.PiCount++;
            if (economics.PerInvoice.TryGetValue(i.Id, out var e))
            {
                if (e.BoughtLines > 0)
                {
                    row.BoughtPiCount++;
                    if (e.BoughtLines < i.Lines) row.PartialPoCount++;
                }
                row.BoughtValue += e.BoughtValue;
                row.SupplierCost += e.SupplierCost;
                row.Fees += e.Fees;
                row.FeesEstimated += e.FeesEstimated;
                if (!unpaidPos.TryGetValue(i.CustomerId, out var set)) unpaidPos[i.CustomerId] = set = [];
                set.UnionWith(e.UnpaidPoIds);
                allUnpaidPos.UnionWith(e.UnpaidPoIds);
            }
            if (firstFinals.TryGetValue(i.Id, out var first))
            {
                if (!finalDays.TryGetValue(i.CustomerId, out var list)) finalDays[i.CustomerId] = list = [];
                list.Add(Math.Max(0, (first - i.CreatedAt).TotalDays));
            }
        }

        // Picked customers always show, even with nothing in the window (a base shows only its active customers).
        if (request.CustomerIds is { Count: > 0 } && filter != null)
            foreach (var id in await filter.ToListAsync()) Row(id);

        var rowIds = rows.Keys.ToList();
        var names = await _db.Customers.AsNoTracking()
            .Where(c => rowIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.CustomerCode, c.Base })
            .ToDictionaryAsync(c => c.Id);
        foreach (var row in rows.Values)
        {
            if (names.TryGetValue(row.CustomerId, out var c))
            {
                row.CustomerName = c.Name;
                row.CustomerCode = c.CustomerCode;
                row.Base = c.Base;
            }
            row.UnpaidPoCount = unpaidPos.TryGetValue(row.CustomerId, out var set) ? set.Count : 0;
            Finish(row);
            if (finalDays.TryGetValue(row.CustomerId, out var days)) ApplyFinalDays(row, days);
        }

        var totals = new BIMetrics { UnpaidPoCount = allUnpaidPos.Count, UnconvertedFeeCount = economics.UnconvertedFeeCount };
        foreach (var row in rows.Values)
        {
            totals.RfqCount += row.RfqCount;
            totals.SentQuoteCount += row.SentQuoteCount;
            totals.SentQuoteSum += row.SentQuoteSum;
            totals.PiCount += row.PiCount;
            totals.BoughtPiCount += row.BoughtPiCount;
            totals.PartialPoCount += row.PartialPoCount;
            totals.BoughtValue += row.BoughtValue;
            totals.SupplierCost += row.SupplierCost;
            totals.Fees += row.Fees;
            totals.FeesEstimated += row.FeesEstimated;
        }
        Finish(totals);
        ApplyFinalDays(totals, finalDays.Values.SelectMany(d => d).ToList());

        // ── Trend ──
        var bucket = request.Days <= 62 ? "day" : request.Days <= 400 ? "week" : "month";
        var trend = new SortedDictionary<DateTime, BITrendPoint>();
        var today = DateTime.UtcNow.Date;
        for (var start = BucketStart(from, bucket); start <= today; start = NextBucket(start, bucket))
            trend[start] = new BITrendPoint { Start = start };
        // Clamped to the window so a row stamped slightly in the future still lands in the last bucket.
        BITrendPoint Point(DateTime day) => trend[BucketStart(day < from ? from : day > today ? today : day, bucket)];

        foreach (var r in rfqDays) Point(r.Day).Rfqs += r.Count;
        foreach (var q in quoteDays)
        {
            var p = Point(q.Day);
            p.SentQuotes += q.Count;
            p.SentQuoteSum += q.Sum;
        }
        foreach (var i in invoiceRows)
        {
            var p = Point(i.CreatedAt.Date);
            p.Pis++;
            if (economics.PerInvoice.TryGetValue(i.Id, out var e)) p.BoughtValue += e.BoughtValue;
        }

        return Ok(new BISummaryResponse
        {
            From = DateTime.SpecifyKind(from, DateTimeKind.Utc),
            To = DateTime.SpecifyKind(today, DateTimeKind.Utc),
            Days = request.Days,
            Bucket = bucket,
            Totals = totals,
            Customers = rows.Values
                .OrderByDescending(r => r.BoughtValue).ThenByDescending(r => r.SentQuoteSum).ThenByDescending(r => r.RfqCount)
                .ThenBy(r => r.CustomerName)
                .ToList(),
            Trend = trend.Values.Select(p => { p.Start = DateTime.SpecifyKind(p.Start, DateTimeKind.Utc); return p; }).ToList(),
        });
    }

    /// <summary>
    /// Proforma invoices in the window, one page at a time, with bought value, supplier cost, fees, profit and
    /// time to final invoice. Profit and fees are allocated in memory, so sorting and paging happen here too.
    /// </summary>
    [HttpPost("proformas")]
    public async Task<ActionResult<BIProformaPage>> Proformas([FromBody] BIProformaQuery request)
    {
        if (!await HasAccessAsync()) return Forbid();
        if (ValidateWindow(request) is { } error) return BadRequest(error);
        var take = Math.Clamp(request.Take, 1, 100);
        var skip = Math.Max(0, request.Skip);

        var invoices = WindowInvoices(WindowStart(request.Days), CustomerFilter(request));
        var baseRows = await invoices.Select(i => new
        {
            i.Id, i.InvoiceNumber, i.B1InvoiceNumber, i.Status, i.CreatedAt, i.TotalAmount,
            i.CustomerId, CustomerName = i.Customer.Name, CustomerCode = i.Customer.CustomerCode,
            QuoteNumber = i.Quote.QuoteNumber,
            Lines = i.InvoiceItems.Count(x => x.Status != CancelledLineStatus),
            FirstFinalAt = _db.FinalInvoices.Where(f => f.ProformaInvoiceId == i.Id).Min(f => (DateTime?)f.CreatedAt),
            LastFinalAt = _db.FinalInvoices.Where(f => f.ProformaInvoiceId == i.Id).Max(f => (DateTime?)f.CreatedAt),
            FinalCount = _db.FinalInvoices.Count(f => f.ProformaInvoiceId == i.Id),
        }).ToListAsync();
        var economics = await EconomicsAsync(invoices.Select(i => i.Id));

        var rows = baseRows.Select(r =>
        {
            economics.PerInvoice.TryGetValue(r.Id, out var e);
            var bought = e != null && e.BoughtLines > 0;
            var hasCost = e != null && (bought || e.Fees != 0);
            var profit = hasCost ? e!.BoughtValue - e.SupplierCost - e.Fees : (decimal?)null;
            return new BIProformaRow
            {
                Id = r.Id,
                InvoiceNumber = r.InvoiceNumber,
                B1ProformaInvoiceNumber = r.B1InvoiceNumber,
                Status = r.Status,
                CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
                CustomerId = r.CustomerId,
                CustomerName = r.CustomerName,
                CustomerCode = r.CustomerCode,
                QuoteNumber = r.QuoteNumber,
                TotalAmount = r.TotalAmount,
                LineCount = r.Lines,
                BoughtLineCount = e?.BoughtLines ?? 0,
                PoCount = e?.PoIds.Count ?? 0,
                UnpaidPoCount = e?.UnpaidPoIds.Count ?? 0,
                BoughtValue = bought ? e!.BoughtValue : null,
                SupplierCost = bought ? e!.SupplierCost : null,
                Fees = hasCost ? Math.Round(e!.Fees, 2) : null,
                FeesEstimated = hasCost ? Math.Round(e!.FeesEstimated, 2) : null,
                Profit = profit == null ? null : Math.Round(profit.Value, 2),
                MarginPercent = profit == null || !bought || e!.BoughtValue == 0 ? null : Math.Round(profit.Value / e.BoughtValue * 100, 1),
                FirstFinalAt = r.FirstFinalAt == null ? null : DateTime.SpecifyKind(r.FirstFinalAt.Value, DateTimeKind.Utc),
                LastFinalAt = r.LastFinalAt == null ? null : DateTime.SpecifyKind(r.LastFinalAt.Value, DateTimeKind.Utc),
                FinalCount = r.FinalCount,
                DaysToFinal = r.FirstFinalAt == null ? null : Math.Round(Math.Max(0, (r.FirstFinalAt.Value - r.CreatedAt).TotalDays), 1),
            };
        });

        var desc = request.SortDesc;
        // Rows without a value sort after every real value either way.
        IOrderedEnumerable<BIProformaRow> By<T>(Func<BIProformaRow, T?> key) where T : struct =>
            desc
                ? rows.OrderBy(r => key(r) == null).ThenByDescending(r => key(r))
                : rows.OrderBy(r => key(r) == null).ThenBy(r => key(r));
        var ordered = request.SortBy switch
        {
            "boughtValue" => By(r => r.BoughtValue),
            "supplierCost" => By(r => r.SupplierCost),
            "fees" => By(r => r.Fees),
            "profit" => By(r => r.Profit),
            "daysToFinal" => By(r => r.DaysToFinal),
            "customer" => desc ? rows.OrderByDescending(r => r.CustomerName) : rows.OrderBy(r => r.CustomerName),
            _ => desc ? rows.OrderByDescending(r => r.CreatedAt) : rows.OrderBy(r => r.CreatedAt),
        };

        return Ok(new BIProformaPage
        {
            Total = baseRows.Count,
            Items = ordered.ThenByDescending(r => r.Id).Skip(skip).Take(take).ToList(),
        });
    }

    // ── Bought value, supplier cost and fees ────────────────────────────────

    private sealed class PiEconomics
    {
        /// <summary>PI lines with at least one live PO line.</summary>
        public int BoughtLines;
        /// <summary>Sell value of the bought lines (legacy discounts applied) — the customer pays it whether or not the PI is paid yet.</summary>
        public decimal BoughtValue;
        /// <summary>PO line cost of the bought lines.</summary>
        public decimal SupplierCost;
        /// <summary>Shipping, wire fee, tax, processing and "Bank Fee and Others" allocated to this PI.</summary>
        public decimal Fees;
        /// <summary>Part of <see cref="Fees"/> still taken from an unpaid PO's written amounts.</summary>
        public decimal FeesEstimated;
        public HashSet<long> PoIds = [];
        public HashSet<long> UnpaidPoIds = [];
    }

    private sealed class Economics
    {
        public Dictionary<long, PiEconomics> PerInvoice = [];
        /// <summary>Fee withdraws left out because their currency could not be turned into USD.</summary>
        public int UnconvertedFeeCount;
    }

    /// <summary>
    /// For each proforma: what was bought for it and what that cost.
    /// <list type="bullet">
    /// <item>Supplier cost = the PO lines bought for its lines.</item>
    /// <item>A paid PO (wallet payments + credit ≥ its total) costs what really left the wallets: supplier payments,
    /// credit and every "Bank Fee and Others" withdraw on its payment requests. An unpaid PO costs what is written
    /// on it (lines + shipping, tax, processing, wire fee) plus any bank fee already withdrawn.</item>
    /// <item>A PO's fees (its cost above its lines) are shared between proformas by their share of its line value.</item>
    /// <item>Manual "Bank Fee and Others" withdraws tagged to the proforma itself (no payment request) are added as they are.</item>
    /// </list>
    /// </summary>
    private async Task<Economics> EconomicsAsync(IQueryable<long> invoiceIds)
    {
        var result = new Economics();
        PiEconomics Pi(long id)
        {
            if (!result.PerInvoice.TryGetValue(id, out var e)) result.PerInvoice[id] = e = new PiEconomics();
            return e;
        }

        var lines = await PoLines(invoiceIds).ToListAsync();
        var poIds = lines.Select(l => l.POId).Distinct().ToList();

        if (poIds.Count > 0)
        {
            // Every live line of these POs (stock lines and other proformas' lines included) — the fee share base.
            var poLineTotals = await _db.POItems.AsNoTracking()
                .Where(p => p.POId != null && poIds.Contains(p.POId.Value) && p.ReturnedAt == null)
                .GroupBy(p => p.POId!.Value)
                .Select(g => new { POId = g.Key, Total = g.Sum(p => p.TotalPrice) })
                .ToDictionaryAsync(x => x.POId, x => x.Total);
            var poExtras = await _db.PurchaseOrders.AsNoTracking()
                .Where(p => poIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    Extras = (p.ProcessingFee ?? 0m) + (p.Shipping ?? 0m) + (p.Tax ?? 0m)
                        + (p.ImportDetail != null ? p.ImportDetail.Wirefee ?? 0m : 0m),
                })
                .ToDictionaryAsync(x => x.Id, x => x.Extras);

            // Same definition of "paid" as the Wallets page: wallet payments + net supplier credit applied.
            var walletPaid = await SupplierPaymentLedger.WalletPayments(_db)
                .Where(t => t.PaymentRequest != null && t.PaymentRequest.POId != null && poIds.Contains(t.PaymentRequest.POId.Value))
                .GroupBy(t => t.PaymentRequest!.POId!.Value)
                .Select(g => new { POId = g.Key, Paid = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(x => x.POId, x => x.Paid);
            var creditPaid = await SupplierPaymentLedger.CreditApplications(_db)
                .Where(e => e.PaymentRequest!.POId != null && poIds.Contains(e.PaymentRequest.POId.Value))
                .GroupBy(e => e.PaymentRequest!.POId!.Value)
                .Select(g => new { POId = g.Key, Paid = g.Sum(e => e.Direction == SupplierCreditDirections.Out ? e.Amount : -e.Amount) })
                .ToDictionaryAsync(x => x.POId, x => x.Paid);

            // "Bank Fee and Others" on the POs' payment requests, plus the supplier-payment rate to turn them into USD.
            var poFeeRows = await _db.PaymentTransactions.AsNoTracking()
                .Where(t => t.Type == "Withdraw" && t.ToType == BankFeeToType
                    && t.PaymentRequest != null && t.PaymentRequest.POId != null && poIds.Contains(t.PaymentRequest.POId.Value))
                .Select(t => new FeeRow
                {
                    POId = t.PaymentRequest!.POId!.Value, PaymentRequestId = t.PaymentRequestId,
                    Amount = t.Amount, ExchangeRate = t.ExchangeRate, TxCurrency = t.TxCurrency, WalletCurrency = t.PaymentBox.Currency,
                })
                .ToListAsync();
            var prIds = poFeeRows.Where(f => f.PaymentRequestId != null).Select(f => f.PaymentRequestId!.Value).Distinct().ToList();
            var prRates = prIds.Count == 0 ? [] : (await SupplierPaymentLedger.WalletPayments(_db)
                    .Where(t => t.PaymentRequestId != null && prIds.Contains(t.PaymentRequestId.Value) && t.ExchangeRate != null && t.ExchangeRate > 0)
                    .Select(t => new { PrId = t.PaymentRequestId!.Value, t.Id, Rate = t.ExchangeRate!.Value })
                    .ToListAsync())
                .GroupBy(x => x.PrId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().Rate);
            var bankFees = new Dictionary<long, decimal>();
            foreach (var f in poFeeRows)
            {
                var rate = f.PaymentRequestId is { } prId && prRates.TryGetValue(prId, out var r) ? r : (decimal?)null;
                if (ToUsd(f, rate) is { } usd) bankFees[f.POId] = bankFees.GetValueOrDefault(f.POId) + usd;
                else result.UnconvertedFeeCount++;
            }

            // Per PO: the money it costs, and how much of that is above its lines.
            var poFees = new Dictionary<long, (decimal Fees, bool Paid, decimal LineTotal)>();
            foreach (var poId in poIds)
            {
                var lineTotal = poLineTotals.GetValueOrDefault(poId);
                var planned = lineTotal + poExtras.GetValueOrDefault(poId);
                var paid = walletPaid.GetValueOrDefault(poId) + creditPaid.GetValueOrDefault(poId);
                var isPaid = planned > 0 && paid >= planned - PaidTolerance;
                var outflow = (isPaid ? paid : planned) + bankFees.GetValueOrDefault(poId);
                poFees[poId] = (outflow - lineTotal, isPaid, lineTotal);
            }

            // Sell value once per PI line, however many PO lines it was split into.
            foreach (var line in lines.GroupBy(l => l.InvoiceItemId).Select(g => g.First()))
            {
                var e = Pi(line.InvoiceId);
                e.BoughtLines++;
                e.BoughtValue += line.SellTotal;
            }
            foreach (var g in lines.GroupBy(l => new { l.InvoiceId, l.POId }))
            {
                var e = Pi(g.Key.InvoiceId);
                var cost = g.Sum(l => l.TotalPrice);
                e.SupplierCost += cost;
                e.PoIds.Add(g.Key.POId);

                var po = poFees[g.Key.POId];
                var share = po.LineTotal > 0 ? cost / po.LineTotal : 0m;
                var fees = po.Fees * share;
                e.Fees += fees;
                if (!po.Paid)
                {
                    e.FeesEstimated += fees;
                    e.UnpaidPoIds.Add(g.Key.POId);
                }
            }
        }

        // Costs booked straight against the proforma from the Wallets page (shipping, courier, other fees).
        var directFees = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.Type == "Withdraw" && t.ToType == BankFeeToType && t.PaymentRequestId == null
                && t.InvoiceId != null && invoiceIds.Contains(t.InvoiceId.Value))
            .Select(t => new FeeRow
            {
                InvoiceId = t.InvoiceId!.Value,
                Amount = t.Amount, ExchangeRate = t.ExchangeRate, TxCurrency = t.TxCurrency, WalletCurrency = t.PaymentBox.Currency,
            })
            .ToListAsync();
        foreach (var f in directFees)
        {
            if (ToUsd(f, null) is { } usd) Pi(f.InvoiceId).Fees += usd;
            else result.UnconvertedFeeCount++;
        }

        return result;
    }

    private sealed class FeeRow
    {
        public long POId { get; set; }
        public long InvoiceId { get; set; }
        public long? PaymentRequestId { get; set; }
        public decimal Amount { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? TxCurrency { get; set; }
        public string WalletCurrency { get; set; } = "USD";
    }

    /// <summary>
    /// A withdraw in USD. Amount is in TxCurrency (wallet currency when null); Amount × ExchangeRate is the wallet
    /// amount. A non-USD wallet amount needs the supplier payment's rate (wallet currency per 1 USD).
    /// </summary>
    private static decimal? ToUsd(FeeRow f, decimal? walletPerUsd)
    {
        static bool IsUsd(string? c) => string.Equals(c, "USD", StringComparison.OrdinalIgnoreCase);
        if (IsUsd(f.TxCurrency) || (f.TxCurrency == null && IsUsd(f.WalletCurrency))) return f.Amount;
        var walletAmount = f.Amount * (f.ExchangeRate ?? 1m);
        if (IsUsd(f.WalletCurrency)) return walletAmount;
        return walletPerUsd is > 0 ? walletAmount / walletPerUsd.Value : null;
    }

    /// <summary>Live PO lines bought for the given proformas' lines (returned lines and cancelled/returned POs excluded).</summary>
    private IQueryable<BIPoLine> PoLines(IQueryable<long> invoiceIds) =>
        from pi in _db.POItems
        join ii in _db.InvoiceItems on pi.InvoiceItemId equals (long?)ii.Id
        where pi.POId != null && pi.ReturnedAt == null
            && !ClosedPoStatuses.Contains(pi.PurchaseOrder!.Status)
            && invoiceIds.Contains(ii.InvoiceId)
        select new BIPoLine
        {
            InvoiceId = ii.InvoiceId,
            InvoiceItemId = ii.Id,
            POId = pi.POId!.Value,
            TotalPrice = pi.TotalPrice,
            // Legacy FinalPrice edits keep the quote total and store the real deduction in Discount (see TotalPNService).
            SellTotal = ii.QuoteItem != null && ii.UnitPrice == ii.QuoteItem.UnitPrice && (ii.Discount ?? 0m) != 0m
                ? ii.TotalPrice - (ii.Discount ?? 0m)
                : ii.TotalPrice,
        };

    private static void Finish(BIMetrics m)
    {
        m.BoughtValue = Math.Round(m.BoughtValue, 2);
        m.SupplierCost = Math.Round(m.SupplierCost, 2);
        m.Fees = Math.Round(m.Fees, 2);
        m.FeesEstimated = Math.Round(m.FeesEstimated, 2);
        m.Profit = m.BoughtValue - m.SupplierCost - m.Fees;
    }

    private static void ApplyFinalDays(BIMetrics metrics, List<double> days)
    {
        metrics.FinalizedCount = days.Count;
        if (days.Count == 0) return;
        metrics.AvgDaysToFinal = Math.Round(days.Average(), 1);
        metrics.MinDaysToFinal = Math.Round(days.Min(), 1);
        metrics.MaxDaysToFinal = Math.Round(days.Max(), 1);
    }

    private static DateTime BucketStart(DateTime day, string bucket) => bucket switch
    {
        // ISO weeks start on Monday.
        "week" => day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        "month" => new DateTime(day.Year, day.Month, 1),
        _ => day.Date,
    };

    private static DateTime NextBucket(DateTime start, string bucket) => bucket switch
    {
        "week" => start.AddDays(7),
        "month" => start.AddMonths(1),
        _ => start.AddDays(1),
    };
}

public class BIPoLine
{
    public long InvoiceId { get; set; }
    public long InvoiceItemId { get; set; }
    public long POId { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal SellTotal { get; set; }
}

public class BIQuery
{
    /// <summary>Customers to report on; null or empty = every customer the caller may see.</summary>
    public List<long>? CustomerIds { get; set; }
    /// <summary>Customer bases to report on (e.g. every Base 5 customer); null or empty = no base filter.</summary>
    public List<int>? Bases { get; set; }
    /// <summary>Window length in days, ending today (UTC).</summary>
    public int Days { get; set; }
}

public class BIProformaQuery : BIQuery
{
    public int Skip { get; set; }
    public int Take { get; set; } = 25;
    /// <summary>createdAt (default) | boughtValue | supplierCost | fees | profit | daysToFinal | customer</summary>
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; } = true;
}

public class BICustomerOption
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int? Base { get; set; }
}

public class BIBaseOption
{
    public int Base { get; set; }
    public int CustomerCount { get; set; }
}

public class BIMetrics
{
    public int RfqCount { get; set; }
    public int SentQuoteCount { get; set; }
    public decimal SentQuoteSum { get; set; }
    public int PiCount { get; set; }
    /// <summary>Proformas with at least one line in a live PO.</summary>
    public int BoughtPiCount { get; set; }
    /// <summary>Proformas with only some of their lines in a PO.</summary>
    public int PartialPoCount { get; set; }
    /// <summary>Sell value of the proforma lines that are in a PO (paid by the customer or not).</summary>
    public decimal BoughtValue { get; set; }
    /// <summary>PO line cost of those lines.</summary>
    public decimal SupplierCost { get; set; }
    /// <summary>Shipping, wire fee, tax, processing and "Bank Fee and Others" from the wallet withdraws.</summary>
    public decimal Fees { get; set; }
    /// <summary>Part of Fees still taken from unpaid POs' written amounts.</summary>
    public decimal FeesEstimated { get; set; }
    /// <summary>BoughtValue − SupplierCost − Fees.</summary>
    public decimal Profit { get; set; }
    /// <summary>POs behind these proformas not fully paid yet (their cost is still the PO amount).</summary>
    public int UnpaidPoCount { get; set; }
    /// <summary>Fee withdraws left out because their currency could not be converted (totals only).</summary>
    public int UnconvertedFeeCount { get; set; }
    /// <summary>Proformas with at least one final invoice.</summary>
    public int FinalizedCount { get; set; }
    public double? AvgDaysToFinal { get; set; }
    public double? MinDaysToFinal { get; set; }
    public double? MaxDaysToFinal { get; set; }
}

public class BICustomerRow : BIMetrics
{
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public int? Base { get; set; }
}

public class BITrendPoint
{
    public DateTime Start { get; set; }
    public int Rfqs { get; set; }
    public int SentQuotes { get; set; }
    public decimal SentQuoteSum { get; set; }
    public int Pis { get; set; }
    /// <summary>Bought value of the proformas created in this bucket.</summary>
    public decimal BoughtValue { get; set; }
}

public class BISummaryResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Days { get; set; }
    /// <summary>day | week | month — the trend's bucket size.</summary>
    public string Bucket { get; set; } = "day";
    public BIMetrics Totals { get; set; } = new();
    public List<BICustomerRow> Customers { get; set; } = [];
    public List<BITrendPoint> Trend { get; set; } = [];
}

public class BIProformaRow
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? B1ProformaInvoiceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? QuoteNumber { get; set; }
    /// <summary>Whole proforma total, for context only.</summary>
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
    public int BoughtLineCount { get; set; }
    public int PoCount { get; set; }
    public int UnpaidPoCount { get; set; }
    /// <summary>Null when nothing on this proforma is in a PO yet.</summary>
    public decimal? BoughtValue { get; set; }
    public decimal? SupplierCost { get; set; }
    public decimal? Fees { get; set; }
    public decimal? FeesEstimated { get; set; }
    public decimal? Profit { get; set; }
    public decimal? MarginPercent { get; set; }
    public DateTime? FirstFinalAt { get; set; }
    public DateTime? LastFinalAt { get; set; }
    public int FinalCount { get; set; }
    /// <summary>Days from proforma creation to its first final invoice.</summary>
    public double? DaysToFinal { get; set; }
}

public class BIProformaPage
{
    public int Total { get; set; }
    public List<BIProformaRow> Items { get; set; } = [];
}
