namespace Procument.Module.Sales.DTOs;

public class GrantPermissionRequest
{
    public List<long> InvoiceIds { get; set; } = new();
    public long TargetUserId { get; set; }
    public string Permission { get; set; } = string.Empty;
}
