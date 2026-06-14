using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IWarehouseService
{
    Task<List<WarehouseResponse>> GetAllAsync();
    Task<WarehouseResponse?> GetByIdAsync(long id);
    Task<WarehouseResponse> CreateAsync(SaveWarehouseRequest request);
    Task<WarehouseResponse?> UpdateAsync(long id, SaveWarehouseRequest request);
    Task<bool> DeleteAsync(long id);

    // User assignments
    Task<List<WarehouseUserResponse>> GetUsersAsync(long warehouseId);
    Task<bool> AssignUserAsync(long warehouseId, long userId);
    Task<bool> UnassignUserAsync(long warehouseId, long userId);

    // Company Preset links
    Task<List<WarehouseResponse>> GetByCompanyPresetAsync(long companyPresetId);
    Task<bool> LinkCompanyPresetAsync(long companyPresetId, long warehouseId);
    Task<bool> UnlinkCompanyPresetAsync(long companyPresetId, long warehouseId);

    // Helper for Inventory user → their warehouse IDs
    Task<List<long>> GetWarehouseIdsForUserAsync(long userId);
}

public class WarehouseService : IWarehouseService
{
    private readonly DbContext _db;

    public WarehouseService(DbContext db) => _db = db;

    public async Task<List<WarehouseResponse>> GetAllAsync()
    {
        return await _db.Set<Warehouse>()
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => MapResponse(w))
            .ToListAsync();
    }

    public async Task<WarehouseResponse?> GetByIdAsync(long id)
    {
        var w = await _db.Set<Warehouse>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return w == null ? null : MapResponse(w);
    }

    public async Task<WarehouseResponse> CreateAsync(SaveWarehouseRequest request)
    {
        var w = new Warehouse
        {
            Name = request.Name.Trim(),
            DisplayName = request.DisplayName?.Trim(),
            Type = request.Type ?? "OurWarehouse",
            Address = request.Address?.Trim(),
            ShipToAddress = request.ShipToAddress?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            FedexAccount = request.FedexAccount?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<Warehouse>().Add(w);
        await _db.SaveChangesAsync();
        return MapResponse(w);
    }

    public async Task<WarehouseResponse?> UpdateAsync(long id, SaveWarehouseRequest request)
    {
        var w = await _db.Set<Warehouse>().FindAsync(id);
        if (w == null) return null;
        w.Name = request.Name.Trim();
        w.DisplayName = request.DisplayName?.Trim();
        w.Type = request.Type ?? w.Type;
        w.Address = request.Address?.Trim();
        w.ShipToAddress = request.ShipToAddress?.Trim();
        w.Phone = request.Phone?.Trim();
        w.Email = request.Email?.Trim();
        w.FedexAccount = request.FedexAccount?.Trim();
        if (request.IsActive.HasValue) w.IsActive = request.IsActive.Value;
        await _db.SaveChangesAsync();
        return MapResponse(w);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var w = await _db.Set<Warehouse>().FindAsync(id);
        if (w == null) return false;
        w.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<WarehouseUserResponse>> GetUsersAsync(long warehouseId)
    {
        return await _db.Set<UserWarehouse>()
            .AsNoTracking()
            .Include(uw => uw.User)
            .Where(uw => uw.WarehouseId == warehouseId)
            .Select(uw => new WarehouseUserResponse { Id = uw.User.Id, Name = uw.User.Name, Email = uw.User.Email })
            .ToListAsync();
    }

    public async Task<bool> AssignUserAsync(long warehouseId, long userId)
    {
        var exists = await _db.Set<UserWarehouse>().AnyAsync(uw => uw.WarehouseId == warehouseId && uw.UserId == userId);
        if (exists) return true;
        _db.Set<UserWarehouse>().Add(new UserWarehouse { WarehouseId = warehouseId, UserId = userId });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnassignUserAsync(long warehouseId, long userId)
    {
        var uw = await _db.Set<UserWarehouse>().FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.UserId == userId);
        if (uw == null) return false;
        _db.Set<UserWarehouse>().Remove(uw);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<WarehouseResponse>> GetByCompanyPresetAsync(long companyPresetId)
    {
        return await _db.Set<CompanyPresetWarehouse>()
            .AsNoTracking()
            .Include(cpw => cpw.Warehouse)
            .Where(cpw => cpw.CompanyPresetId == companyPresetId && cpw.Warehouse.IsActive)
            .Select(cpw => MapResponse(cpw.Warehouse))
            .ToListAsync();
    }

    public async Task<bool> LinkCompanyPresetAsync(long companyPresetId, long warehouseId)
    {
        var exists = await _db.Set<CompanyPresetWarehouse>().AnyAsync(x => x.CompanyPresetId == companyPresetId && x.WarehouseId == warehouseId);
        if (exists) return true;
        _db.Set<CompanyPresetWarehouse>().Add(new CompanyPresetWarehouse { CompanyPresetId = companyPresetId, WarehouseId = warehouseId });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnlinkCompanyPresetAsync(long companyPresetId, long warehouseId)
    {
        var link = await _db.Set<CompanyPresetWarehouse>().FirstOrDefaultAsync(x => x.CompanyPresetId == companyPresetId && x.WarehouseId == warehouseId);
        if (link == null) return false;
        _db.Set<CompanyPresetWarehouse>().Remove(link);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<long>> GetWarehouseIdsForUserAsync(long userId)
    {
        return await _db.Set<UserWarehouse>()
            .AsNoTracking()
            .Where(uw => uw.UserId == userId)
            .Select(uw => uw.WarehouseId)
            .ToListAsync();
    }

    private static WarehouseResponse MapResponse(Warehouse w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        DisplayName = w.DisplayName,
        Type = w.Type,
        Address = w.Address,
        ShipToAddress = w.ShipToAddress,
        Phone = w.Phone,
        Email = w.Email,
        FedexAccount = w.FedexAccount,
        IsActive = w.IsActive,
        CreatedAt = w.CreatedAt,
    };
}
