using Microsoft.AspNetCore.Http;
using Procument.Module.Sales.DTOs;

namespace Procument.Module.Sales.Services;

public interface IWalletTransferService
{
    Task<List<WalletTransferPendingResponse>> GetAllAsync();
    Task<WalletTransferPendingResponse?> GetByIdAsync(long id);
    Task<WalletTransferPendingResponse> CreateAsync(CreateWalletTransferPendingRequest req, long userId);
    /// <summary>POP uploaded in the office: the transfer goes to Review (and the POP reader) like a warehouse one.</summary>
    Task<bool> UploadPopAsync(long id, IFormFile file, long userId, string storageRoot);
    /// <summary>The warehouse phone's POP and the rate saved with it: the transfer goes to Review. Null = stored, otherwise why not.</summary>
    Task<string?> StoreFromWarehouseAsync(long id, decimal? rate, decimal? bocRate, string? rateAt,
        string by, DateTime at, string fileName, byte[] pop, string storageRoot);
    /// <summary>The office's final numbers: executes the transfer. Null = done, otherwise why not.</summary>
    Task<string?> AcceptAsync(long id, AcceptWalletTransferRequest req, long userId);
    Task<(Stream Stream, string FileName, string MimeType)?> GetPopFileAsync(long id, string storageRoot);
}
