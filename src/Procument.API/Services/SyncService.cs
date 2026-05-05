using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;
using Procument.Shared.Services;

namespace Procument.API.Services;

public interface ISyncService
{
    Task<string> SyncWithSatelliteAsync(long nodeId);
    Task<List<(string NodeName, string Result)>> SyncAllAsync();
    Task<(string PrivateKey, string PublicKey)> GenerateMainKeysAsync();
}

public class SyncService : ISyncService
{
    private readonly AppDbContext _db;
    private readonly ICryptoService _crypto;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public SyncService(AppDbContext db, ICryptoService crypto, IConfiguration config, HttpClient httpClient)
    {
        _db = db;
        _crypto = crypto;
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<List<(string NodeName, string Result)>> SyncAllAsync()
    {
        var nodes = await _db.SatelliteNodes.ToListAsync();
        var results = new List<(string NodeName, string Result)>();

        foreach (var node in nodes)
        {
            try
            {
                var result = await SyncWithSatelliteAsync(node.Id);
                results.Add((node.Name, result));
            }
            catch (Exception ex)
            {
                results.Add((node.Name, $"Error: {ex.Message}"));
            }
        }

        return results;
    }

    public async Task<(string PrivateKey, string PublicKey)> GenerateMainKeysAsync()
    {
        return _crypto.GenerateRsaKeyPair();
    }

    public async Task<string> SyncWithSatelliteAsync(long nodeId)
    {
        var node = await _db.SatelliteNodes.FindAsync(nodeId);
        if (node == null) return "Node not found";

        string mainPrivateKey = _config["Sync:MainPrivateKey"] ?? throw new Exception("Main Private Key not configured");
        string mainPublicKey = _config["Sync:MainPublicKey"] ?? throw new Exception("Main Public Key not configured");

        // 1. Gather RFQs for this base (Full Sync)
        var rfqsToSync = await _db.RFQs
            .Include(r => r.Customer)
            .Include(r => r.RFQItems)
                .ThenInclude(i => i.PartNumber)
            .Where(r => r.Customer.Base == node.BaseNumber)
            .ToListAsync();

        int sentCount = rfqsToSync.Count;

        var payload = new SyncPayload
        {
            LastSyncTime = node.LastSyncAt ?? DateTime.MinValue,
            RFQs = rfqsToSync.Select(r => new SyncRFQData
            {
                Id = r.Id,
                MainAppId = r.Id,
                Name = r.Name,
                Status = r.Status,
                ExType = r.ExType ?? 0,
                LeadTime = r.LeadTime,
                Notes = r.Notes,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer.Name,
                CustomerCode = r.Customer.CustomerCode,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.ModifyAt,
                ReceivedDate= r.ReceivedDate,
                Items = r.RFQItems.Select(i => new SyncRFQItemData
                {
                    Id = i.Id,
                    MainAppId = i.Id,
                    Alt = i.Alt,
                    Condition = i.Condition,
                    Qty = (int)i.Qty,
                    Unit = i.Unit,
                    Priority = i.Priority,
                    Remark = i.Note,
                    PartNumberId = i.PartNumberId,
                    PartNumberName = i.PartNumber.Name,
                    PartNumberDescription = i.PartNumber.Description
                }).ToList()
            }).ToList()
        };

        // 2. Encrypt and Sign
        string jsonPayload = JsonSerializer.Serialize(payload);
        var encrypted = _crypto.EncryptAndSign(jsonPayload, node.PublicKey, mainPrivateKey);

        var requestDto = new SyncExchangeRequest
        {
            SenderPublicKey = mainPublicKey,
            EncryptedKey = encrypted.EncryptedKey,
            Iv = encrypted.Iv,
            AuthTag = encrypted.AuthTag,
            CipherText = encrypted.CipherText,
            Signature = encrypted.Signature
        };

        // 3. POST to Satellite
        string baseUrl = node.EndpointUrl.TrimEnd('/');
        string targetUrl = baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase) 
            ? $"{baseUrl}/sync/exchange" 
            : $"{baseUrl}/api/sync/exchange";

        var response = await _httpClient.PostAsJsonAsync(targetUrl, requestDto);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"Sync failed: {response.StatusCode} - {error}";
        }

        var responseDto = await response.Content.ReadFromJsonAsync<SyncExchangeResponse>();
        if (responseDto == null) return "Empty response from satellite";

        // 4. Decrypt and Verify Response
        string decryptedJson = _crypto.VerifyAndDecrypt(
            responseDto.CipherText,
            responseDto.EncryptedKey,
            responseDto.Iv,
            responseDto.AuthTag,
            responseDto.Signature,
            node.PublicKey,
            mainPrivateKey
        );

        var satellitePayload = JsonSerializer.Deserialize<SyncPayload>(decryptedJson);
        if (satellitePayload == null) return "Invalid payload in response";

        // 5. Apply changes from Satellite
        int updatedCount = 0;
        int createdCount = 0;

        foreach (var sRfq in satellitePayload.RFQs)
        {
            RFQHeader? mainRfq = null;

            // 1. Try to find by MainAppId first
            if (sRfq.MainAppId.HasValue)
            {
                mainRfq = await _db.RFQs.Include(r => r.RFQItems).FirstOrDefaultAsync(r => r.Id == sRfq.MainAppId.Value);
            }

            // 2. Fallback to registry
            if (mainRfq == null)
            {
                var registry = await _db.SyncRegistries
                    .FirstOrDefaultAsync(x => x.EntityName == "RFQ" && x.SatelliteAppId == sRfq.Id && x.SatelliteNodeId == node.Id);

                if (registry != null)
                {
                    mainRfq = await _db.RFQs.Include(r => r.RFQItems).FirstOrDefaultAsync(r => r.Id == registry.MainAppId);
                }
            }

            if (mainRfq == null)
            {
                // Try to find customer
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerCode == sRfq.CustomerCode && c.Base == node.BaseNumber);
                if (customer == null)
                {
                    customer = new Customer
                    {
                        Name = sRfq.CustomerName,
                        CustomerCode = sRfq.CustomerCode,
                        Base = node.BaseNumber,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _db.Customers.Add(customer);
                    await _db.SaveChangesAsync();
                }

                mainRfq = new RFQHeader
                {
                    Name = sRfq.Name,
                    Status = sRfq.Status,
                    ExType = sRfq.ExType,
                    LeadTime = sRfq.LeadTime ?? DateTime.UtcNow.AddDays(7),
                    Notes = sRfq.Notes,
                    CustomerId = customer.Id,
                    CreatedAt = sRfq.CreatedAt,
                    ModifyAt = sRfq.UpdatedAt,
                    ReceivedDate = sRfq.ReceivedDate
                };
                _db.RFQs.Add(mainRfq);
                await _db.SaveChangesAsync();

                _db.SyncRegistries.Add(new SyncRegistry
                {
                    EntityName = "RFQ",
                    MainAppId = mainRfq.Id,
                    SatelliteAppId = sRfq.Id,
                    SatelliteNodeId = node.Id,
                    LastSyncAt = DateTime.UtcNow
                });
                createdCount++;
            }
            else
            {
                // Update existing - only if Satellite version is newer or equal
                if (sRfq.UpdatedAt >= (mainRfq.ModifyAt ?? DateTime.MinValue))
                {
                    mainRfq.Name = sRfq.Name;
                    mainRfq.Status = sRfq.Status;
                    mainRfq.ExType = sRfq.ExType;
                    mainRfq.LeadTime = sRfq.LeadTime ?? mainRfq.LeadTime;
                    mainRfq.Notes = sRfq.Notes;
                    mainRfq.ReceivedDate = sRfq.ReceivedDate;
                    mainRfq.ModifyAt = sRfq.UpdatedAt;
                    updatedCount++;
                }
            }

            // Sync Items
            foreach (var sItem in sRfq.Items)
            {
                RFQItem? mainItem = null;

                if (sItem.MainAppId.HasValue)
                {
                    mainItem = mainRfq.RFQItems.FirstOrDefault(i => i.Id == sItem.MainAppId.Value);
                }

                if (mainItem == null)
                {
                    var itemRegistry = await _db.SyncRegistries
                        .FirstOrDefaultAsync(x => x.EntityName == "RFQItem" && x.SatelliteAppId == sItem.Id && x.SatelliteNodeId == node.Id);

                    if (itemRegistry != null)
                    {
                        mainItem = mainRfq.RFQItems.FirstOrDefault(i => i.Id == itemRegistry.MainAppId);
                    }
                }

                if (mainItem == null)
                {
                    var partNumber = await _db.PartNumbers.FirstOrDefaultAsync(p => p.Name == sItem.PartNumberName);
                    if (partNumber == null)
                    {
                        partNumber = new PartNumber 
                        { 
                            Name = sItem.PartNumberName, 
                            Description = sItem.PartNumberDescription,
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.PartNumbers.Add(partNumber);
                        await _db.SaveChangesAsync();
                    }

                    mainItem = new RFQItem
                    {
                        RFQId = mainRfq.Id,
                        PartNumberId = partNumber.Id,
                        Qty = sItem.Qty,
                        Alt = sItem.Alt,
                        Condition = sItem.Condition,
                        Unit = sItem.Unit,
                        Priority = sItem.Priority,
                        Note = sItem.Remark
                    };
                    _db.RFQItems.Add(mainItem);
                    await _db.SaveChangesAsync();

                    _db.SyncRegistries.Add(new SyncRegistry
                    {
                        EntityName = "RFQItem",
                        MainAppId = mainItem.Id,
                        SatelliteAppId = sItem.Id,
                        SatelliteNodeId = node.Id,
                        LastSyncAt = DateTime.UtcNow
                    });
                }
                else
                {
                    // Update item only if RFQ was updated
                    if (sRfq.UpdatedAt >= (mainRfq.ModifyAt ?? DateTime.MinValue))
                    {
                        mainItem.Qty = sItem.Qty;
                        mainItem.Alt = sItem.Alt;
                        mainItem.Condition = sItem.Condition;
                        mainItem.Unit = sItem.Unit;
                        mainItem.Priority = sItem.Priority;
                        mainItem.Note = sItem.Remark;
                    }
                }
            }
        }

        node.LastSyncAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return $"Sync success: Sent {sentCount} updates. Received {createdCount} new and {updatedCount} updated RFQs.";
    }
}
