using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;
using Procument.Shared.Services;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _poService;
    private readonly DbContext _db;
    private readonly IFinalInvoiceLockGuard _lockGuard;

    public PurchaseOrdersController(IPurchaseOrderService poService, DbContext db, IFinalInvoiceLockGuard lockGuard)
    {
        _poService = poService;
        _db = db;
        _lockGuard = lockGuard;
    }

    /// <summary>Get all purchase orders (paginated).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<POResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var result = await _poService.GetAllAsync(pq);
        return Ok(result);
    }

    /// <summary>Get all unassigned POItems (not yet assigned to a PO).</summary>
    [HttpGet("unassigned-items")]
    public async Task<ActionResult<List<UnassignedPOItemResponse>>> GetUnassignedItems()
    {
        var result = await _poService.GetUnassignedItemsAsync();
        return Ok(result);
    }

    /// <summary>Get a purchase order by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<POResponse>> GetById(long id)
    {
        var result = await _poService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new purchase order.</summary>
    /// TODO fix service later
    [HttpPost]
    [Auditable("PurchaseOrder", "Create", CaptureBody = true)]
    public async Task<ActionResult<POResponse>> Create([FromBody] CreatePORequest request)
    {
        var result = await _poService.CreateAsync(request);

        // Mark related RFQs as unread for all assigned users
        try
        {
            var rfqIds = await _db.Set<POItem>()
                .Where(i => i.POId == result.Id && i.ProcumentId != null)
                .Select(i => i.ProcumentRecord!.RFQItem.RFQId)
                .Distinct()
                .ToListAsync();

            foreach (var rfqId in rfqIds)
            {
                // Get RFQ owner
                var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
                var userIds = new HashSet<long>();
                if (rfq?.UserId != null) userIds.Add(rfq.UserId.Value);

                // Get users with permissions on this RFQ
                var permUserIds = await _db.Set<EntityPermission>()
                    .Where(p => p.EntityName == "RFQ" && p.EntityId == rfqId.ToString())
                    .Select(p => p.UserId)
                    .ToListAsync();
                foreach (var uid in permUserIds) userIds.Add(uid);

                foreach (var uid in userIds)
                {
                    var existing = await _db.Set<RFQUserRead>()
                        .FirstOrDefaultAsync(r => r.RFQId == rfqId && r.UserId == uid);

                    if (existing != null)
                    {
                        existing.IsRead = false;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _db.Set<RFQUserRead>().Add(new RFQUserRead
                        {
                            RFQId = rfqId,
                            UserId = uid,
                            IsRead = false,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
        }
        catch { /* Don't fail PO creation if unread marking fails */ }

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update purchase order status.
    /// Only SuperAdmin can Approve or Reject a PO (via admin-approval endpoint).
    /// Once Payment Done, Admin and Expert can change status to logistics steps.
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "SuperAdmin,Admin,Expert")]
    [Auditable("PurchaseOrder", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdatePOStatusRequest request)
    {
        if (await _lockGuard.IsPurchaseOrderLocked(id))
            return BadRequest(new { message = "This PO is locked because a Final Invoice has been created." });

        bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        bool isSuperAdmin = User.IsInRole("SuperAdmin");

        // Get PO info for notification
        var po = await _poService.GetByIdAsync(id);
        if (po == null) return NotFound();

        var success = await _poService.UpdateStatusAsync(id, request.Status, isAdmin, isSuperAdmin, request.RejectionNote);
        if (!success) return BadRequest(new { message = "Status change not allowed at this stage or role restricted." });

        // Notify all non-admin users about key PO status changes
        if (request.Status == "Completed" || request.Status == "Cancelled")
        {
            var expertIds = await _db.Set<User>().Where(u => u.Role != "Admin" && u.Role != "SuperAdmin" && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var uid in expertIds)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = uid,
                    Type = "StatusChange",
                    EntityName = "PurchaseOrder",
                    EntityId = id,
                    EntityNumber = po.PONumber,
                    Message = $"PO {po.PONumber} status changed to {request.Status}.",
                });
            }
            await _db.SaveChangesAsync();
        }

        return Ok();
    }

    /// <summary>Update a single PO item (supplier, qty, unitPrice).</summary>
    [HttpPut("items/{id:long}")]
    [Auditable("POItem", "Update", CaptureBody = true)]
    public async Task<IActionResult> UpdateItem(long id, [FromBody] UpdatePOItemRequest request)
    {
        request.Id = id;
        var success = await _poService.UpdateItemAsync(request);
        return success ? Ok() : NotFound();
    }

    /// <summary>Delete a purchase order.</summary>
    [HttpDelete("{id:long}")]
    [Auditable("PurchaseOrder", "Delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _poService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Get enriched PO data for PDF generation.</summary>
    [HttpGet("{id:long}/pdf-data")]
    public async Task<IActionResult> GetPdfData(long id)
    {
        var po = await _db.Set<PurchaseOrder>()
            .Include(p => p.Supplier)
            .Include(p => p.ImportDetail)
            .Include(p => p.POItems).ThenInclude(i => i.PartNumber)
            .Include(p => p.POItems).ThenInclude(i => i.ProcumentRecord!).ThenInclude(pr => pr.RFQItem).ThenInclude(ri => ri.RFQ).ThenInclude(r => r.Customer)
            .Include(p => p.POItems).ThenInclude(i => i.ProcumentRecord!).ThenInclude(pr => pr.Supplier)
            .Include(p => p.POItems).ThenInclude(i => i.ProcumentRecord!).ThenInclude(pr => pr.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();

        // Determine delivery address from first item's RFQ ExType
        // ExType: null/0 = Warehouse (Hong Kong), other = Customer Exwork
        var firstProc = po.POItems.FirstOrDefault(i => i.ProcumentRecord?.RFQItem?.RFQ != null)?.ProcumentRecord;
        var rfq = firstProc?.RFQItem?.RFQ;
        var customer = rfq?.Customer;
        var exType = rfq?.ExType;

        // Ordered By — the user who created the first procurement record
        var orderedByUser = po.POItems.FirstOrDefault(i => i.ProcumentRecord?.User != null)?.ProcumentRecord?.User;
        var orderedBy = orderedByUser?.Name ?? "";

        string deliverToName;
        string deliverToAddress;
        string deliverToPhone = "";
        string deliverToEmail = "";
        if (exType.HasValue && exType.Value != 0 && customer != null)
        {
            // Customer Exwork — deliver to customer address
            deliverToName = customer.Name;
            deliverToAddress = customer.ShipTo ?? customer.BillTo ?? "";
            deliverToPhone = customer.Phone ?? "";
            deliverToEmail = customer.Email ?? "";
        }
        else
        {
            // Warehouse — deliver to Hong Kong office
            deliverToName = "Warehouse — Hong Kong";
            deliverToAddress = "Unit 1203, 12/F, Tower 1, Lippo Centre, 89 Queensway, Admiralty, Hong Kong";
        }

        var items = po.POItems.Select(i => new
        {
            partNumber = i.PartNumber?.Name ?? "",
            description = i.PartNumber?.Description ?? "",
            qty = i.Qty,
            condition = i.Condition ?? "",
            certification = i.ProcumentRecord?.CertName ?? "",
            unitPrice = (double)i.UnitPrice,
            totalPrice = (double)i.TotalPrice,
            shippingCost = i.ProcumentRecord?.ShippingCost,
            note = i.ProcumentRecord?.Note ?? "",
        }).ToList();

        return Ok(new
        {
            poNumber = po.PONumber,
            status = po.Status,
            createdAt = po.CreatedAt,
            totalAmount = po.TotalAmount,
            orderedBy,
            supplier = new
            {
                name = po.Supplier?.Name ?? "",
                address = po.Supplier?.Address ?? "",
                phone = po.Supplier?.Phone ?? "",
                email = po.Supplier?.Email ?? "",
            },
            vendor = new
            {
                name = po.Supplier?.Name ?? "",
                address = po.Supplier?.Address ?? "",
                phone = po.Supplier?.Phone ?? "",
                email = po.Supplier?.Email ?? "",
            },
            deliverTo = new
            {
                name = deliverToName,
                address = deliverToAddress,
                phone = deliverToPhone,
                email = deliverToEmail,
            },
            importDetail = po.ImportDetail != null ? new
            {
                fedExAccount = po.ImportDetail.FedExAccount ?? "",
                servicePriority = po.ImportDetail.CourierName ?? "",
                shippingMethod = po.ImportDetail.ShippingMethod ?? "",
                incoterms = po.ImportDetail.Incoterms ?? "",
                comments = po.ImportDetail.Notes ?? "",
            } : null,
            items,
        });
    }

    // ═══════════════════════════════════════════
    // Import Details
    // ═══════════════════════════════════════════

    /// <summary>Get import details for a PO.</summary>
    [HttpGet("{poId:long}/import-detail")]
    public async Task<IActionResult> GetImportDetail(long poId)
    {
        var detail = await _db.Set<POImportDetail>()
            .FirstOrDefaultAsync(d => d.PurchaseOrderId == poId);

        if (detail == null)
            return Ok((POImportDetailResponse?)null);

        return Ok(new POImportDetailResponse
        {
            Id = detail.Id,
            PurchaseOrderId = detail.PurchaseOrderId,
            BankName = detail.BankName,
            BankAccountNumber = detail.BankAccountNumber,
            BankAddress = detail.BankAddress,
            BankCity = detail.BankCity,
            BankCountry = detail.BankCountry,
            FedExAccount = detail.FedExAccount,
            CourierName = detail.CourierName,
            ShippingMethod = detail.ShippingMethod,
            Incoterms = detail.Incoterms,
            Notes = detail.Notes,
        });
    }

    /// <summary>Create or update import details for a PO.</summary>
    [HttpPut("{poId:long}/import-detail")]
    public async Task<IActionResult> SaveImportDetail(long poId, [FromBody] SavePOImportDetailRequest request)
    {
        var po = await _db.Set<PurchaseOrder>().FindAsync(poId);
        if (po == null) return NotFound();

        var detail = await _db.Set<POImportDetail>()
            .FirstOrDefaultAsync(d => d.PurchaseOrderId == poId);

        if (detail == null)
        {
            detail = new POImportDetail { PurchaseOrderId = poId };
            _db.Set<POImportDetail>().Add(detail);
        }

        detail.BankName = request.BankName;
        detail.BankAccountNumber = request.BankAccountNumber;
        detail.BankAddress = request.BankAddress;
        detail.BankCity = request.BankCity;
        detail.BankCountry = request.BankCountry;
        detail.FedExAccount = request.FedExAccount;
        detail.CourierName = request.CourierName;
        detail.ShippingMethod = request.ShippingMethod;
        detail.Incoterms = request.Incoterms;
        detail.Notes = request.Notes;

        await _db.SaveChangesAsync();

        return Ok(new POImportDetailResponse
        {
            Id = detail.Id,
            PurchaseOrderId = detail.PurchaseOrderId,
            BankName = detail.BankName,
            BankAccountNumber = detail.BankAccountNumber,
            BankAddress = detail.BankAddress,
            BankCity = detail.BankCity,
            BankCountry = detail.BankCountry,
            FedExAccount = detail.FedExAccount,
            CourierName = detail.CourierName,
            ShippingMethod = detail.ShippingMethod,
            Incoterms = detail.Incoterms,
            Notes = detail.Notes,
        });
    }

    // ═══════════════════════════════════════════
    // Track Numbers
    // ═══════════════════════════════════════════

    /// <summary>Get all track numbers for a PO item.</summary>
    [HttpGet("items/{poItemId:long}/track-numbers")]
    public async Task<ActionResult<List<TrackNumberResponse>>> GetTrackNumbers(long poItemId)
    {
        var tracks = await _db.Set<POItemTrackNumber>()
            .Where(t => t.POItemId == poItemId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TrackNumberResponse
            {
                Id = t.Id,
                POItemId = t.POItemId,
                TrackNumber = t.TrackNumber,
                Carrier = t.Carrier,
                Notes = t.Notes,
                CreatedAt = t.CreatedAt,
            })
            .ToListAsync();

        return Ok(tracks);
    }

    /// <summary>Add a track number to a PO item.</summary>
    [HttpPost("items/{poItemId:long}/track-numbers")]
    public async Task<IActionResult> AddTrackNumber(long poItemId, [FromBody] SaveTrackNumberRequest request)
    {
        var item = await _db.Set<POItem>().FindAsync(poItemId);
        if (item == null) return NotFound();

        var track = new POItemTrackNumber
        {
            POItemId = poItemId,
            TrackNumber = request.TrackNumber,
            Carrier = request.Carrier,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Set<POItemTrackNumber>().Add(track);
        await _db.SaveChangesAsync();

        return Ok(new TrackNumberResponse
        {
            Id = track.Id,
            POItemId = track.POItemId,
            TrackNumber = track.TrackNumber,
            Carrier = track.Carrier,
            Notes = track.Notes,
            CreatedAt = track.CreatedAt,
        });
    }

    /// <summary>Delete a track number.</summary>
    [HttpDelete("track-numbers/{trackId:long}")]
    public async Task<IActionResult> DeleteTrackNumber(long trackId)
    {
        var track = await _db.Set<POItemTrackNumber>().FindAsync(trackId);
        if (track == null) return NotFound();

        _db.Set<POItemTrackNumber>().Remove(track);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────
    // Admin Approval + Payment Workflow
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Approve or reject a PO. When a PO is created it is locked for Admins and regular users —
    /// only SuperAdmin can change the AdminApproval state.
    /// </summary>
    [HttpPatch("{id:long}/admin-approval")]
    [Authorize(Roles = "SuperAdmin")]
    [Auditable("PurchaseOrder", "AdminApproval", CaptureBody = true)]
    public async Task<IActionResult> SetAdminApproval(long id, [FromBody] UpdateAdminApprovalRequest request)
    {
        if (request.Decision != "Approved" && request.Decision != "Rejected" && request.Decision != "Pending")
            return BadRequest(new { message = "Decision must be Approved, Rejected, or Pending." });

        var po = await _db.Set<PurchaseOrder>().FindAsync(id);
        if (po == null) return NotFound();

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);

        po.AdminApproval = request.Decision;
        po.AdminApprovalNote = request.Note;
        po.AdminApprovalAt = DateTime.UtcNow;
        po.AdminApprovalBy = userId > 0 ? userId : null;

        if (request.Decision == "Approved")
        {
            po.Status = "Waiting For Payment";
        }

        await _db.SaveChangesAsync();

        // Notify Payment users when approved
        if (request.Decision == "Approved")
        {
            var paymentUserIds = await _db.Set<User>()
                .Where(u => u.Role == "Payment" && u.IsActive)
                .Select(u => u.Id).ToListAsync();
            foreach (var uid in paymentUserIds)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = uid,
                    Type = "PaymentReady",
                    EntityName = "PurchaseOrder",
                    EntityId = id,
                    EntityNumber = po.PONumber,
                    Message = $"PO {po.PONumber} approved — ready for payment."
                });
            }
            await _db.SaveChangesAsync();
        }

        return Ok(new { po.AdminApproval, po.AdminApprovalNote, po.AdminApprovalAt });
    }

    /// <summary>Payment submits POP/supplier invoice for an approved PO.</summary>
    [HttpPatch("{id:long}/submit-payment")]
    [Authorize(Roles = "Payment,Admin,SuperAdmin")]
    [Auditable("PurchaseOrder", "SubmitPayment")]
    public async Task<IActionResult> SubmitPayment(long id)
    {
        var po = await _db.Set<PurchaseOrder>().FindAsync(id);
        if (po == null) return NotFound();
        if (po.AdminApproval != "Approved")
            return BadRequest(new { message = "PO must be Admin-Approved before payment can be submitted." });

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);

        po.PaymentStatus = "Submitted";
        po.PaymentSubmittedAt = DateTime.UtcNow;
        po.PaymentSubmittedBy = userId > 0 ? userId : null;
        po.Status = "Payment Done";
        await _db.SaveChangesAsync();

        return Ok(new { po.PaymentStatus, po.PaymentSubmittedAt, po.Status });
    }

    /// <summary>Payment queue: POs approved by admin. Payment role sees these.</summary>
    [HttpGet("payment-queue")]
    [Authorize(Roles = "Payment,SuperAdmin")]
    public async Task<ActionResult<List<POResponse>>> GetPaymentQueue()
    {
        var pos = await _db.Set<PurchaseOrder>()
            .Where(p => p.AdminApproval == "Approved")
            .OrderByDescending(p => p.AdminApprovalAt)
            .Include(p => p.Supplier)
            .ToListAsync();

        var invoiceMap = await ResolveInvoiceNumbersAsync(pos.Where(p => p.InvoiceId.HasValue).Select(p => p.InvoiceId!.Value).Distinct().ToList());

        var list = pos.Select(p => new POResponse
        {
            Id = p.Id,
            PONumber = p.PONumber,
            TotalAmount = p.TotalAmount,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.Name ?? "",
            InvoiceId = p.InvoiceId,
            InvoiceNumber = p.InvoiceId.HasValue && invoiceMap.ContainsKey(p.InvoiceId.Value) ? invoiceMap[p.InvoiceId.Value] : null,
            AdminApproval = p.AdminApproval,
            AdminApprovalNote = p.AdminApprovalNote,
            AdminApprovalAt = p.AdminApprovalAt,
            PaymentStatus = p.PaymentStatus,
            PaymentSubmittedAt = p.PaymentSubmittedAt,
            PaymentApproval = p.PaymentApproval,
            PaymentApprovalNote = p.PaymentApprovalNote,
            PaymentApprovalAt = p.PaymentApprovalAt,
        }).ToList();

        return Ok(list);
    }

    /// <summary>Full enriched view: PO items with the entire RFQ → Quote → Invoice → PO trail.</summary>
    [HttpGet("{id:long}/enriched")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Payment")]
    public async Task<IActionResult> GetEnriched(long id)
    {
        var po = await _db.Set<PurchaseOrder>()
            .Include(p => p.Supplier)
            .Include(p => p.POItems).ThenInclude(i => i.PartNumber)
            .Include(p => p.POItems).ThenInclude(i => i.ProcumentRecord!).ThenInclude(pr => pr.Supplier)
            .Include(p => p.POItems).ThenInclude(i => i.ProcumentRecord!).ThenInclude(pr => pr.RFQItem).ThenInclude(ri => ri.RFQ).ThenInclude(r => r.Customer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();

        var invoiceItemIds = po.POItems.Where(i => i.InvoiceItemId.HasValue).Select(i => i.InvoiceItemId!.Value).Distinct().ToList();
        var invoiceItemMap = new Dictionary<long, (long? InvoiceId, string? InvoiceNumber, decimal? InvoiceUnitPrice, long? QuoteItemId)>();
        var quoteItemMap = new Dictionary<long, (long QuoteId, string QuoteNumber, decimal UnitPrice)>();

        if (invoiceItemIds.Count > 0)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            var ids = string.Join(",", invoiceItemIds);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT ii.Id, ii.InvoiceId, inv.InvoiceNumber, ii.UnitPrice, ii.QuoteItemId FROM InvoiceItems ii INNER JOIN Invoices inv ON ii.InvoiceId = inv.Id WHERE ii.Id IN ({ids})";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    invoiceItemMap[r.GetInt64(0)] = (r.GetInt64(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? (decimal?)null : r.GetDecimal(3), r.IsDBNull(4) ? (long?)null : r.GetInt64(4));
                }
            }

            var quoteItemIds = invoiceItemMap.Values.Where(v => v.QuoteItemId.HasValue).Select(v => v.QuoteItemId!.Value).Distinct().ToList();
            if (quoteItemIds.Count > 0)
            {
                var qids = string.Join(",", quoteItemIds);
                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = $"SELECT qi.Id, q.Id AS QuoteId, q.QuoteNumber, qi.UnitPrice FROM QuoteItems qi INNER JOIN Quotes q ON qi.QuoteId = q.Id WHERE qi.Id IN ({qids})";
                using var r2 = await cmd2.ExecuteReaderAsync();
                while (await r2.ReadAsync())
                {
                    quoteItemMap[r2.GetInt64(0)] = (r2.GetInt64(1), r2.IsDBNull(2) ? "" : r2.GetString(2), r2.GetDecimal(3));
                }
            }
        }

        var items = po.POItems.Select(i =>
        {
            var proc = i.ProcumentRecord;
            var rfqItem = proc?.RFQItem;
            var rfq = rfqItem?.RFQ;
            var invInfo = i.InvoiceItemId.HasValue && invoiceItemMap.ContainsKey(i.InvoiceItemId.Value) ? invoiceItemMap[i.InvoiceItemId.Value] : default;
            var qInfo = invInfo.QuoteItemId.HasValue && quoteItemMap.ContainsKey(invInfo.QuoteItemId.Value) ? quoteItemMap[invInfo.QuoteItemId.Value] : default;

            return new
            {
                poItemId = i.Id,
                partNumber = i.PartNumber?.Name,
                description = i.PartNumber?.Description,
                qty = i.Qty,
                condition = i.Condition,

                // RFQ
                rfqId = rfq?.Id,
                rfqNumber = rfq?.Name,
                customerName = rfq?.Customer?.Name,
                customerBase = rfq?.Customer?.Base,

                // Procurement
                procumentId = proc?.Id,
                procurementSupplier = proc?.Supplier?.Name,
                procurementBuyPrice = proc?.Price,
                procurementCondition = proc?.Condition,
                procurementAlt = proc?.Alt,
                procurementNote = proc?.Note,

                // Quote
                quoteId = qInfo.QuoteId == 0 ? (long?)null : qInfo.QuoteId,
                quoteNumber = qInfo.QuoteNumber,
                quoteUnitPrice = qInfo.QuoteId == 0 ? (decimal?)null : qInfo.UnitPrice,

                // Invoice
                invoiceId = invInfo.InvoiceId,
                invoiceNumber = invInfo.InvoiceNumber,
                invoiceUnitPrice = invInfo.InvoiceUnitPrice,

                // PO
                poUnitPrice = i.UnitPrice,
                poTotalPrice = i.TotalPrice,
                poSupplier = po.Supplier?.Name,
            };
        }).ToList();

        return Ok(new
        {
            id = po.Id,
            poNumber = po.PONumber,
            supplierName = po.Supplier?.Name,
            totalAmount = po.TotalAmount,
            createdAt = po.CreatedAt,
            adminApproval = po.AdminApproval,
            adminApprovalNote = po.AdminApprovalNote,
            paymentStatus = po.PaymentStatus,
            paymentApproval = po.PaymentApproval,
            paymentApprovalNote = po.PaymentApprovalNote,
            items,
        });
    }

    /// <summary>
    /// Payment User accepts or rejects the PO.
    /// If rejected, it returns to Admin for corrections.
    /// </summary>
    [HttpPatch("{id:long}/payment-approval")]
    [Authorize(Roles = "Payment,Admin,SuperAdmin")]
    [Auditable("PurchaseOrder", "PaymentApproval", CaptureBody = true)]
    public async Task<IActionResult> SetPaymentApproval(long id, [FromBody] UpdatePaymentApprovalRequest request)
    {
        if (request.Decision != "Accepted" && request.Decision != "Rejected" && request.Decision != "Pending")
            return BadRequest(new { message = "Decision must be Accepted, Rejected, or Pending." });

        var po = await _db.Set<PurchaseOrder>().FindAsync(id);
        if (po == null) return NotFound();

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);

        po.PaymentApproval = request.Decision;
        po.PaymentApprovalNote = request.Note;
        po.PaymentApprovalAt = DateTime.UtcNow;
        po.PaymentApprovalBy = userId > 0 ? userId : null;

        if (request.Decision == "Rejected")
        {
            // Reset AdminApproval and PaymentStatus so Admin/Expert can fix it
            po.AdminApproval = "Pending";
            po.PaymentStatus = "NotStarted";
            po.Status = "Waiting For Admin Approval";

            // Notify Admin/SuperAdmin/Expert
            var userIds = await _db.Set<User>()
                .Where(u => (u.Role == "Admin" || u.Role == "SuperAdmin" || u.Role == "Expert") && u.IsActive)
                .Select(u => u.Id).ToListAsync();

            foreach (var uid in userIds)
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = uid,
                    Type = "PaymentRejected",
                    EntityName = "PurchaseOrder",
                    EntityId = id,
                    EntityNumber = po.PONumber,
                    Message = $"PO {po.PONumber} rejected by Payment: {request.Note}"
                });
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { po.PaymentApproval, po.PaymentApprovalNote, po.PaymentApprovalAt, po.AdminApproval, po.PaymentStatus });
    }

    private async Task<Dictionary<long, string>> ResolveInvoiceNumbersAsync(List<long> invoiceIds)
    {
        var result = new Dictionary<long, string>();
        if (invoiceIds.Count == 0) return result;

        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, InvoiceNumber FROM Invoices WHERE Id IN ({string.Join(",", invoiceIds)})";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            if (!r.IsDBNull(1)) result[r.GetInt64(0)] = r.GetString(1);
        }
        return result;
    }
}
