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

    public static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        NotStarted, Sourcing, WaitingForSupplierDocuments, WaitingForPr,
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
