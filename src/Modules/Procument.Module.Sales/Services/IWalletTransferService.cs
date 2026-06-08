using Microsoft.AspNetCore.Http;
using Procument.Module.Sales.DTOs;

namespace Procument.Module.Sales.Services;

public interface IWalletTransferService
{
    Task<List<WalletTransferPendingResponse>> GetAllAsync();
    Task<WalletTransferPendingResponse?> GetByIdAsync(long id);
    Task<WalletTransferPendingResponse> CreateAsync(CreateWalletTransferPendingRequest req, long userId);
    Task<bool> ReviewAsync(long id, string decision, string? note, long userId);
    Task<bool> UploadPopAndExecuteAsync(long id, IFormFile file, long userId, string storageRoot);
    Task<(Stream Stream, string FileName, string MimeType)?> GetPopFileAsync(long id, string storageRoot);
}
