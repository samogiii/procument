using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class CompanyPresetWarehouse : BaseEntity
{
    public long CompanyPresetId { get; set; }
    public long WarehouseId { get; set; }

    // Navigation
    public CompanyPreset CompanyPreset { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
