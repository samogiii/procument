using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Procument.Module.Catalog.Entities;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Entities;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.OurInventory.Services;

public interface IStockManagementService
{
    Task<OpeningStockResult> ImportOpeningAsync(OpeningStockRequest request, long userId);
    Task AdjustAsync(long stockItemId, StockAdjustRequest request, long userId);
    Task TransferAsync(long stockItemId, StockTransferRequest request, long userId);
    Task<bool> UpdateAsync(long stockItemId, UpdateStockItemRequest request);
    Task IssueReservationAsync(long reservationId, IssueReservationRequest request, long userId);
    Task ReleaseReservationAsync(long reservationId, long userId);
}

/// <summary>Manual stock operations (opening balance, adjust, transfer, lot details). Quantities go through the ledger.</summary>
public sealed class StockManagementService(DbContext db, IStockLedgerService ledger) : IStockManagementService
{
    private const string DefaultCondition = "NE";

    /// <summary>
    /// Validates every row first; nothing is saved when any row is invalid (or on a dry run).
    /// Unknown part numbers are added to the catalog, as Stock POs do.
    /// </summary>
    public async Task<OpeningStockResult> ImportOpeningAsync(OpeningStockRequest request, long userId)
    {
        var result = new OpeningStockResult { Lines = request.Lines.Count };
        if (request.Lines.Count == 0)
        {
            result.Errors.Add(new OpeningStockError(0, "The file has no rows."));
            return result;
        }

        var warehouses = await db.Set<Warehouse>().AsNoTracking().Where(w => w.IsActive)
            .Select(w => new { w.Id, w.Name, w.DisplayName }).ToListAsync();
        var presets = await db.Set<CompanyPreset>().AsNoTracking().Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Name }).ToListAsync();
        var partNames = request.Lines.Where(l => !l.PartNumberId.HasValue && !string.IsNullOrWhiteSpace(l.PartNumber))
            .Select(l => l.PartNumber!.Trim().ToUpperInvariant()).Distinct().ToList();
        var knownParts = await db.Set<PartNumber>().AsNoTracking()
            .Where(p => partNames.Contains(p.Name.ToUpper()))
            .Select(p => new { p.Id, p.Name }).ToListAsync();
        var partIds = request.Lines.Where(l => l.PartNumberId.HasValue).Select(l => l.PartNumberId!.Value).Distinct().ToList();
        var existingIds = await db.Set<PartNumber>().AsNoTracking().Where(p => partIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();

        var resolved = new List<(int Row, OpeningStockLine Line, long? PartId, long WarehouseId, long PresetId, List<string> Serials)>();
        var serialsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var row = i + 1;
            var line = request.Lines[i];
            void Err(string m) => result.Errors.Add(new OpeningStockError(row, m));

            long? partId = null;
            if (line.PartNumberId.HasValue)
            {
                if (!existingIds.Contains(line.PartNumberId.Value)) { Err($"Part number id {line.PartNumberId} was not found."); continue; }
                partId = line.PartNumberId;
            }
            else if (string.IsNullOrWhiteSpace(line.PartNumber)) { Err("Part number is missing."); continue; }
            else partId = knownParts.FirstOrDefault(p => p.Name.Equals(line.PartNumber.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;

            var warehouse = line.WarehouseId.HasValue
                ? warehouses.FirstOrDefault(w => w.Id == line.WarehouseId.Value)
                : warehouses.FirstOrDefault(w => Matches(line.Warehouse, w.Name) || Matches(line.Warehouse, w.DisplayName));
            if (warehouse is null) { Err($"Warehouse \"{line.Warehouse ?? line.WarehouseId?.ToString()}\" was not found or is inactive."); continue; }

            var preset = line.CompanyPresetId.HasValue
                ? presets.FirstOrDefault(p => p.Id == line.CompanyPresetId.Value)
                : presets.FirstOrDefault(p => Matches(line.CompanyPreset, p.Name));
            if (preset is null) { Err($"Company \"{line.CompanyPreset ?? line.CompanyPresetId?.ToString()}\" was not found or is inactive."); continue; }

            if (line.Qty <= 0 || decimal.Round(line.Qty, 2) != line.Qty) { Err("Quantity must be above 0 with at most two decimals."); continue; }
            if (line.UnitCost < 0) { Err("Unit cost cannot be negative."); continue; }
            if (line.MinQty is < 0) { Err("Minimum quantity cannot be negative."); continue; }

            var serials = (line.Serials ?? []).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (serials.Count > 0 && serials.Count != line.Qty) { Err($"{serials.Count} serial(s) for a quantity of {line.Qty:0.##}."); continue; }
            var dup = serials.FirstOrDefault(s => !serialsSeen.Add(s));
            if (dup is not null) { Err($"Serial {dup} appears more than once in the file."); continue; }

            resolved.Add((row, line, partId, warehouse.Id, preset.Id, serials));
        }

        if (result.Errors.Count > 0 || request.DryRun)
        {
            result.Units = resolved.Sum(r => r.Line.Qty);
            result.NewPartNumbers = resolved.Where(r => r.PartId is null).Select(r => r.Line.PartNumber!.Trim().ToUpperInvariant()).Distinct().Count();
            return result;
        }

        var reference = string.IsNullOrWhiteSpace(request.Reference) ? $"Opening stock {DateTime.UtcNow:yyyy-MM-dd}" : request.Reference.Trim();
        var lots = new HashSet<long>();
        var created = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await ledger.ExecuteAsync(async () =>
        {
            lots.Clear();
            created.Clear();
            foreach (var r in resolved)
            {
                var partId = r.PartId;
                if (partId is null)
                {
                    var name = r.Line.PartNumber!.Trim();
                    if (!created.TryGetValue(name, out var newId))
                    {
                        var part = new PartNumber { Name = name, Description = r.Line.Description, CreatedAt = DateTime.UtcNow };
                        db.Set<PartNumber>().Add(part);
                        await db.SaveChangesAsync();
                        created[name] = newId = part.Id;
                    }
                    partId = newId;
                }

                var lot = await ledger.GetOrCreateLotAsync(new StockLotKey(partId.Value,
                    string.IsNullOrWhiteSpace(r.Line.Condition) ? DefaultCondition : r.Line.Condition.Trim().ToUpperInvariant(),
                    r.WarehouseId, r.PresetId, r.Line.CertName));
                if (!string.IsNullOrWhiteSpace(r.Line.BinLocation)) lot.BinLocation = r.Line.BinLocation.Trim();
                if (r.Line.TagDate.HasValue) lot.TagDate = r.Line.TagDate.Value.Date;
                if (r.Line.MinQty.HasValue) lot.MinQty = r.Line.MinQty;
                await ledger.ReceiveAsync(lot, r.Line.Qty, r.Line.UnitCost, OurStockMovementTypes.Opening,
                    new StockMovementContext { UserId = userId, Reference = reference, Reason = $"Import row {r.Row}" },
                    r.Serials.Count > 0 ? r.Serials : null);
                lots.Add(lot.Id);
            }
            return true;
        });

        result.Saved = true;
        result.Units = resolved.Sum(r => r.Line.Qty);
        result.LotsTouched = lots.Count;
        result.NewPartNumbers = created.Count;
        return result;
    }

    public Task AdjustAsync(long stockItemId, StockAdjustRequest request, long userId)
        => ledger.ExecuteAsync(async () =>
        {
            await EnsureLotAsync(stockItemId);
            await ledger.AdjustAsync(stockItemId, request.Qty, new StockMovementContext
            {
                UserId = userId, Reference = $"ADJ-{DateTime.UtcNow:yyyyMMdd}", Reason = request.Reason,
            });
            return true;
        });

    public Task TransferAsync(long stockItemId, StockTransferRequest request, long userId)
        => ledger.ExecuteAsync(async () =>
        {
            await EnsureLotAsync(stockItemId);
            if (!await db.Set<Warehouse>().AnyAsync(w => w.Id == request.TargetWarehouseId && w.IsActive))
                throw new InvalidOperationException("The target warehouse was not found or is inactive.");
            await ledger.TransferAsync(stockItemId, request.TargetWarehouseId, request.Qty, new StockMovementContext
            {
                UserId = userId, Reference = $"TRF-{DateTime.UtcNow:yyyyMMdd}",
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual transfer" : request.Reason,
            });
            return true;
        });

    public async Task<bool> UpdateAsync(long stockItemId, UpdateStockItemRequest request)
    {
        var lot = await db.Set<OurStockItem>().FirstOrDefaultAsync(i => i.Id == stockItemId);
        if (lot is null) return false;
        if (request.MinQty is < 0) throw new InvalidOperationException("Minimum quantity cannot be negative.");
        lot.BinLocation = Clean(request.BinLocation, 50);
        lot.MinQty = request.MinQty;
        lot.TagDate = request.TagDate?.Date;
        lot.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        lot.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Ships reserved stock to the customer: an Issue movement at the lot's average cost (the line's cost of goods).
    /// When the Sales Order line holds nothing more, its status becomes "Delivered to Customer".
    /// </summary>
    public Task IssueReservationAsync(long reservationId, IssueReservationRequest request, long userId)
        => ledger.ExecuteAsync(async () =>
        {
            var reservation = await db.Set<OurStockReservation>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId)
                ?? throw new KeyNotFoundException($"Reservation {reservationId} was not found.");
            var qty = request.Qty ?? reservation.Qty;
            await ledger.ConsumeAsync(reservationId, qty, new StockMovementContext
            {
                UserId = userId,
                Reference = reservation.InvoiceItemId.HasValue ? $"Sales order line {reservation.InvoiceItemId}" : $"Reservation {reservationId}",
                Reason = string.IsNullOrWhiteSpace(request.Note) ? "Shipped to customer" : request.Note.Trim(),
            });
            if (reservation.InvoiceItemId.HasValue
                && !await db.Set<OurStockReservation>().AnyAsync(r => r.InvoiceItemId == reservation.InvoiceItemId && r.Status == OurStockReservationStatuses.Active))
                await SetSalesLineStatusAsync(reservation.InvoiceItemId.Value, "Delivered to Customer");
            return true;
        });

    public Task ReleaseReservationAsync(long reservationId, long userId)
        => ledger.ExecuteAsync(async () =>
        {
            if (!await db.Set<OurStockReservation>().AnyAsync(r => r.Id == reservationId))
                throw new KeyNotFoundException($"Reservation {reservationId} was not found.");
            await ledger.ReleaseAsync(reservationId, OurStockReservationStatuses.Released);
            return true;
        });

    /// <summary>Sales Order lines live in the Sales module, which this module cannot reference; update them with SQL.</summary>
    private async Task SetSalesLineStatusAsync(long invoiceItemId, string status)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        if (db.Database.CurrentTransaction is { } tx) command.Transaction = tx.GetDbTransaction();
        command.CommandText = "UPDATE ProformaInvoiceItems SET Status = @status WHERE Id = @id";
        var s = command.CreateParameter(); s.ParameterName = "@status"; s.Value = status; command.Parameters.Add(s);
        var i = command.CreateParameter(); i.ParameterName = "@id"; i.Value = invoiceItemId; command.Parameters.Add(i);
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureLotAsync(long stockItemId)
    {
        if (!await db.Set<OurStockItem>().AnyAsync(i => i.Id == stockItemId))
            throw new KeyNotFoundException($"Stock item {stockItemId} was not found.");
    }

    private static bool Matches(string? input, string? value)
        => !string.IsNullOrWhiteSpace(input) && !string.IsNullOrWhiteSpace(value)
           && string.Equals(input.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }
}
