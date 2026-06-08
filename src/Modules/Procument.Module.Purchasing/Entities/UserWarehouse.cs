using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class UserWarehouse : BaseEntity
{
    public long UserId { get; set; }
    public long WarehouseId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
