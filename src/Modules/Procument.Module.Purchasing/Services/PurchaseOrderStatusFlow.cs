using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// The canonical PO lifecycle. Every transition is copied to the live PO lines and
/// their source PI lines, keeping PI, PO and Total P/N on the same per-part status.
/// </summary>
public static class PurchaseOrderStatusFlow
{
    public const string NotStarted = "Not Started";
    public const string WaitingForAdminApproval = "Waiting For Admin Approval";
    public const string Sourcing = "Sourcing";
    public const string WaitingForSupplierDocuments = "Waiting For Supplier Documents";
    public const string WaitingForPr = "Waiting For PR";
    public const string WaitingForPayment = "Waiting For Payment";
    public const string PrRejected = "PR Rejected";
    public const string PaymentDone = "Payment Done";
    public const string WaitingForShipment = "Waiting For Shipment";
    public const string ShipToWarehouse = "Ship to Warehouse";
    public const string WaitingForExpertShipmentApproval = "Waiting for Expert Approval Shipment";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Returned = "Returned";
    public const string EndUser = "EndUser";
    public const string InShop = "In Shop";
    public const string ReceivedInWarehouse = "Received in Warehouse";
    /// <summary>
    /// Set automatically after Completed, once the part's shipment reached the customer (SN# finished
    /// as "Delivered to Customer", or a PickUp). Never set by hand, so it is not in <see cref="AllowedStatuses"/>.
    /// </summary>
    public const string DeliveredToCustomer = "Delivered to Customer";

    /// <summary>PO states that accept no further manual transitions or returns.</summary>
    public static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        Completed, DeliveredToCustomer, Cancelled, Returned
    };

    /// <summary>Shipping-track statuses that no longer hold parts in a warehouse.</summary>
    private static readonly string[] FinishedTrackStatuses = [DeliveredToCustomer, "Rejected"];

    public static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        NotStarted, WaitingForAdminApproval, Sourcing, WaitingForSupplierDocuments, WaitingForPr,
        WaitingForPayment, PrRejected, PaymentDone, WaitingForShipment,
        ShipToWarehouse, WaitingForExpertShipmentApproval, Completed,
        Cancelled, Returned, EndUser, InShop, ReceivedInWarehouse, "Issue"
    };

    public static int? RemainingInShopDays(POItem item, DateTime? now = null)
    {
        if (!item.InShopLeadTimeDays.HasValue || !item.InShopStartedAt.HasValue) return null;
        var elapsed = Math.Max(0, ((now ?? DateTime.UtcNow).Date - item.InShopStartedAt.Value.Date).Days);
        return Math.Max(0, item.InShopLeadTimeDays.Value - elapsed);
    }

    /// <summary>
    /// The part note shown on the PO page and printed on the PO PDF: the PO line's own note once
    /// one was set (even "" = deliberately blank), otherwise the supplier quote note it came from.
    /// Needs ProcumentRecord loaded for the fallback.
    /// </summary>
    public static string? PartNote(POItem item) => item.Note ?? item.ProcumentRecord?.Note;

    public static string DisplayStatus(POItem item)
    {
        if (!string.Equals(item.Status, InShop, StringComparison.OrdinalIgnoreCase))
            return item.Status ?? NotStarted;
        var remaining = RemainingInShopDays(item);
        return remaining.HasValue ? $"In shop ({remaining.Value} days)" : InShop;
    }

    public static async Task ApplyAsync(DbContext db, PurchaseOrder po, string status, bool updatePo = true)
    {
        if (updatePo) po.Status = status;
        var items = await db.Set<POItem>()
            .Where(i => i.POId == po.Id && i.ReturnedAt == null)
            .ToListAsync();
        foreach (var item in items) item.Status = status;
        await SyncPiItemsAsync(db, items, status);
    }

    /// <summary>
    /// Promotes PO lines whose parts all reached the customer to <see cref="DeliveredToCustomer"/>,
    /// and a PO once every live line is delivered. A line counts as delivered when at least one of its
    /// tracks is "Delivered to Customer" and none of its other tracks still holds stock — tracks that
    /// were rejected or fully transferred to another warehouse (their child track carries on) don't count.
    /// Call after the track statuses are saved. Stock POs never go to a customer and are skipped.
    /// </summary>
    public static async Task PromoteDeliveredAsync(DbContext db, IEnumerable<long> poItemIds)
    {
        var ids = poItemIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var lines = await db.Set<POItem>()
            .Include(i => i.TrackNumbers).ThenInclude(t => t.Items)
            .Include(i => i.PurchaseOrder)
            .Where(i => ids.Contains(i.Id) && i.ReturnedAt == null && i.PurchaseOrder != null && i.PurchaseOrder.Origin != "Stock")
            .ToListAsync();

        static bool HoldsNoStock(POItemTrackNumber t) =>
            FinishedTrackStatuses.Contains(t.Status)
            || (t.Items.Count > 0 && t.Items.All(i => i.ActualQty.HasValue && i.ActualQty.Value <= i.TransferredOutQty));

        var delivered = lines
            .Where(l => l.Status != DeliveredToCustomer
                && l.TrackNumbers.Any(t => t.Status == DeliveredToCustomer)
                && l.TrackNumbers.All(HoldsNoStock))
            .ToList();
        if (delivered.Count == 0) return;

        foreach (var line in delivered) line.Status = DeliveredToCustomer;
        await SyncPiItemsAsync(db, delivered, DeliveredToCustomer);

        var poIds = delivered.Select(l => l.POId!.Value).Distinct().ToList();
        var pos = await db.Set<PurchaseOrder>()
            .Include(p => p.POItems.Where(i => i.ReturnedAt == null))
            .Where(p => poIds.Contains(p.Id) && p.Status != Cancelled && p.Status != Returned)
            .ToListAsync();
        foreach (var po in pos)
            if (po.POItems.Count > 0 && po.POItems.All(i => i.Status == DeliveredToCustomer || i.Status == Cancelled))
                po.Status = DeliveredToCustomer;

        await db.SaveChangesAsync();
    }

    public static async Task SyncPiItemsAsync(DbContext db, IEnumerable<POItem> items, string status)
    {
        var ids = items.Where(i => i.InvoiceItemId.HasValue).Select(i => i.InvoiceItemId!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        using var command = connection.CreateCommand();

        // This helper is also called while supplier POP/payment processing owns
        // an EF transaction. A raw ADO.NET command must explicitly enlist in
        // that transaction or SQL Server rejects ExecuteNonQueryAsync.
        if (db.Database.CurrentTransaction is { } currentTransaction)
            command.Transaction = currentTransaction.GetDbTransaction();

        var parameters = new List<string>();
        for (var index = 0; index < ids.Count; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@id{index}";
            parameter.Value = ids[index];
            command.Parameters.Add(parameter);
            parameters.Add(parameter.ParameterName);
        }
        var statusParameter = command.CreateParameter();
        statusParameter.ParameterName = "@status";
        statusParameter.Value = status;
        command.Parameters.Add(statusParameter);
        command.CommandText = $"UPDATE ProformaInvoiceItems SET Status = @status WHERE Id IN ({string.Join(",", parameters)})";
        await command.ExecuteNonQueryAsync();
    }
}
