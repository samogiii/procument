using Procument.Module.Sales.DTOs;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;

namespace Procument.Module.Sales.Services;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin, string? status = null, string? customer = null);
    Task<InvoiceResponse?> GetByIdAsync(long id, long userId, bool isAdmin);
    Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, long userId);
    Task<bool> UpdateAsync(long id, UpdateInvoiceRequest request);
    Task<bool> UpdateItemsAsync(long id, UpdateInvoiceItemsRequest request);
    Task<bool> UpdateStatusAsync(long id, string status, long userId, bool isAdmin);
    Task<bool> GrantPermissionsAsync(List<long> invoiceIds, long targetUserId, string permission);
    Task<bool> DeleteAsync(long id);
}
