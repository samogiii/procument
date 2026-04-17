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
[Authorize(Roles = "Admin,Expert")]
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

    /// <summary>Update purchase order status.</summary>
    [HttpPatch("{id:long}/status")]
    [Auditable("PurchaseOrder", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdatePOStatusRequest request)
    {
        if (await _lockGuard.IsPurchaseOrderLocked(id))
            return BadRequest(new { message = "This PO is locked because a Final Invoice has been created." });

        bool isAdmin = User.IsInRole("Admin");

        // Get PO info for notification
        var po = await _poService.GetByIdAsync(id);
        if (po == null) return NotFound();

        var success = await _poService.UpdateStatusAsync(id, request.Status, isAdmin, request.RejectionNote);
        if (!success) return BadRequest("Status change not allowed.");

        // Notify all non-admin users about key PO status changes
        if (request.Status == "Completed" || request.Status == "Cancelled")
        {
            var expertIds = await _db.Set<User>().Where(u => u.Role != "Admin" && u.IsActive).Select(u => u.Id).ToListAsync();
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
}
