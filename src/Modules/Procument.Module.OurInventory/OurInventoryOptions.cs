namespace Procument.Module.OurInventory;

public sealed class OurInventoryOptions
{
    public const string SectionName = "OurInventory";

    public string StockPoNumberPrefix { get; set; } = "SPO-";
    public string OurStockSupplierName { get; set; } = "OUR STOCK";
    public int QuoteReservationDays { get; set; } = 14;
    public bool AllowNegativeStock { get; set; }
    public bool UseLandedCost { get; set; }
}
