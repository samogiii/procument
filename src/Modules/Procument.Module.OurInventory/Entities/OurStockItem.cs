using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.OurInventory.Entities;

public class OurStockItem : BaseEntity
{
    public long PartNumberId { get; set; }
    public string Condition { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public long CompanyPresetId { get; set; }
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyAvailable { get; private set; }
    public decimal AvgUnitCost { get; set; }
    public string? CertName { get; set; }
    public DateTime? TagDate { get; set; }
    public string? BinLocation { get; set; }
    public decimal? MinQty { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    public PartNumber PartNumber { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public CompanyPreset CompanyPreset { get; set; } = null!;
    public ICollection<OurStockMovement> Movements { get; set; } = [];
    public ICollection<OurStockReservation> Reservations { get; set; } = [];
    public ICollection<OurStockSerial> Serials { get; set; } = [];
}
