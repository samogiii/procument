using Microsoft.EntityFrameworkCore;
using Procument.Shared.DTOs;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public interface IFinalInvoiceService
{
    Task<PagedResult<FinalInvoiceListItem>> GetAllAsync(PageQuery page, string? customerSearch = null, bool isSuperAdmin = true, int[]? userBases = null, string? pnSearch = null, DateTime? createdFrom = null, DateTime? createdTo = null, List<string>? customerCodes = null, List<string>? statuses = null, string? sortBy = null, bool sortDesc = false);
    Task<object> GetFilterOptionsAsync(string? search = null, string? customerSearch = null, bool isSuperAdmin = true, int[]? userBases = null, string? pnSearch = null, DateTime? createdFrom = null, DateTime? createdTo = null, List<string>? customerCodes = null, List<string>? statuses = null);
    Task<FinalInvoiceResponse?> GetByIdAsync(long id);
    Task<FinalInvoiceResponse> CreateFromProformaAsync(long proformaInvoiceId, List<long> invoiceItemIds);
    Task<FinalInvoiceCandidatesResponse?> GetCandidatesAsync(long proformaInvoiceId);
    Task<List<InvoicedPartResponse>> GetInvoicedPartsAsync(long? proformaInvoiceId, long? poId);
    Task<ReleaseFinalInvoicePartsResponse> ReleasePartsAsync(List<long> invoiceItemIds);
    Task<bool> UpdateStatusAsync(long id, string status);
    Task<bool> UpdateAsync(long id, UpdateFinalInvoiceRequest request);
    Task<bool> UpdateB1FinalInvoiceNumberAsync(long id, string? b1FinalInvoiceNumber);
    Task<bool> CanCreateFinalInvoice(long proformaInvoiceId);
    Task<List<EligibleProformaResponse>> GetEligibleProformasAsync();
    Task<string?> GetShippingMethodAsync(long finalInvoiceId);
}

public class FinalInvoiceService : IFinalInvoiceService
{
    private readonly DbContext _db;
    private readonly IB1NumberService _b1Service;

    public FinalInvoiceService(DbContext db, IB1NumberService b1Service)
    {
        _db = db;
        _b1Service = b1Service;
    }

    /// <summary>
    /// List endpoint — paginated and projected to a flat DTO so the query is a single
    /// SELECT with only the columns the list page actually displays. Avoids the 5-level
    /// Include/ThenInclude chain used by <see cref="GetByIdAsync"/>.
    /// </summary>
    public async Task<PagedResult<FinalInvoiceListItem>> GetAllAsync(PageQuery page, string? customerSearch = null, bool isSuperAdmin = true, int[]? userBases = null, string? pnSearch = null, DateTime? createdFrom = null, DateTime? createdTo = null, List<string>? customerCodes = null, List<string>? statuses = null, string? sortBy = null, bool sortDesc = false)
    {
        var q = _db.Set<FinalInvoice>().AsNoTracking().AsQueryable();

        if (!isSuperAdmin && userBases != null)
            q = q.Where(fi => fi.Customer == null || fi.Customer.Base == null || userBases.Contains(fi.Customer.Base.Value));

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            q = q.Where(fi =>
                fi.InvoiceNumber.Contains(s)
             || (fi.B1FinalInvoiceNumber != null && fi.B1FinalInvoiceNumber.Contains(s))
             || fi.Status.Contains(s)
             || (fi.Customer != null && (fi.Customer.Name.Contains(s) || (fi.Customer.CustomerCode != null && fi.Customer.CustomerCode.Contains(s))))
             || (fi.ProformaInvoice != null && (fi.ProformaInvoice.InvoiceNumber.Contains(s) || (fi.ProformaInvoice.B1InvoiceNumber != null && fi.ProformaInvoice.B1InvoiceNumber.Contains(s))))
             || fi.Items.Any(item =>
                    (item.PartNumber != null && item.PartNumber.Name.Contains(s)) ||
                    (item.PartNumber != null && item.PartNumber.Description != null && item.PartNumber.Description.Contains(s)) ||
                    (item.Condition != null && item.Condition.Contains(s))));
        }

        if (!string.IsNullOrWhiteSpace(customerSearch))
        {
            var cs = customerSearch.Trim();
            q = q.Where(fi => fi.Customer != null && fi.Customer.Name.Contains(cs));
        }

        if (customerCodes?.Count > 0)
        {
            var codes = customerCodes.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (codes.Count > 0)
                q = q.Where(fi => fi.Customer != null && codes.Contains(fi.Customer.CustomerCode));
        }

        if (statuses?.Count > 0)
        {
            var sts = statuses.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (sts.Count > 0)
                q = q.Where(fi => sts.Contains(fi.Status));
        }

        if (!string.IsNullOrWhiteSpace(pnSearch))
        {
            var s = pnSearch.Trim();
            q = q.Where(fi => fi.Items.Any(item =>
                (item.PartNumber != null && item.PartNumber.Name.Contains(s)) ||
                (item.InvoiceItem != null && item.InvoiceItem.QuoteItem != null && item.InvoiceItem.QuoteItem.Alt != null && item.InvoiceItem.QuoteItem.Alt.Contains(s))
            ));
        }

        if (createdFrom.HasValue)
            q = q.Where(fi => fi.CreatedAt >= createdFrom.Value);

        if (createdTo.HasValue)
            q = q.Where(fi => fi.CreatedAt <= createdTo.Value.AddDays(1).AddTicks(-1));

        IQueryable<FinalInvoice> ordered = sortBy switch
        {
            "invoiceNumber"         => sortDesc ? q.OrderByDescending(fi => fi.InvoiceNumber) : q.OrderBy(fi => fi.InvoiceNumber),
            "customerCode"          => sortDesc ? q.OrderByDescending(fi => fi.Customer != null ? fi.Customer.CustomerCode : "") : q.OrderBy(fi => fi.Customer != null ? fi.Customer.CustomerCode : ""),
            "proformaInvoiceNumber" => sortDesc ? q.OrderByDescending(fi => fi.ProformaInvoice != null ? fi.ProformaInvoice.InvoiceNumber : "") : q.OrderBy(fi => fi.ProformaInvoice != null ? fi.ProformaInvoice.InvoiceNumber : ""),
            "totalAmount"           => sortDesc ? q.OrderByDescending(fi => fi.TotalAmount) : q.OrderBy(fi => fi.TotalAmount),
            "status"                => sortDesc ? q.OrderByDescending(fi => fi.Status) : q.OrderBy(fi => fi.Status),
            "dueDate"               => sortDesc ? q.OrderByDescending(fi => fi.DueDate) : q.OrderBy(fi => fi.DueDate),
            "paidDate"              => sortDesc ? q.OrderByDescending(fi => fi.PaidDate) : q.OrderBy(fi => fi.PaidDate),
            _                       => q.OrderByDescending(fi => fi.CreatedAt),
        };

        var projected = ordered.Select(fi => new FinalInvoiceListItem
        {
            Id = fi.Id,
            InvoiceNumber = fi.InvoiceNumber,
            B1InvoiceNumber = fi.B1FinalInvoiceNumber,
            TotalAmount = fi.TotalAmount,
            Status = fi.Status,
            CreatedAt = fi.CreatedAt,
            DueDate = fi.DueDate,
            PaidDate = fi.PaidDate,
            ProformaInvoiceId = fi.ProformaInvoiceId,
            ProformaInvoiceNumber = fi.ProformaInvoice != null ? fi.ProformaInvoice.InvoiceNumber : "",
            B1ProformaInvoiceNumber = fi.ProformaInvoice != null ? fi.ProformaInvoice.B1InvoiceNumber : null,
            CustomerId = fi.CustomerId,
            CustomerName = fi.Customer != null ? fi.Customer.Name : "",
            CustomerCode = fi.Customer != null ? fi.Customer.CustomerCode : null,
            ItemCount = fi.Items.Count()
        });

        return await projected.ToPagedResultAsync(page);
    }

    /// <summary>
    /// Cascading filter options. Each list is built from the rows that survive the *other*
    /// active filters, so picking a customer narrows the status list and vice versa, while
    /// a column never collapses to the values already selected in it. Called with no
    /// arguments it returns the full lists the client caches behind "Show all".
    /// </summary>
    public async Task<object> GetFilterOptionsAsync(string? search = null, string? customerSearch = null, bool isSuperAdmin = true, int[]? userBases = null, string? pnSearch = null, DateTime? createdFrom = null, DateTime? createdTo = null, List<string>? customerCodes = null, List<string>? statuses = null)
    {
        var permitted = _db.Set<FinalInvoice>().AsNoTracking().AsQueryable();

        if (!isSuperAdmin && userBases != null)
            permitted = permitted.Where(fi => fi.Customer == null || fi.Customer.Base == null || userBases.Contains(fi.Customer.Base.Value));

        var codeValues = (customerCodes ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var statusValues = (statuses ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        IQueryable<FinalInvoice> Build(string? exclude)
        {
            var q = permitted;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(fi =>
                    fi.InvoiceNumber.Contains(s)
                 || (fi.B1FinalInvoiceNumber != null && fi.B1FinalInvoiceNumber.Contains(s))
                 || fi.Status.Contains(s)
                 || (fi.Customer != null && (fi.Customer.Name.Contains(s) || (fi.Customer.CustomerCode != null && fi.Customer.CustomerCode.Contains(s))))
                 || (fi.ProformaInvoice != null && (fi.ProformaInvoice.InvoiceNumber.Contains(s) || (fi.ProformaInvoice.B1InvoiceNumber != null && fi.ProformaInvoice.B1InvoiceNumber.Contains(s)))));
            }

            if (!string.IsNullOrWhiteSpace(customerSearch))
            {
                var cs = customerSearch.Trim();
                q = q.Where(fi => fi.Customer != null && fi.Customer.Name.Contains(cs));
            }

            if (!string.IsNullOrWhiteSpace(pnSearch))
            {
                var s = pnSearch.Trim();
                q = q.Where(fi => fi.Items.Any(item =>
                    (item.PartNumber != null && item.PartNumber.Name.Contains(s)) ||
                    (item.InvoiceItem != null && item.InvoiceItem.QuoteItem != null && item.InvoiceItem.QuoteItem.Alt != null && item.InvoiceItem.QuoteItem.Alt.Contains(s))));
            }

            if (exclude != "customerCode" && codeValues.Count > 0)
                q = q.Where(fi => fi.Customer != null && codeValues.Contains(fi.Customer.CustomerCode));

            if (exclude != "status" && statusValues.Count > 0)
                q = q.Where(fi => statusValues.Contains(fi.Status));

            if (createdFrom.HasValue)
                q = q.Where(fi => fi.CreatedAt >= createdFrom.Value);

            if (createdTo.HasValue)
                q = q.Where(fi => fi.CreatedAt <= createdTo.Value.AddDays(1).AddTicks(-1));

            return q;
        }

        var availableStatuses = await Build("status").Select(fi => fi.Status).Distinct().OrderBy(s => s).ToListAsync();
        var availableCustomers = await Build("customerCode")
            .Where(fi => fi.Customer != null)
            .Select(fi => new { code = fi.Customer!.CustomerCode, name = fi.Customer!.Name })
            .Distinct()
            .ToListAsync();
        return new
        {
            statuses = availableStatuses,
            customers = availableCustomers.GroupBy(c => c.code).Select(g => g.First()).OrderBy(c => c.code).ToList()
        };
    }

    public async Task<FinalInvoiceResponse?> GetByIdAsync(long id)
    {
        var fi = await _db.Set<FinalInvoice>()
            .Include(f => f.Customer)
            .Include(f => f.ProformaInvoice)
                .ThenInclude(pi => pi.Quote)
            .Include(f => f.Items).ThenInclude(i => i.PartNumber)
            .Include(f => f.Items).ThenInclude(i => i.InvoiceItem)
                .ThenInclude(ii => ii!.QuoteItem)
                    .ThenInclude(qi => qi!.RFQItem)
                        .ThenInclude(ri => ri!.RFQ)
                            .ThenInclude(r => r!.RFQItems)
            .FirstOrDefaultAsync(f => f.Id == id);

        return fi == null ? null : MapToResponse(fi);
    }

    private const string CancelledLineStatus = "Cancelled";

    /// <summary>
    /// Track statuses from "Received in Warehouse" onwards — the part reached the warehouse, so it can
    /// be final-invoiced. "Ship to Warehouse" (still on the way) and "Rejected" don't count.
    /// </summary>
    private static readonly string[] ReadyTrackStatuses =
    [
        "Received in Warehouse", "Waiting for Packing", "Ship To USA",
        "Clearing Customs", "Received in Office", "Delivered to Customer",
    ];

    /// <summary>PO-line tracks whose parts were received in the warehouse (or went further).</summary>
    private IQueryable<long> ReadyTrackIds() =>
        _db.Set<POItemTrackNumber>()
            .Where(t => ReadyTrackStatuses.Contains(t.Status))
            .Select(t => t.Id);

    /// <summary>
    /// Sales Order lines that can go on a Final Invoice right now: the Sales Order is live, the line
    /// is not cancelled and not on a Final Invoice yet, and its live PO line has at least one ready
    /// track (see <see cref="ReadyTrackIds"/>). A Sales Order is invoiced part by part through this rule.
    /// </summary>
    private IQueryable<InvoiceItem> InvoiceableItems()
    {
        var readyTrackIds = ReadyTrackIds();
        return _db.Set<InvoiceItem>().Where(ii =>
            !ii.Invoice.IsCancelled
            && ii.Status != CancelledLineStatus
            && !_db.Set<FinalInvoiceItem>().Any(f => f.InvoiceItemId == ii.Id)
            && _db.Set<POItem>().Any(pi => pi.InvoiceItemId == ii.Id
                && pi.ReturnedAt == null
                && pi.TrackNumbers.Any(t => readyTrackIds.Contains(t.Id))));
    }

    public async Task<List<EligibleProformaResponse>> GetEligibleProformasAsync()
    {
        var eligibleCounts = await InvoiceableItems()
            .GroupBy(ii => ii.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Count);
        if (eligibleCounts.Count == 0) return new();

        var ids = eligibleCounts.Keys.ToList();
        var lines = await _db.Set<InvoiceItem>().AsNoTracking()
            .Where(ii => ids.Contains(ii.InvoiceId) && ii.Status != CancelledLineStatus)
            .Select(ii => new { ii.InvoiceId, Invoiced = _db.Set<FinalInvoiceItem>().Any(f => f.InvoiceItemId == ii.Id) })
            .ToListAsync();
        var lineStats = lines.GroupBy(l => l.InvoiceId)
            .ToDictionary(g => g.Key, g => (Total: g.Count(), Invoiced: g.Count(l => l.Invoiced)));

        var eligibleProformas = await _db.Set<Invoice>().AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => ids.Contains(i.Id))
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return eligibleProformas.Select(i => new EligibleProformaResponse
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            B1ProformaInvoiceNumber = i.B1InvoiceNumber,
            CustomerName = i.Customer?.Name ?? "",
            CustomerCode = i.Customer?.CustomerCode,
            TotalAmount = i.TotalAmount,
            EligibleItemCount = eligibleCounts[i.Id],
            InvoicedItemCount = lineStats.TryGetValue(i.Id, out var s) ? s.Invoiced : 0,
            TotalItemCount = lineStats.TryGetValue(i.Id, out var t) ? t.Total : 0,
        }).ToList();
    }

    /// <summary>
    /// Every live line of a Sales Order for the Create Final Invoice part picker — selectable ones
    /// plus the rest with the reason they can't be picked yet. Null when the Sales Order doesn't exist.
    /// </summary>
    public async Task<FinalInvoiceCandidatesResponse?> GetCandidatesAsync(long proformaInvoiceId)
    {
        var proforma = await _db.Set<Invoice>().AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.QuoteItem).ThenInclude(qi => qi!.PartNumber)
            .FirstOrDefaultAsync(i => i.Id == proformaInvoiceId);
        if (proforma == null) return null;

        var lines = proforma.InvoiceItems.Where(ii => ii.Status != CancelledLineStatus).OrderBy(ii => ii.Id).ToList();
        var lineIds = lines.Select(ii => ii.Id).ToList();

        var readyTrackIds = ReadyTrackIds();
        var tracks = await _db.Set<POItemTrackNumber>().AsNoTracking()
            .Where(t => t.POItem.InvoiceItemId != null && lineIds.Contains(t.POItem.InvoiceItemId.Value) && t.POItem.ReturnedAt == null)
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                InvoiceItemId = t.POItem.InvoiceItemId!.Value,
                t.TrackNumber,
                Ready = readyTrackIds.Contains(t.Id),
            })
            .ToListAsync();
        var tracksByLine = tracks.ToLookup(t => t.InvoiceItemId);

        var linesWithPo = (await _db.Set<POItem>().AsNoTracking()
            .Where(pi => pi.InvoiceItemId != null && lineIds.Contains(pi.InvoiceItemId.Value) && pi.ReturnedAt == null)
            .Select(pi => pi.InvoiceItemId!.Value)
            .ToListAsync()).ToHashSet();

        var invoiced = (await _db.Set<FinalInvoiceItem>().AsNoTracking()
            .Where(f => f.InvoiceItemId != null && lineIds.Contains(f.InvoiceItemId.Value))
            .Select(f => new { InvoiceItemId = f.InvoiceItemId!.Value, f.FinalInvoiceId, f.FinalInvoice.InvoiceNumber })
            .ToListAsync())
            .GroupBy(f => f.InvoiceItemId)
            .ToDictionary(g => g.Key, g => g.First());

        return new FinalInvoiceCandidatesResponse
        {
            ProformaInvoiceId = proforma.Id,
            InvoiceNumber = proforma.InvoiceNumber,
            B1ProformaInvoiceNumber = proforma.B1InvoiceNumber,
            CustomerName = proforma.Customer?.Name ?? "",
            CustomerCode = proforma.Customer?.CustomerCode,
            Items = lines.Select(ii =>
            {
                var lineTracks = tracksByLine[ii.Id].ToList();
                var finished = lineTracks.Where(t => t.Ready).Select(t => t.TrackNumber).Distinct().ToList();
                var open = lineTracks.Where(t => !t.Ready).Select(t => t.TrackNumber).Distinct().ToList();
                invoiced.TryGetValue(ii.Id, out var onInvoice);

                string? reason =
                    proforma.IsCancelled ? "Sales Order is cancelled"
                    : onInvoice != null ? $"Already on {onInvoice.InvoiceNumber}"
                    : !linesWithPo.Contains(ii.Id) ? "No PO yet"
                    : lineTracks.Count == 0 ? "No track number yet"
                    : finished.Count == 0 ? "Not received in warehouse yet"
                    : null;

                return new FinalInvoiceCandidateItem
                {
                    InvoiceItemId = ii.Id,
                    PartNumber = ii.QuoteItem?.PartNumber?.Name ?? "",
                    Alt = ii.QuoteItem?.Alt,
                    Description = ii.QuoteItem?.PartNumber?.Description,
                    Qty = ii.Qty,
                    UnitPrice = ii.UnitPrice,
                    TotalPrice = ii.TotalPrice,
                    Condition = ii.Condition ?? ii.QuoteItem?.Condition,
                    Status = ii.Status,
                    FinishedTrackNumbers = finished,
                    OpenTrackNumbers = open,
                    FinalInvoiceId = onInvoice?.FinalInvoiceId,
                    FinalInvoiceNumber = onInvoice?.InvoiceNumber,
                    Eligible = reason == null,
                    Reason = reason,
                };
            }).ToList()
        };
    }

    /// <summary>True while the Sales Order still has at least one part that can be final-invoiced.</summary>
    public async Task<bool> CanCreateFinalInvoice(long proformaInvoiceId)
    {
        return await InvoiceableItems().AnyAsync(ii => ii.InvoiceId == proformaInvoiceId);
    }

    /// <summary>
    /// Create a Final Invoice for the selected Sales Order lines. Each line must still be invoiceable
    /// (see <see cref="InvoiceableItems"/>); the rest of the Sales Order stays open for later Final Invoices.
    /// </summary>
    public async Task<FinalInvoiceResponse> CreateFromProformaAsync(long proformaInvoiceId, List<long> invoiceItemIds)
    {
        var ids = (invoiceItemIds ?? new List<long>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one part for the Final Invoice.");

        var strategy = _db.Database.CreateExecutionStrategy();
        var finalInvoiceId = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Serialises Final Invoice creation per Sales Order, so two users can't invoice the same part twice.
            await _db.Database
                .SqlQuery<long>($"SELECT Id AS [Value] FROM ProformaInvoices WITH (UPDLOCK, HOLDLOCK) WHERE Id = {proformaInvoiceId}")
                .ToListAsync();

            var proforma = await _db.Set<Invoice>()
                .Include(i => i.InvoiceItems)
                    .ThenInclude(ii => ii.QuoteItem)
                        .ThenInclude(qi => qi!.ProcumentRecord)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(ii => ii.QuoteItem)
                        .ThenInclude(qi => qi!.PartNumber)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == proformaInvoiceId);

            if (proforma == null)
                throw new InvalidOperationException("Sales Order not found.");
            if (proforma.IsCancelled)
                throw new InvalidOperationException("This Sales Order is cancelled.");

            var invoiceable = await InvoiceableItems()
                .Where(ii => ii.InvoiceId == proformaInvoiceId && ids.Contains(ii.Id))
                .Select(ii => ii.Id)
                .ToListAsync();
            if (invoiceable.Count != ids.Count)
                throw new InvalidOperationException(
                    "Some selected parts can't be invoiced: each part needs a track number received in the warehouse and must not be on another Final Invoice. Refresh and try again.");

            var selected = proforma.InvoiceItems.Where(ii => ids.Contains(ii.Id)).OrderBy(ii => ii.Id).ToList();

            if (proforma.PaymentStatus == InvoicePaymentTerms.Credit)
                await InvoicePaymentTerms.EnsureCreditAvailableAsync(_db, proforma.CustomerId, proforma.TotalAmount, proforma.Id);

            if (proforma.PaymentStatus is InvoicePaymentTerms.Net or InvoicePaymentTerms.Cad)
                proforma.PaymentTermStartedAt ??= DateTime.UtcNow;
            if (proforma.PaymentStatus != InvoicePaymentTerms.Prepayment && proforma.Status != "Finish")
                proforma.Status = "Running";

            // Ready tracks of the live (non-returned) PO lines — the shipments this invoice bills for.
            var readyTrackIds = ReadyTrackIds();
            var finishedTracks = await _db.Set<POItemTrackNumber>()
                .Where(t => t.POItem.InvoiceItemId != null && ids.Contains(t.POItem.InvoiceItemId.Value)
                         && t.POItem.ReturnedAt == null && readyTrackIds.Contains(t.Id))
                .OrderBy(t => t.Id)
                .Select(t => new { InvoiceItemId = t.POItem.InvoiceItemId!.Value, t.TrackNumber, t.Carrier })
                .ToListAsync();
            var tracksByLine = finishedTracks.ToLookup(t => t.InvoiceItemId);

            var finalInvoice = new FinalInvoice
            {
                // Unique placeholder, replaced with "INV-{Id}" right after the insert. Invoices.InvoiceNumber
                // has a unique index, so a fixed placeholder left behind by a failed creation would block
                // every later final invoice.
                InvoiceNumber = $"TMP-{Guid.NewGuid():N}",
                // Same digits as the proforma's B1 number, re-lettered I (null when the proforma has none).
                B1FinalInvoiceNumber = _b1Service.Relabel(proforma.B1InvoiceNumber, B1NumberService.FinalInvoiceLetter),
                // Only the selected parts — same rule as the Sales Order total (sum of line TotalPrice).
                TotalAmount = selected.Sum(ii => ii.TotalPrice),
                Status = "Draft",
                ProformaInvoiceId = proforma.Id,
                CustomerId = proforma.CustomerId,
                CreatedAt = DateTime.UtcNow,
            };

            _db.Set<FinalInvoice>().Add(finalInvoice);
            await _db.SaveChangesAsync(); // Get the ID

            finalInvoice.InvoiceNumber = $"INV-{finalInvoice.Id}";

            foreach (var ii in selected)
            {
                var quoteItem = ii.QuoteItem;
                var proc = quoteItem?.ProcumentRecord;
                var lineTracks = tracksByLine[ii.Id].ToList();

                // Resolve the effective PartNumber: if the expert quoted an alt to the customer,
                // find or create a PartNumber entry for the alt so the Final Invoice uses it.
                var effectivePartNumberId = quoteItem?.PartNumberId;
                var altName = quoteItem?.Alt?.Trim();
                if (!string.IsNullOrEmpty(altName))
                {
                    var altPn = await _db.Set<PartNumber>().FirstOrDefaultAsync(p => p.Name == altName);
                    if (altPn != null)
                        effectivePartNumberId = altPn.Id;
                    else
                    {
                        var newAltPn = new PartNumber { Name = altName, CreatedAt = DateTime.UtcNow };
                        _db.Set<PartNumber>().Add(newAltPn);
                        await _db.SaveChangesAsync();
                        effectivePartNumberId = newAltPn.Id;
                    }
                }

                _db.Set<FinalInvoiceItem>().Add(new FinalInvoiceItem
                {
                    FinalInvoiceId = finalInvoice.Id,
                    PartNumberId = effectivePartNumberId,
                    InvoiceItemId = ii.Id,
                    Qty = ii.Qty,
                    UnitPrice = ii.UnitPrice,   // Sell price from proforma
                    TotalPrice = ii.TotalPrice,
                    Discount = ii.Discount,     // Copy discount from proforma item
                    Condition = ii.Condition ?? quoteItem?.Condition,
                    CertName = proc?.CertName,
                    TrackNumber = Truncate(string.Join(", ", lineTracks.Select(t => t.TrackNumber).Distinct()), 500),
                    Carrier = Truncate(lineTracks.Select(t => t.Carrier).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "", 200),
                });
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return finalInvoice.Id;
        });

        return (await GetByIdAsync(finalInvoiceId))!;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    /// <summary>
    /// Parts of a Sales Order (or of a PO's live lines) that sit on a Final Invoice — what the user
    /// can take off again to unlock the Sales Order / PO.
    /// </summary>
    public async Task<List<InvoicedPartResponse>> GetInvoicedPartsAsync(long? proformaInvoiceId, long? poId)
    {
        if (!proformaInvoiceId.HasValue && !poId.HasValue) return new();

        var q = _db.Set<FinalInvoiceItem>().AsNoTracking().Where(f => f.InvoiceItemId != null);
        if (proformaInvoiceId.HasValue)
            q = q.Where(f => f.InvoiceItem!.InvoiceId == proformaInvoiceId.Value);
        if (poId.HasValue)
            q = q.Where(f => _db.Set<POItem>().Any(pi => pi.POId == poId.Value && pi.ReturnedAt == null && pi.InvoiceItemId == f.InvoiceItemId));

        return await q
            .OrderBy(f => f.FinalInvoiceId).ThenBy(f => f.Id)
            .Select(f => new InvoicedPartResponse
            {
                InvoiceItemId = f.InvoiceItemId!.Value,
                FinalInvoiceItemId = f.Id,
                PartNumber = f.PartNumber != null ? f.PartNumber.Name : "",
                Qty = f.Qty,
                TotalPrice = f.TotalPrice,
                TrackNumber = f.TrackNumber,
                FinalInvoiceId = f.FinalInvoiceId,
                FinalInvoiceNumber = f.FinalInvoice.InvoiceNumber,
                B1FinalInvoiceNumber = f.FinalInvoice.B1FinalInvoiceNumber,
                FinalInvoiceStatus = f.FinalInvoice.Status,
                FinalInvoiceItemCount = f.FinalInvoice.Items.Count(),
            })
            .ToListAsync();
    }

    /// <summary>
    /// Takes Sales Order lines off their Final Invoice so the Sales Order / PO unlock and the parts can be
    /// edited, returned or invoiced again. Each touched Final Invoice gets its total recalculated; one left
    /// with no parts is deleted (its uploaded documents stay in storage under its number).
    /// </summary>
    public async Task<ReleaseFinalInvoicePartsResponse> ReleasePartsAsync(List<long> invoiceItemIds)
    {
        var ids = (invoiceItemIds ?? new List<long>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one part to remove from its Final Invoice.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var finalItems = await _db.Set<FinalInvoiceItem>()
                .Where(f => f.InvoiceItemId != null && ids.Contains(f.InvoiceItemId.Value))
                .ToListAsync();
            if (finalItems.Count == 0)
                throw new InvalidOperationException("None of the selected parts are on a Final Invoice.");

            var removedIds = finalItems.Select(f => f.Id).ToHashSet();
            var finalInvoiceIds = finalItems.Select(f => f.FinalInvoiceId).Distinct().ToList();
            var finalInvoices = await _db.Set<FinalInvoice>()
                .Include(fi => fi.Items)
                .Where(fi => finalInvoiceIds.Contains(fi.Id))
                .ToListAsync();

            _db.Set<FinalInvoiceItem>().RemoveRange(finalItems);

            var result = new ReleaseFinalInvoicePartsResponse { ReleasedCount = finalItems.Count };
            foreach (var fi in finalInvoices.OrderBy(fi => fi.Id))
            {
                var remaining = fi.Items.Where(i => !removedIds.Contains(i.Id)).ToList();
                if (remaining.Count == 0)
                {
                    _db.Set<FinalInvoice>().Remove(fi);
                    result.DeletedFinalInvoices.Add(fi.InvoiceNumber);
                }
                else
                {
                    fi.TotalAmount = remaining.Sum(i => i.TotalPrice);
                    result.UpdatedFinalInvoices.Add(fi.InvoiceNumber);
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        });
    }

    public async Task<bool> UpdateStatusAsync(long id, string status)
    {
        var fi = await _db.Set<FinalInvoice>().FindAsync(id);
        if (fi == null) return false;

        fi.Status = status;
        if (status == "Paid") fi.PaidDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Sets the B1 final invoice number by hand — used both to fill one in where the source
    /// proforma had none and to correct the copy inherited from it. Blank clears it.
    /// Independent of the proforma's own B1 number.
    /// </summary>
    public async Task<bool> UpdateB1FinalInvoiceNumberAsync(long id, string? b1FinalInvoiceNumber)
    {
        var fi = await _db.Set<FinalInvoice>().FindAsync(id);
        if (fi == null) return false;

        fi.B1FinalInvoiceNumber = _b1Service.Normalize(b1FinalInvoiceNumber);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(long id, UpdateFinalInvoiceRequest request)
    {
        var fi = await _db.Set<FinalInvoice>().FindAsync(id);
        if (fi == null) return false;

        if (request.ShippingCost.HasValue) fi.ShippingCost = request.ShippingCost.Value;
        if (request.ShippingMethod != null) fi.ShippingMethod = request.ShippingMethod;
        if (request.Notes != null) fi.Notes = request.Notes;
        if (request.DueDate.HasValue) fi.DueDate = request.DueDate.Value;

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// How the invoice's parts went out, read from the shipment notes their live POItems' track numbers
    /// sit on: "DDP", "CPT", "Pick Up", or several joined when the parts shipped differently.
    /// Null while nothing is on a shipment note yet.
    /// </summary>
    public async Task<string?> GetShippingMethodAsync(long finalInvoiceId)
    {
        var invoiceItemIds = _db.Set<FinalInvoiceItem>()
            .Where(i => i.FinalInvoiceId == finalInvoiceId && i.InvoiceItemId != null)
            .Select(i => i.InvoiceItemId!.Value);

        var types = await _db.Set<ShipmentNoteTrackNumber>()
            .Where(snt => snt.TrackNumber.POItem.ReturnedAt == null
                       && snt.TrackNumber.POItem.InvoiceItemId != null
                       && invoiceItemIds.Contains(snt.TrackNumber.POItem.InvoiceItemId.Value))
            .Select(snt => snt.ShipmentNote.Type)
            .Distinct()
            .ToListAsync();

        if (types.Count == 0) return null;

        return string.Join(", ", types
            .OrderBy(t => t switch { "DDP" => 0, "CPT" => 1, _ => 2 })
            .Select(t => t == "PickUp" ? "Pick Up" : t));
    }

    private static FinalInvoiceResponse MapToResponse(FinalInvoice fi)
    {
        var rfqItemRank = fi.Items
            .Select(i => i.InvoiceItem?.QuoteItem?.RFQItem?.RFQ)
            .Where(r => r != null)
            .SelectMany(r => r!.RFQItems)
            .DistinctBy(ri => ri.Id)
            .OrderBy(ri => ri.Id)
            .Select((ri, idx) => new { ri.Id, rank = idx + 1 })
            .ToDictionary(x => x.Id, x => x.rank);

        return new()
        {
            Id = fi.Id,
            InvoiceNumber = fi.InvoiceNumber,
            B1InvoiceNumber = fi.B1FinalInvoiceNumber,
            TotalAmount = fi.TotalAmount,
            Status = fi.Status,
            ShippingMethod = fi.ShippingMethod,
            ShippingCost = fi.ShippingCost,
            Notes = fi.Notes,
            DueDate = fi.DueDate,
            PaidDate = fi.PaidDate,
            CreatedAt = fi.CreatedAt,
            ProformaInvoiceId = fi.ProformaInvoiceId,
            ProformaInvoiceNumber = fi.ProformaInvoice?.InvoiceNumber ?? "",
            B1ProformaInvoiceNumber = fi.ProformaInvoice?.B1InvoiceNumber,
            CustomerPONumber = fi.ProformaInvoice?.CustomerPONumber,
            CustomerId = fi.CustomerId,
            CustomerName = fi.Customer?.Name ?? "",
            CustomerBase = fi.Customer?.Base,
            CustomerCode = fi.Customer?.CustomerCode,
            CustomerContactPerson = fi.Customer?.ContactPerson,
            CustomerBillTo = fi.Customer?.BillTo,
            CustomerBillToEmail = fi.Customer?.Email,
            CustomerBillToPhone = fi.Customer?.Phone,
            CustomerTermsAndConditions = !string.IsNullOrWhiteSpace(fi.Customer?.PITermsAndConditions)
                ? fi.Customer.PITermsAndConditions
                : fi.Customer?.TermsAndConditions,
            CustomerCurrencyType = fi.Customer?.CurrencyType,
            CustomerContacts = fi.Customer?.Contacts,
            DefaultDepositWalletId = fi.ProformaInvoice?.DefaultDepositWalletId,
            DefaultBankAccountId = fi.ProformaInvoice?.DefaultBankAccountId,
            QuoteCoefYuan = fi.ProformaInvoice?.Quote?.CoefYuan,
            QuoteExchangeRateYuan = fi.ProformaInvoice?.Quote?.ExchangeRateYuan,
            //CustomerBillToContactPerson = fi.Customer?.ContactPerson,
            CustomerShipTo = fi.Customer?.ShipTo,
            CustomerShipToContactPerson = fi.Customer?.ContactPerson,
            CustomerShipToEmail = fi.Customer?.Email,
            CustomerShipToPhone = fi.Customer?.Phone,
            CustomerShipToAccount = fi.Customer?.ShippingAccount,
            Items = fi.Items.Select(i => new FinalInvoiceItemResponse
            {
                Id = i.Id,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                Discount = i.Discount,
                Condition = i.Condition,
                CertName = i.CertName,
                TrackNumber = i.TrackNumber,
                Carrier = i.Carrier,
                PartNumberId = i.PartNumberId,
                RFQReference = i.InvoiceItem?.QuoteItem?.RFQItemId.HasValue == true &&
                               rfqItemRank.TryGetValue(i.InvoiceItem.QuoteItem.RFQItemId!.Value, out var rank)
                               ? rank.ToString() : null,
                PartNumberName = i.PartNumber?.Name ?? "",
                Alt = i.InvoiceItem?.QuoteItem?.Alt,
                Description = i.PartNumber?.Description,
            }).ToList()
        };
    }
}
