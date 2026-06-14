using Procument.Module.Identity.Entities;
using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class Warehouse : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    /// <summary>"OurWarehouse" or "Forwarded"</summary>
    public string Type { get; set; } = "OurWarehouse";

    public string? Address { get; set; }
    public string? ShipToAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FedexAccount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UserWarehouse> UserWarehouses { get; set; } = new List<UserWarehouse>();
    public ICollection<CompanyPresetWarehouse> CompanyPresetWarehouses { get; set; } = new List<CompanyPresetWarehouse>();
    public ICollection<POItemTrackNumber> TrackNumbers { get; set; } = new List<POItemTrackNumber>();
    public ICollection<ShipmentNote> ShipmentNotes { get; set; } = new List<ShipmentNote>();
}
