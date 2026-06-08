using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;
using Procument.Shared.Services;
using System.Text.Json;

namespace Procument.API.Controllers;

[Authorize]
[ApiController]
[Route("api/browser-sync")]
public class BrowserSyncController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ICryptoService _crypto;

    public BrowserSyncController(AppDbContext db, IConfiguration config, ICryptoService crypto)
    {
        _db = db;
        _config = config;
        _crypto = crypto;
    }

    private string SharedKey => _config["BrowserSync:SharedKey"]
        ?? throw new InvalidOperationException("BrowserSync:SharedKey not configured");

    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    // Returns satellite connection details — SharedKey is NOT included; crypto stays server-side.
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new BrowserSyncConfigResponse
        {
            SatelliteUrl = _config["BrowserSync:SatelliteUrl"] ?? string.Empty,
            ApiKey = _config["BrowserSync:SatelliteApiKey"] ?? string.Empty,
            DefaultNodeId = _config.GetValue<long>("BrowserSync:DefaultSatelliteNodeId", 0)
        });
    }

    // Fetches main RFQs, encrypts them server-side, returns the blob for the browser to relay to satellite.
    // since: ISO-8601 UTC cutoff — only RFQs created or modified at or after this time are included.
    // Defaults to 24 hours ago so the first sync doesn't push every record ever.
    [HttpGet("prepare-sync")]
    public async Task<IActionResult> PrepareSyncPayload(
        [FromQuery] int baseNumber = 2,
        [FromQuery] DateTime? since = null)
    {
        var cutoff = since?.ToUniversalTime() ?? DateTime.UtcNow.AddHours(-24);

        var rfqs = await _db.RFQs
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.RFQItems).ThenInclude(i => i.PartNumber)
            .Where(r => r.Customer.Base == baseNumber
                     && (r.CreatedAt >= cutoff || r.ModifyAt >= cutoff))
            .ToListAsync();

        var payload = new SyncPayload
        {
            LastSyncTime = cutoff, // satellite uses this to filter its own response
            RFQs = rfqs.Select(r => new SyncRFQData
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
                CustomerEmail = r.Customer.Email,
                CustomerPhone = r.Customer.Phone,
                CustomerContactPerson = r.Customer.ContactPerson,
                CustomerShipTo = r.Customer.ShipTo,
                CustomerBillTo = r.Customer.BillTo,
                CustomerShippingAccount = r.Customer.ShippingAccount,
                CustomerDescription = r.Customer.Description,
                CustomerIsActive = r.Customer.IsActive,
                CustomerModifyAt = r.Customer.ModifyAt,
                CustomerTermsAndConditions = r.Customer.TermsAndConditions,
                CustomerCurrencyType = r.Customer.CurrencyType,
                CustomerExWork = r.Customer.ExWork,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.ModifyAt,
                ReceivedDate = r.ReceivedDate,
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

        string json = JsonSerializer.Serialize(payload, CamelCase);
        string cipherText = _crypto.EncryptBrowser(json, SharedKey, out string iv);
        return Ok(new { cipherText, iv });
    }

    // Receives the satellite's encrypted response relayed by the browser.
    // Decrypts it server-side and returns a plain diff the browser can display.
    [HttpPost("process-exchange")]
    public async Task<IActionResult> ProcessExchange([FromBody] SatelliteRelayResponse relay)
    {
        string json = _crypto.DecryptBrowser(relay.CipherText, relay.Iv, SharedKey);
        var satellitePayload = JsonSerializer.Deserialize<SyncPayload>(json, CaseInsensitive);
        if (satellitePayload == null) return BadRequest("Invalid satellite payload");

        var mainRfqs = await _db.RFQs.AsNoTracking()
            .Include(r => r.RFQItems)
            .ToListAsync();
        var mainById = mainRfqs.ToDictionary(r => r.Id);

        var newRfqs = new List<SyncRFQData>();
        var updatedRfqs = new List<SyncRFQData>();

        foreach (var sRfq in satellitePayload.RFQs)
        {
            if (sRfq.MainAppId == null || !mainById.ContainsKey(sRfq.MainAppId.Value))
            {
                newRfqs.Add(sRfq);
            }
            else if (sRfq.UpdatedAt > (mainById[sRfq.MainAppId.Value].ModifyAt ?? DateTime.MinValue))
            {
                updatedRfqs.Add(sRfq);
            }
        }

        return Ok(new
        {
            newRfqs,
            updatedRfqs,
            totalSatelliteRfqs = relay.TotalSatelliteRfqs,
            receivedCount = relay.ReceivedCount,
            pendingCount = relay.PendingCount
        });
    }

    // Encrypts ID mappings server-side so the browser can relay them to satellite.
    [HttpPost("encrypt-mappings")]
    public IActionResult EncryptMappings([FromBody] List<RfqIdMapping> mappings)
    {
        string json = JsonSerializer.Serialize(mappings, CamelCase);
        string cipherText = _crypto.EncryptBrowser(json, SharedKey, out string iv);
        return Ok(new { cipherText, iv });
    }

    // Decrypts an encrypted package exported by the satellite browser for manual import.
    [HttpPost("decrypt-package")]
    public IActionResult DecryptPackage([FromBody] BrowserSyncPackageRequest request)
    {
        string json = _crypto.DecryptBrowser(request.CipherText, request.Iv, SharedKey);
        var payload = JsonSerializer.Deserialize<SyncPayload>(json, CaseInsensitive);
        if (payload == null) return BadRequest("Invalid package");
        return Ok(payload);
    }

    // Imports satellite RFQs into the main database.
    [HttpPost("import")]
    public async Task<IActionResult> ImportFromSatellite([FromBody] BrowserImportRequest request)
    {
        SatelliteNode? node = null;
        if (request.NodeId > 0)
            node = await _db.SatelliteNodes.FindAsync(request.NodeId);

        if (node == null)
        {
            var satelliteUrl = _config["BrowserSync:SatelliteUrl"]?.TrimEnd('/');
            if (!string.IsNullOrEmpty(satelliteUrl))
            {
                // TrimEnd isn't translatable to SQL — evaluate client-side
                var candidates = await _db.SatelliteNodes
                    .Where(n => n.EndpointUrl != null)
                    .ToListAsync();
                node = candidates.FirstOrDefault(n =>
                    n.EndpointUrl!.TrimEnd('/').StartsWith(satelliteUrl));
            }
        }

        bool trackRegistry = node != null;
        int satelliteBaseNumber = node?.BaseNumber ?? _config.GetValue<int>("Satellite:BaseNumber", 2);

        int created = 0, updated = 0;
        var idMappings = new List<RfqIdMapping>();

        foreach (var sRfq in request.RFQs)
        {
            RFQHeader? mainRfq = null;

            if (sRfq.MainAppId.HasValue)
                mainRfq = await _db.RFQs.Include(r => r.RFQItems)
                    .FirstOrDefaultAsync(r => r.Id == sRfq.MainAppId.Value);

            if (mainRfq == null && trackRegistry)
            {
                var reg = await _db.SyncRegistries.FirstOrDefaultAsync(x =>
                    x.EntityName == "RFQ" && x.SatelliteAppId == sRfq.Id && x.SatelliteNodeId == node!.Id);
                if (reg != null)
                    mainRfq = await _db.RFQs.Include(r => r.RFQItems)
                        .FirstOrDefaultAsync(r => r.Id == reg.MainAppId);
            }

            if (mainRfq == null)
            {
                var customer = !string.IsNullOrEmpty(sRfq.CustomerCode)
                    ? await _db.Customers.FirstOrDefaultAsync(c => c.CustomerCode == sRfq.CustomerCode && c.Base == satelliteBaseNumber)
                    : await _db.Customers.FirstOrDefaultAsync(c => c.Name == sRfq.CustomerName && c.Base == satelliteBaseNumber);

                if (customer == null)
                {
                    customer = new Customer { CreatedAt = DateTime.UtcNow, Base = satelliteBaseNumber };
                    _db.Customers.Add(customer);
                }

                // Always keep customer info in sync with satellite's version.
                customer.Name = sRfq.CustomerName;
                customer.CustomerCode = sRfq.CustomerCode;
                customer.Email = sRfq.CustomerEmail;
                customer.Phone = sRfq.CustomerPhone;
                customer.ContactPerson = sRfq.CustomerContactPerson;
                customer.ShipTo = sRfq.CustomerShipTo;
                customer.BillTo = sRfq.CustomerBillTo;
                customer.ShippingAccount = sRfq.CustomerShippingAccount;
                customer.Description = sRfq.CustomerDescription;
                customer.IsActive = sRfq.CustomerIsActive;
                customer.ModifyAt = sRfq.CustomerModifyAt;
                customer.TermsAndConditions = sRfq.CustomerTermsAndConditions;
                customer.CurrencyType = sRfq.CustomerCurrencyType;
                customer.ExWork = sRfq.CustomerExWork;
                await _db.SaveChangesAsync();

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

                if (trackRegistry)
                    _db.SyncRegistries.Add(new SyncRegistry
                    {
                        EntityName = "RFQ",
                        MainAppId = mainRfq.Id,
                        SatelliteAppId = sRfq.Id,
                        SatelliteNodeId = node!.Id,
                        LastSyncAt = DateTime.UtcNow
                    });

                idMappings.Add(new RfqIdMapping { SatelliteId = sRfq.Id, MainAppId = mainRfq.Id });
                created++;
            }
            else if (sRfq.UpdatedAt >= (mainRfq.ModifyAt ?? DateTime.MinValue))
            {
                mainRfq.Name = sRfq.Name;
                mainRfq.Status = sRfq.Status;
                mainRfq.ExType = sRfq.ExType;
                mainRfq.LeadTime = sRfq.LeadTime ?? mainRfq.LeadTime;
                mainRfq.Notes = sRfq.Notes;
                mainRfq.ReceivedDate = sRfq.ReceivedDate;
                mainRfq.ModifyAt = sRfq.UpdatedAt;
                updated++;
            }

            var rfqMapping = idMappings.FirstOrDefault(m => m.SatelliteId == sRfq.Id);

            foreach (var sItem in sRfq.Items)
            {
                RFQItem? mainItem = sItem.MainAppId.HasValue
                    ? mainRfq.RFQItems.FirstOrDefault(i => i.Id == sItem.MainAppId.Value)
                    : null;

                if (mainItem == null && trackRegistry)
                {
                    var itemReg = await _db.SyncRegistries.FirstOrDefaultAsync(x =>
                        x.EntityName == "RFQItem" && x.SatelliteAppId == sItem.Id && x.SatelliteNodeId == node!.Id);
                    if (itemReg != null)
                        mainItem = mainRfq.RFQItems.FirstOrDefault(i => i.Id == itemReg.MainAppId);
                }

                if (mainItem == null)
                {
                    var pn = await _db.PartNumbers.FirstOrDefaultAsync(p => p.Name == sItem.PartNumberName);
                    if (pn == null)
                    {
                        pn = new PartNumber { Name = sItem.PartNumberName, Description = sItem.PartNumberDescription, CreatedAt = DateTime.UtcNow };
                        _db.PartNumbers.Add(pn);
                        await _db.SaveChangesAsync();
                    }

                    var newItem = new RFQItem
                    {
                        RFQId = mainRfq.Id,
                        PartNumberId = pn.Id,
                        Qty = sItem.Qty,
                        Alt = sItem.Alt,
                        Condition = sItem.Condition,
                        Unit = sItem.Unit,
                        Priority = sItem.Priority,
                        Note = sItem.Remark
                    };
                    _db.RFQItems.Add(newItem);
                    await _db.SaveChangesAsync();

                    if (trackRegistry)
                        _db.SyncRegistries.Add(new SyncRegistry
                        {
                            EntityName = "RFQItem",
                            MainAppId = newItem.Id,
                            SatelliteAppId = sItem.Id,
                            SatelliteNodeId = node!.Id,
                            LastSyncAt = DateTime.UtcNow
                        });

                    rfqMapping?.Items.Add(new ItemIdMapping { SatelliteId = sItem.Id, MainAppId = newItem.Id });
                }
                else
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

        await _db.SaveChangesAsync();
        return Ok(new BrowserImportResult { Created = created, Updated = updated, IdMappings = idMappings });
    }
}
