using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>One availability file (ILS "Parts Availability Results" CSV, or Excel) a user uploaded.</summary>
public class ProcurementPlusUpload : BaseEntity
{
    public long UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int RowCount { get; set; }

    public ICollection<ProcurementPlusOffer> Offers { get; set; } = new List<ProcurementPlusOffer>();
}

/// <summary>One row of an uploaded availability file: a company listing a part.</summary>
public class ProcurementPlusOffer : BaseEntity
{
    public long UploadId { get; set; }
    public string PartNo { get; set; } = string.Empty;
    public string PartKey { get; set; } = string.Empty;
    public string? AltPartNo { get; set; }
    public string? AltKey { get; set; }
    /// <summary>Company as written in the file.</summary>
    public string CompanyRaw { get; set; } = string.Empty;
    /// <summary>SupplierNameNormalizer.Key of CompanyRaw.</summary>
    public string CompanyKey { get; set; } = string.Empty;
    /// <summary>Catalog supplier — matched by email, alias or name on upload, or picked by the user.</summary>
    public long? SupplierId { get; set; }
    public string? SupplierComment { get; set; }
    /// <summary>Qty as written ("27", "RQST").</summary>
    public string? QtyRaw { get; set; }
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    public string? Description { get; set; }
    public string? PriceRaw { get; set; }
    public decimal? Price { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    /// <summary>Email column, else the first address written in the supplier comment.</summary>
    public string? Email { get; set; }
    public string? Distance { get; set; }
    public string? Cage { get; set; }
    public string? Exch { get; set; }

    public ProcurementPlusUpload Upload { get; set; } = null!;
    public Supplier? Supplier { get; set; }
}
