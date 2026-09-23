using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Procument.Module.Catalog.Entities;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Shared.DTOs;

namespace Procument.Module.OurInventory.Services;

public interface IStockPurchaseOrderService
{
    Task<StockPurchaseOrderResponse> CreateAsync(SaveStockPurchaseOrderRequest request);
    Task<StockPurchaseOrderResponse?> UpdateAsync(long id, SaveStockPurchaseOrderRequest request);
    Task<StockPurchaseOrderResponse?> SubmitAsync(long id);
    Task<StockPurchaseOrderResponse?> GetByIdAsync(long id);
    Task<PagedResult<StockPurchaseOrderResponse>> GetAllAsync(StockPurchaseOrderQuery query);
    Task<StockPurchaseOrderFilterOptions> GetFilterOptionsAsync(StockPurchaseOrderQuery query);
    Task<List<StockPurchaseSuggestion>> SuggestAsync(long? companyPresetId, long? warehouseId);
}

public sealed class StockPurchaseOrderService(
    DbContext db,
    IOptions<OurInventoryOptions> options) : IStockPurchaseOrderService
{
    public async Task<StockPurchaseOrderResponse> CreateAsync(SaveStockPurchaseOrderRequest request)
    {
        await ValidateAsync(request);
        // EnableRetryOnFailure rejects user transactions opened outside the execution strategy.
        var id = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            return await CreateInTransactionAsync(request);
        });
        return (await GetByIdAsync(id))!;
    }

    private async Task<long> CreateInTransactionAsync(SaveStockPurchaseOrderRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var po = new PurchaseOrder
        {
            // Unique until the number is assigned below: PurchaseOrders.PONumber is unique, so two
            // stock POs created at once must not both hold the same placeholder.
            PONumber = $"TMP-{Guid.NewGuid():N}",
            Origin = "Stock",
            Status = "Draft",
            AdminApproval = "Pending",
            SupplierId = request.SupplierId,
            CompanyPresetId = request.CompanyPresetId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            PreferredWalletId = await ResolvePreferredWalletAsync(request.CompanyPresetId),
            Subject = Clean(request.Subject),
            SupplierPIRef = Clean(request.SupplierPIRef),
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            CreatedAt = DateTime.UtcNow,
        };
        db.Set<PurchaseOrder>().Add(po);
        await db.SaveChangesAsync();

        po.PONumber = $"{options.Value.StockPoNumberPrefix}{po.Id}";
        await ReplaceLinesAsync(po, request.Items);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return po.Id;
    }

    public async Task<StockPurchaseOrderResponse?> UpdateAsync(long id, SaveStockPurchaseOrderRequest request)
    {
        await ValidateAsync(request);
        var updated = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            return await UpdateInTransactionAsync(id, request);
        });
        return updated ? await GetByIdAsync(id) : null;
    }

    private async Task<bool> UpdateInTransactionAsync(long id, SaveStockPurchaseOrderRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var po = await db.Set<PurchaseOrder>().Include(p => p.POItems)
            .FirstOrDefaultAsync(p => p.Id == id && p.Origin == "Stock");
        if (po is null) return false;
        if (!string.Equals(po.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only draft Stock POs can be edited.");

        po.SupplierId = request.SupplierId;
        po.CompanyPresetId = request.CompanyPresetId;
        po.DestinationWarehouseId = request.DestinationWarehouseId;
        po.PreferredWalletId = await ResolvePreferredWalletAsync(request.CompanyPresetId);
        po.Subject = Clean(request.Subject);
        po.SupplierPIRef = Clean(request.SupplierPIRef);
        po.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
        db.Set<POItem>().RemoveRange(po.POItems);
        await ReplaceLinesAsync(po, request.Items);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<StockPurchaseOrderResponse?> SubmitAsync(long id)
    {
        var po = await db.Set<PurchaseOrder>().Include(p => p.POItems)
            .FirstOrDefaultAsync(p => p.Id == id && p.Origin == "Stock");
        if (po is null) return null;
        if (!string.Equals(po.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only draft Stock POs can be submitted.");
        if (po.POItems.Count == 0) throw new InvalidOperationException("A Stock PO must contain at least one line.");

        po.Status = PurchaseOrderStatusFlow.WaitingForAdminApproval;
        po.AdminApproval = "Pending";
        po.AdminApprovalAt = null;
        po.AdminApprovalBy = null;
        po.AdminApprovalNote = null;
        foreach (var line in po.POItems) line.Status = PurchaseOrderStatusFlow.WaitingForAdminApproval;
        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<StockPurchaseOrderResponse?> GetByIdAsync(long id)
    {
        var po = await DetailsQuery().FirstOrDefaultAsync(p => p.Id == id && p.Origin == "Stock");
        if (po is null) return null;
        var result = Map(po);
        result.CompanyPresetName = po.CompanyPresetId.HasValue
            ? (await db.Set<CompanyPreset>().FindAsync(po.CompanyPresetId.Value))?.Name ?? string.Empty
            : string.Empty;
        await EnrichAsync([result]);
        return result;
    }

    public async Task<PagedResult<StockPurchaseOrderResponse>> GetAllAsync(StockPurchaseOrderQuery query)
    {
        var filtered = ApplyFilters(db.Set<PurchaseOrder>().AsNoTracking().Where(p => p.Origin == "Stock"), query);
        var count = await filtered.CountAsync();
        var sum = await filtered.SumAsync(p => p.TotalAmount ?? 0m);
        var ids = await filtered.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Select(p => p.Id).ApplyPaging(query).ToListAsync();
        var rows = await DetailsQuery().Where(p => ids.Contains(p.Id)).ToListAsync();
        var mapped = ids.Select(id => Map(rows.Single(p => p.Id == id))).ToList();
        var presetIds = rows.Where(p => p.CompanyPresetId.HasValue).Select(p => p.CompanyPresetId!.Value).Distinct().ToList();
        var presetNames = await db.Set<CompanyPreset>().Where(p => presetIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
        foreach (var row in mapped)
            row.CompanyPresetName = presetNames.GetValueOrDefault(row.CompanyPresetId, string.Empty);
        await EnrichAsync(mapped);
        return new PagedResult<StockPurchaseOrderResponse>
        {
            Items = mapped, TotalCount = count, Page = query.Page, PageSize = query.PageSize, TotalAmountSum = sum
        };
    }

    public async Task<StockPurchaseOrderFilterOptions> GetFilterOptionsAsync(StockPurchaseOrderQuery query)
    {
        var root = db.Set<PurchaseOrder>().AsNoTracking().Where(p => p.Origin == "Stock");
        var supplierIds = await ApplyFilters(root, query, "supplier").Select(p => p.SupplierId).Distinct().ToListAsync();
        var presetIds = await ApplyFilters(root, query, "preset").Where(p => p.CompanyPresetId.HasValue)
            .Select(p => p.CompanyPresetId!.Value).Distinct().ToListAsync();
        var warehouseIds = await ApplyFilters(root, query, "warehouse").Where(p => p.DestinationWarehouseId.HasValue)
            .Select(p => p.DestinationWarehouseId!.Value).Distinct().ToListAsync();
        return new StockPurchaseOrderFilterOptions
        {
            Suppliers = await db.Set<Supplier>().Where(x => supplierIds.Contains(x.Id)).OrderBy(x => x.Name)
                .Select(x => new StockPurchaseOrderOption(x.Id, x.Name)).ToListAsync(),
            CompanyPresets = await db.Set<CompanyPreset>().Where(x => presetIds.Contains(x.Id)).OrderBy(x => x.Name)
                .Select(x => new StockPurchaseOrderOption(x.Id, x.Name)).ToListAsync(),
            Warehouses = await db.Set<Warehouse>().Where(x => warehouseIds.Contains(x.Id)).OrderBy(x => x.DisplayName ?? x.Name)
                .Select(x => new StockPurchaseOrderOption(x.Id, x.DisplayName ?? x.Name)).ToListAsync(),
            Statuses = await ApplyFilters(root, query, "status").Select(p => p.Status).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }

    public Task<List<StockPurchaseSuggestion>> SuggestAsync(long? companyPresetId, long? warehouseId)
    {
        var query = db.Set<OurStockItem>().AsNoTracking()
            .Where(x => x.MinQty.HasValue && x.MinQty.Value > x.QtyAvailable);
        if (companyPresetId.HasValue) query = query.Where(x => x.CompanyPresetId == companyPresetId.Value);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        return query.GroupBy(x => new { x.PartNumberId, x.PartNumber.Name, x.Condition })
            .Select(g => new StockPurchaseSuggestion
            {
                PartNumberId = g.Key.PartNumberId,
                PartNumber = g.Key.Name,
                Condition = g.Key.Condition,
                QtyAvailable = g.Sum(x => x.QtyAvailable),
                MinQty = g.Sum(x => x.MinQty!.Value),
                Qty = (int)Math.Ceiling(g.Sum(x => x.MinQty!.Value - x.QtyAvailable)),
                SuggestedUnitPrice = g.OrderByDescending(x => x.UpdatedAt).Select(x => x.AvgUnitCost).FirstOrDefault(),
            }).OrderBy(x => x.PartNumber).ToListAsync();
    }

    private async Task ValidateAsync(SaveStockPurchaseOrderRequest request)
    {
        if (request.Items.Count == 0) throw new InvalidOperationException("At least one line is required.");
        if (request.Items.Any(x => x.Qty <= 0 || x.UnitPrice < 0 || (!x.PartNumberId.HasValue && string.IsNullOrWhiteSpace(x.PartNumber))))
            throw new InvalidOperationException("Each line needs a part number, positive quantity, and non-negative unit price.");
        if (!await db.Set<Supplier>().AnyAsync(x => x.Id == request.SupplierId && x.Status == "Approved"))
            throw new InvalidOperationException("Supplier was not found or is not approved.");
        if (!await db.Set<CompanyPreset>().AnyAsync(x => x.Id == request.CompanyPresetId && x.IsActive))
            throw new InvalidOperationException("Company preset was not found or is inactive.");
        if (!await db.Set<Warehouse>().AnyAsync(x => x.Id == request.DestinationWarehouseId && x.IsActive))
            throw new InvalidOperationException("Destination warehouse was not found or is inactive.");
        var hasMappings = await db.Set<CompanyPresetWarehouse>().AnyAsync(x => x.CompanyPresetId == request.CompanyPresetId);
        if (hasMappings && !await db.Set<CompanyPresetWarehouse>().AnyAsync(x => x.CompanyPresetId == request.CompanyPresetId && x.WarehouseId == request.DestinationWarehouseId))
            throw new InvalidOperationException("Destination warehouse is not linked to the selected company preset.");
    }

    private async Task ReplaceLinesAsync(PurchaseOrder po, IReadOnlyList<StockPurchaseOrderLineRequest> requests)
    {
        var lineNo = 1;
        decimal total = 0;
        foreach (var request in requests)
        {
            var part = await ResolvePartAsync(request);
            var line = new POItem
            {
                POId = po.Id,
                PartNumberId = part.Id,
                SupplierId = po.SupplierId,
                Qty = request.Qty,
                UnitPrice = request.UnitPrice,
                TotalPrice = request.Qty * request.UnitPrice,
                Condition = Clean(request.Condition) ?? "New",
                PORef = lineNo++,
                Status = po.Status,
            };
            db.Set<POItem>().Add(line);
            total += line.TotalPrice;
        }
        po.TotalAmount = total;
    }

    private async Task<PartNumber> ResolvePartAsync(StockPurchaseOrderLineRequest request)
    {
        if (request.PartNumberId.HasValue)
            return await db.Set<PartNumber>().FindAsync(request.PartNumberId.Value)
                ?? throw new InvalidOperationException($"Part number {request.PartNumberId.Value} was not found.");
        var name = request.PartNumber!.Trim();
        var normalized = name.ToUpper();
        var existing = await db.Set<PartNumber>().FirstOrDefaultAsync(x => x.Name.ToUpper() == normalized);
        if (existing is not null) return existing;
        var created = new PartNumber { Name = name, CreatedAt = DateTime.UtcNow };
        db.Set<PartNumber>().Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    private async Task<long?> ResolvePreferredWalletAsync(long companyPresetId)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        if (db.Database.CurrentTransaction is { } transaction)
            command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT TOP (1) Id FROM PaymentBoxes WHERE CompanyPresetId = @presetId ORDER BY CreatedAt, Id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@presetId";
        parameter.Value = companyPresetId;
        command.Parameters.Add(parameter);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    private IQueryable<PurchaseOrder> DetailsQuery() => db.Set<PurchaseOrder>().AsNoTracking()
        .Include(p => p.Supplier).Include(p => p.DestinationWarehouse)
        .Include(p => p.POItems).ThenInclude(i => i.PartNumber);

    private static IQueryable<PurchaseOrder> ApplyFilters(IQueryable<PurchaseOrder> query, StockPurchaseOrderQuery filter, string? exclude = null)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p => p.PONumber.Contains(term) || (p.Subject != null && p.Subject.Contains(term)) || (p.SupplierPIRef != null && p.SupplierPIRef.Contains(term)));
        }
        if (exclude != "supplier" && filter.SupplierIds?.Count > 0) query = query.Where(p => filter.SupplierIds.Contains(p.SupplierId));
        if (exclude != "preset" && filter.CompanyPresetIds?.Count > 0) query = query.Where(p => p.CompanyPresetId.HasValue && filter.CompanyPresetIds.Contains(p.CompanyPresetId.Value));
        if (exclude != "warehouse" && filter.WarehouseIds?.Count > 0) query = query.Where(p => p.DestinationWarehouseId.HasValue && filter.WarehouseIds.Contains(p.DestinationWarehouseId.Value));
        if (exclude != "status" && filter.Statuses?.Count > 0) query = query.Where(p => filter.Statuses.Contains(p.Status));
        if (filter.CreatedFrom.HasValue) query = query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);
        if (filter.CreatedTo.HasValue) query = query.Where(p => p.CreatedAt < filter.CreatedTo.Value.Date.AddDays(1));
        return query;
    }

    private static StockPurchaseOrderResponse Map(PurchaseOrder po) => new()
    {
        Id = po.Id, PONumber = po.PONumber, Origin = po.Origin, Status = po.Status,
        AdminApproval = po.AdminApproval, CreatedAt = po.CreatedAt, TotalAmount = po.TotalAmount ?? 0,
        SupplierId = po.SupplierId, SupplierName = po.Supplier?.Name ?? string.Empty,
        CompanyPresetId = po.CompanyPresetId.GetValueOrDefault(),
        DestinationWarehouseId = po.DestinationWarehouseId.GetValueOrDefault(),
        DestinationWarehouseName = po.DestinationWarehouse?.DisplayName ?? po.DestinationWarehouse?.Name ?? string.Empty,
        PreferredWalletId = po.PreferredWalletId, Subject = po.Subject, SupplierPIRef = po.SupplierPIRef,
        PaymentStatus = po.PaymentStatus, QtyOrdered = po.POItems.Where(i => i.ReturnedAt == null).Sum(i => i.Qty),
        ExpectedDeliveryDate = po.ExpectedDeliveryDate,
        Items = po.POItems.OrderBy(i => i.PORef).Select(i => new StockPurchaseOrderLineResponse
        {
            Id = i.Id, PORef = i.PORef ?? 0, PartNumberId = i.PartNumberId!.Value,
            PartNumber = i.PartNumber?.Name ?? string.Empty, Description = i.PartNumber?.Description,
            Qty = i.Qty, UnitPrice = i.UnitPrice, TotalPrice = i.TotalPrice, Condition = i.Condition ?? string.Empty,
        }).ToList(),
    };

    /// <summary>Adds payment and receipt progress. Payments live in the Sales module, so they are read with SQL.</summary>
    private async Task EnrichAsync(List<StockPurchaseOrderResponse> rows)
    {
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.Id).ToList();

        var prStatuses = await db.Set<PaymentRequest>().AsNoTracking()
            .Where(p => p.POId.HasValue && ids.Contains(p.POId.Value))
            .GroupBy(p => p.POId!.Value)
            .Select(g => new { POId = g.Key, Status = g.OrderByDescending(p => p.Id).Select(p => p.Status).FirstOrDefault() })
            .ToDictionaryAsync(x => x.POId, x => x.Status);

        var received = await db.Set<OurStockMovement>().AsNoTracking()
            .Where(m => m.POId.HasValue && ids.Contains(m.POId.Value) && m.POItemId.HasValue
                && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
            .GroupBy(m => m.POId!.Value)
            .Select(g => new { POId = g.Key, Qty = g.Sum(m => m.Qty) })
            .ToDictionaryAsync(x => x.POId, x => x.Qty);

        var paid = new Dictionary<long, decimal>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            if (db.Database.CurrentTransaction is { } transaction) command.Transaction = transaction.GetDbTransaction();
            var names = new List<string>();
            for (var i = 0; i < ids.Count; i++)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@po{i}";
                parameter.Value = ids[i];
                command.Parameters.Add(parameter);
                names.Add(parameter.ParameterName);
            }
            command.CommandText = $@"SELECT pr.POId, SUM(t.Amount) FROM PaymentTransactions t
                INNER JOIN PaymentRequests pr ON pr.Id = t.PaymentRequestId
                WHERE t.Type = 'Withdraw' AND t.ToType = 'Supplier' AND pr.POId IN ({string.Join(",", names)})
                GROUP BY pr.POId";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                paid[reader.GetInt64(0)] = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
        }

        foreach (var row in rows)
        {
            row.PrStatus = prStatuses.GetValueOrDefault(row.Id);
            row.QtyReceived = received.GetValueOrDefault(row.Id);
            row.PaidAmount = paid.GetValueOrDefault(row.Id);
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
