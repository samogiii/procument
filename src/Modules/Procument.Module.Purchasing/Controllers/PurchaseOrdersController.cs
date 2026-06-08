using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Shared.Services;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;
using Procument.Shared.Services;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment,AHM")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _poService;
    private readonly DbContext _db;
    private readonly IFinalInvoiceLockGuard _lockGuard;
    private readonly IPaymentLedgerService _paymentLedgerService;
    private readonly INotificationService _notifications;

    public PurchaseOrdersController(IPurchaseOrderService poService, DbContext db, IFinalInvoiceLockGuard lockGuard, IPaymentLedgerService paymentLedgerService, INotificationService notifications)
    {
        _poService = poService;
        _db = db;
        _lockGuard = lockGuard;
        _paymentLedgerService = paymentLedgerService;
        _notifications = notifications;
    }

    /// <summary>Get all purchase orders (paginated). Non-admins see only POs assigned to them via EntityPermission("PO").</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<POResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 1000)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var result = await _poService.GetAllAsync(pq, userId, isAdmin, isSuperAdmin, userBases);
        return Ok(result);
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

    /// <summary>Get all unassigned POItems (not yet assigned to a PO).
    /// Admins see everything; non-admins see only POItems whose SourceProcurementItem
    /// they have an EntityPermission("Procurement") on.</summary>
    [HttpGet("unassigned-items")]
    public async Task<ActionResult<List<UnassignedPOItemResponse>>> GetUnassignedItems()
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var result = await _poService.GetUnassignedItemsAsync(userId, isAdmin, isSuperAdmin, userBases);
        return Ok(result);
    }

    /// <summary>Get a purchase order by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<POResponse>> GetById(long id)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        if (!await _poService.UserCanAccessAsync(id, userId, isAdmin, isSuperAdmin, userBases)) return Forbid();
        var result = await _poService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new purchase order.</summary>
    /// TODO fix service later
    [HttpPost]
    [Auditable("PurchaseOrder", "Create", CaptureBody = true)]
    public async Task<ActionResult<POResponse>> Create([FromBody] CreatePORequest request)
    {
        var (userId, isAdmin, _, _) = GetCurrentUser();

        // Non-admins may only create POs from items that trace back to a ProcurementItem
        // they were assigned to. Admins bypass this check.
        if (!isAdmin && request.POItemIds?.Count > 0)
        {
            var requestedIds = request.POItemIds.Distinct().ToList();
            var sourceProcItemIds = await _db.Set<POItem>()
                .Where(i => requestedIds.Contains(i.Id))
                .Select(i => new { i.Id, i.SourceProcurementItemId })
                .ToListAsync();

            if (sourceProcItemIds.Count != requestedIds.Count)
                return BadRequest(new { message = "One or more POItemIds were not found." });

            if (sourceProcItemIds.Any(x => !x.SourceProcurementItemId.HasValue))
                return Forbid();

            var permittedIdStrings = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Procurement")
                .Select(p => p.EntityId)
                .ToListAsync();
            var permittedProcItemIds = permittedIdStrings
                .Select(s => long.TryParse(s, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToHashSet();

            if (sourceProcItemIds.Any(x => !permittedProcItemIds.Contains(x.SourceProcurementItemId!.Value)))
                return Forbid();
        }

        var result = await _poService.CreateAsync(request);

        // If a non-admin created the PO (they had Procurement-level access), grant them
        // an EntityPermission on the new PO so it shows up in their /purchase-orders list.
        if (!isAdmin && userId > 0)
        {
            try
            {
                var already = await _db.Set<EntityPermission>()
                    .AnyAsync(p => p.UserId == userId && p.EntityName == "PO" && p.EntityId == result.Id.ToString());
                if (!already)
                {
                    _db.Set<EntityPermission>().Add(new EntityPermission
                    {
                        UserId = userId,
                        EntityName = "PO",
                        EntityId = result.Id.ToString(),
                        Permission = "Edit",
                        CreatedAt = DateTime.UtcNow,
                    });
                    await _db.SaveChangesAsync();
                }
            }
            catch { /* non-fatal — admin can assign later */ }
        }

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

    /// <summary>
    /// Return this PO (or a subset of its items) back into the Procurement layer so a new
    /// supplier can be sourced. Empty/missing ItemIds = full return (PO flips to "Returned").
    /// Admin / SuperAdmin only.
    /// </summary>
    [HttpPost("{id:long}/return")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Auditable("PurchaseOrder", "Return", CaptureBody = true)]
    public async Task<ActionResult<ReturnPOResponse>> ReturnPO(long id, [FromBody] ReturnPORequest request)
    {
        if (await _lockGuard.IsPurchaseOrderLocked(id))
            return BadRequest(new { message = "This PO is locked because a Final Invoice has been created." });

        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "A return reason is required." });

        var (userId, _, _, _) = GetCurrentUser();
        var result = await _poService.ReturnAsync(id, request, userId);
        if (result == null)
            return BadRequest(new { message = "PO cannot be returned (not found, already terminal, or no live items selected)." });

        // Fire notification so assigned PO users see the PO has moved back to Procurement
        try
        {
            var po = await _poService.GetByIdAsync(id);
            if (po != null)
            {
                var assignedUserIds = await _db.Set<EntityPermission>()
                    .Where(p => p.EntityName == "PO" && p.EntityId == id.ToString())
                    .Select(p => p.UserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var uid in assignedUserIds)
                {
                    _db.Set<Notification>().Add(new Notification
                    {
                        UserId = uid,
                        Type = "POReturned",
                        EntityName = "PurchaseOrder",
                        EntityId = id,
                        EntityNumber = po.PONumber,
                        Message = result.FullReturn
                            ? $"PO {po.PONumber} was fully returned to Procurement."
                            : $"{result.ReturnedPOItemIds.Count} item(s) on PO {po.PONumber} were returned to Procurement.",
                    });
                }
                if (assignedUserIds.Count > 0) await _db.SaveChangesAsync();
            }
        }
        catch { /* notifications are best-effort */ }

        return Ok(result);
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

    /// <summary>SuperAdmin: override the unit price of a POItem. Recalculates TotalPrice and PO.TotalAmount. Reflected immediately in Total P/N.</summary>
    [HttpPatch("items/{id:long}/price")]
    [Authorize(Roles = "SuperAdmin")]
    [Auditable("POItem", "PriceOverride", CaptureBody = true)]
    public async Task<IActionResult> UpdateItemPrice(long id, [FromBody] UpdatePOItemPriceRequest request)
    {
        var success = await _poService.UpdateItemPriceAsync(id, request.UnitPrice);
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
        var (uid, isA, isSA, bases) = GetCurrentUser();
        if (!await _poService.UserCanAccessAsync(id, uid, isA, isSA, bases)) return Forbid();
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

        string deliverToName = "";
        string deliverToAddress = "";
        string deliverToPhone = "";
        string deliverToEmail = "";
        string fedexAccount = "";
        if (exType.HasValue && exType.Value != 0 && customer != null)
        {
            // Customer Exwork — deliver to customer address
            deliverToName = customer.Name;
            deliverToAddress = customer.ShipTo ?? customer.BillTo ?? "";
            deliverToPhone = customer.Phone ?? "";
            deliverToEmail = customer.Email ?? "";
            fedexAccount = customer.ShippingAccount ?? "";
        }
        else
        {
            // Warehouse — deliver to Hong Kong office (Base 105)
            var base105 = await _db.Set<CompanyPreset>().FirstOrDefaultAsync(p => p.SortOrder == 105 && p.IsActive);
            if (base105 != null)
            {
                deliverToName = base105.Name;
                deliverToAddress = base105.ShipToAddress ?? base105.Location ?? "";
                fedexAccount = base105.FedexAccount ?? "";
                deliverToPhone = base105.ShipToPhone ?? base105.Phone ?? "";
                deliverToEmail = base105.Email ?? "";
            }
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
                fedexAccount = fedexAccount,
            },
            importDetail = po.ImportDetail != null ? new
            {
                fedExAccount = po.ImportDetail.FedExAccount ?? "",
                servicePriority = po.ImportDetail.CourierName ?? "",
                shippingMethod = po.ImportDetail.ShippingMethod ?? "",
                incoterms = po.ImportDetail.Incoterms ?? "",
                comments = po.ImportDetail.Notes ?? "",
            } : null,
            // ─── PO-level totals adjustments — used as defaults in the PDF generator ───
            processingFee = po.ProcessingFee,
            shipping = po.Shipping,
            tax = po.Tax,
            items,
        });
    }

    /// <summary>
    /// Update PO-level totals adjustments (Processing Fee, Shipping, Tax).
    /// These are flat decimal amounts shown in the PDF totals block — independent of
    /// per-item ProcumentRecord.ShippingCost. The user types them on the PO page (or
    /// in the PDF generator dialog), and they persist on the PurchaseOrder row.
    /// </summary>
    [HttpPatch("{id:long}/totals")]
    [Auditable("PurchaseOrder", "UpdateTotals", CaptureBody = true)]
    public async Task<IActionResult> UpdateTotals(long id, [FromBody] UpdatePOTotalsRequest request)
    {
        var (uid, isA, isSA, bases) = GetCurrentUser();
        if (!await _poService.UserCanAccessAsync(id, uid, isA, isSA, bases)) return Forbid();
        if (await _lockGuard.IsPurchaseOrderLocked(id))
            return BadRequest(new { message = "This PO is locked because a Final Invoice has been created." });

        var po = await _db.Set<PurchaseOrder>().FindAsync(id);
        if (po == null) return NotFound();

        po.ProcessingFee = request.ProcessingFee;
        po.Shipping = request.Shipping;
        po.Tax = request.Tax;
        po.PODate = request.PODate;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            processingFee = po.ProcessingFee,
            shipping = po.Shipping,
            tax = po.Tax,
        });
    }

    // ═══════════════════════════════════════════
    // Import Details
    // ═══════════════════════════════════════════

    /// <summary>Get import details for a PO.</summary>
    [HttpGet("{poId:long}/import-detail")]
    public async Task<IActionResult> GetImportDetail(long poId)
    {
        var (uid, isA, isSA, bases) = GetCurrentUser();
        if (!await _poService.UserCanAccessAsync(poId, uid, isA, isSA, bases)) return Forbid();
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
            SwiftCode = detail.SwiftCode,
            ABA = detail.ABA,
            Wirefee = detail.Wirefee,
        });
    }

    /// <summary>Create or update import details for a PO.</summary>
    [HttpPut("{poId:long}/import-detail")]
    public async Task<IActionResult> SaveImportDetail(long poId, [FromBody] SavePOImportDetailRequest request)
    {
        var (uid, isA, isSA, bases) = GetCurrentUser();
        if (!await _poService.UserCanAccessAsync(poId, uid, isA, isSA, bases)) return Forbid();
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
        detail.SwiftCode = request.SwiftCode;
        detail.ABA = request.ABA;
        detail.Wirefee = request.Wirefee;

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
            SwiftCode = detail.SwiftCode,
            ABA = detail.ABA,
            Wirefee = detail.Wirefee,
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
            .Include(t => t.Warehouse)
            .Where(t => t.POItemId == poItemId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TrackNumberResponse
            {
                Id = t.Id,
                POItemId = t.POItemId,
                TrackNumber = t.TrackNumber,
                Carrier = t.Carrier,
                Notes = t.Notes,
                WarehouseId = t.WarehouseId,
                WarehouseName = t.Warehouse != null ? t.Warehouse.Name : null,
                Status = t.Status,
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
            WarehouseId = request.WarehouseId,
            Status = "Ship to Warehouse",
            CreatedAt = DateTime.UtcNow,
        };

        _db.Set<POItemTrackNumber>().Add(track);
        await _db.SaveChangesAsync();

        // Notify Inventory users assigned to this warehouse
        if (request.WarehouseId.HasValue)
        {
            var inventoryUserIds = await _db.Set<UserWarehouse>()
                .Where(uw => uw.WarehouseId == request.WarehouseId.Value)
                .Select(uw => uw.UserId)
                .ToListAsync();

            if (inventoryUserIds.Count > 0)
            {
                var adderIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                long.TryParse(adderIdStr, out var adderId);
                var adder = adderId > 0 ? await _db.Set<User>().FindAsync(adderId) : null;
                var poItem = await _db.Set<POItem>().Include(i => i.PartNumber).FirstOrDefaultAsync(i => i.Id == poItemId);
                var warehouse = await _db.Set<Warehouse>().FindAsync(request.WarehouseId.Value);
                await _notifications.CreateForUsersAsync(
                    inventoryUserIds, "TrackAdded", "TrackNumber", track.Id, track.TrackNumber,
                    $"New track {track.TrackNumber} added for {poItem?.PartNumber?.Name ?? "a part"} → {warehouse?.Name ?? "your warehouse"}",
                    adderId > 0 ? adderId : null, adder?.Name);
            }
        }

        return Ok(new TrackNumberResponse
        {
            Id = track.Id,
            POItemId = track.POItemId,
            TrackNumber = track.TrackNumber,
            Carrier = track.Carrier,
            Notes = track.Notes,
            WarehouseId = track.WarehouseId,
            Status = track.Status,
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

    /// <summary>Admin: flat list of all track numbers across all POs with their inventory items.</summary>
    [HttpGet("track-numbers/summary")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<TrackNumberSummaryResponse>>> GetTrackNumberSummary(
        [FromQuery] long? warehouseId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _db.Set<POItemTrackNumber>()
            .AsNoTracking()
            .Include(t => t.Warehouse)
            .Include(t => t.POItem).ThenInclude(i => i.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .Include(t => t.POItem).ThenInclude(i => i.PartNumber)
            .Include(t => t.POItem).ThenInclude(i => i.ProcumentRecord).ThenInclude(pr => pr!.RFQItem).ThenInclude(ri => ri.RFQ).ThenInclude(rfq => rfq.Customer)
            .Include(t => t.Items).ThenInclude(i => i.ReviewedBy)
            .Include(t => t.Documents).ThenInclude(d => d.UploadedBy)
            .Include(t => t.Documents).ThenInclude(d => d.POItem).ThenInclude(p => p!.PartNumber)
            .Include(t => t.Boxes)
            .AsQueryable();

        if (warehouseId.HasValue) query = query.Where(t => t.WarehouseId == warehouseId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t => t.TrackNumber.Contains(s) || (t.POItem.PurchaseOrder != null && t.POItem.PurchaseOrder.PONumber.Contains(s)));
        }

        var total = await query.CountAsync();
        var tracks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        // Load SN boxes for all returned track numbers.
        // ShipmentNoteBox.TrackNumberId is often null (not set by frontend),
        // so we resolve via ShipmentNoteTrackNumber join instead.
        var trackIds = tracks.Select(t => t.Id).ToList();

        var snLinks = await _db.Set<ShipmentNoteTrackNumber>()
            .AsNoTracking()
            .Where(snt => trackIds.Contains(snt.TrackNumberId))
            .ToListAsync();

        var trackToSnIds = snLinks
            .GroupBy(snt => snt.TrackNumberId)
            .ToDictionary(g => g.Key, g => g.Select(snt => snt.ShipmentNoteId).ToList());

        var allLinkedSnIds = snLinks.Select(snt => snt.ShipmentNoteId).Distinct().ToList();

        var snBoxes = allLinkedSnIds.Count > 0
            ? await _db.Set<ShipmentNoteBox>()
                .AsNoTracking()
                .Where(b => allLinkedSnIds.Contains(b.ShipmentNoteId))
                .Include(b => b.ShipmentNote)
                .ToListAsync()
            : new List<ShipmentNoteBox>();

        var snBoxesBySnId = snBoxes
            .GroupBy(b => b.ShipmentNoteId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = tracks.Select(t => new TrackNumberSummaryResponse
        {
            Id = t.Id,
            TrackNumber = t.TrackNumber,
            Carrier = t.Carrier,
            Notes = t.Notes,
            Status = t.Status,
            WarehouseId = t.WarehouseId,
            WarehouseName = t.Warehouse?.Name,
            WarehouseAddress = t.Warehouse?.Address,
            POItemId = t.POItemId,
            POId = t.POItem?.POId,
            PONumber = t.POItem?.PurchaseOrder?.PONumber,
            PartNumberName = t.POItem?.PartNumber?.Name,
            Description = t.POItem?.PartNumber?.Description,
            SupplierName = t.POItem?.PurchaseOrder?.Supplier?.Name,
            Qty = t.POItem?.Qty ?? 0,
            Condition = t.POItem?.Condition,
            CustomerName = t.POItem?.ProcumentRecord?.RFQItem?.RFQ?.Customer?.Name,
            CustomerCode = t.POItem?.ProcumentRecord?.RFQItem?.RFQ?.Customer?.CustomerCode,
            CreatedAt = t.CreatedAt,
            Items = t.Items.Select(i => new TrackSummaryItem
            {
                Id = i.Id,
                POItemId = i.POItemId,
                PartNumberName = t.POItem?.PartNumber?.Name,
                Description = t.POItem?.PartNumber?.Description,
                SupplierName = t.POItem?.PurchaseOrder?.Supplier?.Name,
                Qty = t.POItem?.Qty ?? 0,
                Condition = t.POItem?.Condition,
                CustomerName = t.POItem?.ProcumentRecord?.RFQItem?.RFQ?.Customer?.Name,
                CustomerCode = t.POItem?.ProcumentRecord?.RFQItem?.RFQ?.Customer?.CustomerCode,
                ExpectedQty = i.ExpectedQty,
                ActualQty = i.ActualQty,
                IsAvailable = i.IsAvailable,
                Status = i.Status,
                ReviewedByName = i.ReviewedBy?.Name,
                ReviewedAt = i.ReviewedAt,
                ReviewNote = i.ReviewNote,
            }).ToList(),
            Documents = t.Documents.Select(d => new TrackSummaryDocument
            {
                Id = d.Id,
                POItemId = d.POItemId,
                PartNumberName = d.POItem?.PartNumber?.Name,
                OriginalFileName = d.OriginalFileName,
                MimeType = d.MimeType,
                FileSizeBytes = d.FileSizeBytes,
                UploadedAt = d.UploadedAt,
                UploadedByName = d.UploadedBy?.Name,
            }).ToList(),
            ReceivedBoxes = t.Boxes.OrderBy(b => b.BoxNumber).Select(b => new TrackSummaryBox
            {
                Id = b.Id,
                BoxNumber = b.BoxNumber,
                WeightKg = b.WeightKg,
                HeightCm = b.HeightCm,
                WidthCm = b.WidthCm,
                LengthCm = b.LengthCm,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt,
            }).ToList(),
            SnBoxes = trackToSnIds.TryGetValue(t.Id, out var linkedSnIds)
                ? linkedSnIds
                    .SelectMany(snId => snBoxesBySnId.TryGetValue(snId, out var boxes) ? boxes : new())
                    .OrderBy(b => b.BoxNumber)
                    .Select(b => new TrackSummarySnBox
                    {
                        Id = b.Id,
                        ShipmentNoteId = b.ShipmentNoteId,
                        SNNumber = b.ShipmentNote?.SNNumber,
                        BoxNumber = b.BoxNumber,
                        WeightKg = b.WeightKg,
                        HeightCm = b.HeightCm,
                        WidthCm = b.WidthCm,
                        LengthCm = b.LengthCm,
                        Notes = b.Notes,
                        CreatedAt = b.CreatedAt,
                    }).ToList()
                : new(),
        }).ToList();

        return Ok(new { items = result, totalCount = total });
    }

    // ─────────────────────────────────────────────────────────────
    // Admin Approval + Payment Workflow
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Approve or reject a PO. When a PO is created it is locked for regular users —
    /// Admin and SuperAdmin can change the AdminApproval state.
    /// </summary>
    [HttpPatch("{id:long}/admin-approval")]
    [Authorize(Roles = "Admin,SuperAdmin")]
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
    [Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
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

    /// <summary>Payment queue: POs approved by admin. Payment and Admin roles see these.</summary>
    [HttpGet("payment-queue")]
    [Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
    public async Task<ActionResult<List<POResponse>>> GetPaymentQueue()
    {
        var pos = await _db.Set<PurchaseOrder>()
            .Where(p => p.AdminApproval == "Approved")
            .OrderByDescending(p => p.AdminApprovalAt)
            .Include(p => p.Supplier)
            .ToListAsync();

        var invoiceIds = pos.Where(p => p.InvoiceId.HasValue).Select(p => p.InvoiceId!.Value).Distinct().ToList();
        var invoiceMap = await ResolveInvoiceNumbersAsync(invoiceIds);

        // Resolve CustomerId from Invoice via raw SQL (Invoice entity is in Sales module)
        var customerMap = new Dictionary<long, long>();
        if (invoiceIds.Count > 0)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            var ids = string.Join(",", invoiceIds);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT Id, CustomerId FROM Invoices WHERE Id IN ({ids})";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                customerMap[reader.GetInt64(0)] = reader.GetInt64(1);
        }

        // Resolve preferred wallet names
        var walletIds = pos.Where(p => p.PreferredWalletId.HasValue).Select(p => p.PreferredWalletId!.Value).Distinct().ToList();
        var walletMap = new Dictionary<long, (string Name, string Company)>();
        if (walletIds.Count > 0)
        {
            var conn2 = _db.Database.GetDbConnection();
            if (conn2.State != System.Data.ConnectionState.Open) await conn2.OpenAsync();
            var wids = string.Join(",", walletIds);
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = $"SELECT pb.Id, COALESCE(NULLIF(pb.Name,''), cp.Name), cp.Name FROM PaymentBoxes pb JOIN CompanyPresets cp ON cp.Id = pb.CompanyPresetId WHERE pb.Id IN ({wids})";
            using var rdr2 = await cmd2.ExecuteReaderAsync();
            while (await rdr2.ReadAsync())
                walletMap[rdr2.GetInt64(0)] = (rdr2.GetString(1), rdr2.GetString(2));
        }

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
            CustomerId = p.InvoiceId.HasValue && customerMap.ContainsKey(p.InvoiceId.Value) ? customerMap[p.InvoiceId.Value] : null,
            AdminApproval = p.AdminApproval,
            AdminApprovalNote = p.AdminApprovalNote,
            AdminApprovalAt = p.AdminApprovalAt,
            PaymentStatus = p.PaymentStatus,
            PaymentSubmittedAt = p.PaymentSubmittedAt,
            PaymentApproval = p.PaymentApproval,
            PaymentApprovalNote = p.PaymentApprovalNote,
            PaymentApprovalAt = p.PaymentApprovalAt,
            PreferredWalletId = p.PreferredWalletId,
            PreferredWalletName = p.PreferredWalletId.HasValue && walletMap.ContainsKey(p.PreferredWalletId.Value) ? walletMap[p.PreferredWalletId.Value].Name : null,
            PreferredWalletCompany = p.PreferredWalletId.HasValue && walletMap.ContainsKey(p.PreferredWalletId.Value) ? walletMap[p.PreferredWalletId.Value].Company : null,
        }).ToList();

        return Ok(list);
    }

    /// <summary>Full enriched view: PO items with the entire RFQ → Quote → Invoice → PO trail.</summary>
    [HttpGet("{id:long}/enriched")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert,Payment,AHM")]
    public async Task<IActionResult> GetEnriched(long id)
    {
        var (uid, isA, isSA, bases) = GetCurrentUser();
        if (!await _poService.UserCanAccessAsync(id, uid, isA, isSA, bases)) return Forbid();
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
                customerCode = rfq?.Customer?.CustomerCode,
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
    [Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
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

    /// <summary>
    /// Called by the Withdraw Panel user when they upload a POP — records which wallet was debited.
    /// The wallet transaction is created here instead of at acceptance time.
    /// </summary>
    [HttpPost("{id:long}/record-pop-withdrawal")]
    [Authorize(Roles = "Payment,AHM,Admin,SuperAdmin")]
    public async Task<IActionResult> RecordPopWithdrawal(long id, [FromBody] PopWithdrawRequest request)
    {
        var po = await _db.Set<PurchaseOrder>().FindAsync(id);
        if (po == null) return NotFound();

        if (po.PaymentApproval != "Accepted")
            return BadRequest(new { message = "Payment has not been accepted yet." });

        var pr = await _db.Set<PaymentRequest>().FirstOrDefaultAsync(r => r.POId == po.Id);

        // Prevent duplicate withdrawal transactions via raw SQL check (avoids cross-module entity dependency)
        if (pr != null)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM PaymentTransactions WHERE PaymentRequestId = @prId AND Type = 'Withdraw'";
            var p = cmd.CreateParameter(); p.ParameterName = "@prId"; p.Value = pr.Id; cmd.Parameters.Add(p);
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0L);
            if (count > 0)
                return Conflict(new { message = "A withdrawal has already been recorded for this payment." });
        }

        await _paymentLedgerService.TryAutoWithdrawAsync(
            po.SupplierId,
            po.TotalAmount ?? 0,
            pr?.CompanyPresetId,
            pr?.Id,
            request.WalletId);

        return Ok(new { message = "Withdrawal recorded." });
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
