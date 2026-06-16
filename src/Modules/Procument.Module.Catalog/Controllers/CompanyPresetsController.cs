using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.Catalog.Controllers;

public record CompanyPresetBankAccountDto(long Id, string AccountName, string? BankName, string? BankAddress, string? AccountNumber, string? BeneficiaryName, string? SwiftCode, int SortOrder);
public record UpsertBankAccountRequest(string AccountName, string? BankName, string? BankAddress, string? AccountNumber, string? BeneficiaryName, string? SwiftCode, int SortOrder = 0);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class CompanyPresetsController : ControllerBase
{
    private readonly DbContext _db;
    public CompanyPresetsController(DbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult> GetAll()
    {
        // Auto-seed defaults if table is empty
        if (!await _db.Set<CompanyPreset>().AnyAsync())
        {
            var defaults = new List<CompanyPreset>
            {
                new() {
                    Name = "Didit Developments Ltd",
                    Location = "Viglen House Business Centre, Alpterton Lane, Wemb, London HA0 1HD, United Kingdom",
                    Phone = "+44 7722223340",
                    Website = "www.diditsolution.com",
                    Email = "sales@diditsolution.com",
                    TermsAndConditions = "",
                    SortOrder = 1,
                },
                new() {
                    Name = "Synair (HK) Supply Limited",
                    Location = "Flat/Rm D9 8/F Universal Industrial Centre, 19-25 Shan Mei Street, Shatin NT, Hong Kong",
                    Phone = "+86-18075746425",
                    Website = "www.SYNAIR.aero",
                    Email = "sales@synair.com",
                    TermsAndConditions = "PAYMENT METHOD: PREPAYMENT\nDELIVERY TERM: EXW VENDOR\nALL QUOTES ARE VALID FOR 7 DAYS, SUBJECT TO PRIOR SALE.",
                    SortOrder = 2,
                },
                new() {
                    Name = "Horizon Aviation Trading LLC - SPC",
                    Location = "Horizon Group of Companies 1303, 13th Floor, 3 Sails Tower Corniche St, Abu Dhabi, UAE",
                    Phone = "+971 505141719",
                    Website = "https://horizon-avex.com/",
                    Email = "sales@horizon-avex.com",
                    TermsAndConditions = "PAYMENT METHOD: NET 30\nDELIVERY TERM: CPT Dubai\nALL QUOTES ARE VALID FOR 7 DAYS, SUBJECT TO PRIOR SALE.",
                    SortOrder = 4,
                },
                new() {
                    Name = "Shenzhen Yunleifei Technology Co., LTD",
                    Location = "No. 416, Building B, Guancheng Community, Guanhu Street, Longhua District, Shenzhen, China",
                    Phone = "+86 13879958620",
                    Website = "",
                    Email = "aviation@yunleifei.com",
                    TermsAndConditions = "Payment Method: Prepayment\nDelivery Terms: EXW Hong Kong\nLead Time: Average delivery to HK is 1 week from vendor stock.\nShipping: Final shipment from HK to your destination can be arranged upon request using your provided shipping account.",
                    SortOrder = 5,
                },
            };
            _db.Set<CompanyPreset>().AddRange(defaults);
            await _db.SaveChangesAsync();
        }

        var items = await _db.Set<CompanyPreset>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Location,
                p.Phone,
                p.Website,
                p.Email,
                p.TermsAndConditions,
                p.LogoBase64,
                p.ShipToAddress,
                p.ShipToPhone,
                p.LogoMimeType,
                p.FedexAccount,
                p.BankName,
                p.BankAddress,
                p.AccountNumber,
                p.BeneficiaryName,
                p.SwiftCode,
                p.SortOrder,
                p.IsActive,
                p.CreatedAt,
                p.ModifyAt,
                p.PrimaryColor,
                p.AccentColor,
                p.CustomPdfHtml,
                BankAccounts = p.BankAccounts
                    .OrderBy(b => b.SortOrder).ThenBy(b => b.AccountName)
                    .Select(b => new { b.Id, b.AccountName, b.BankName, b.BankAddress, b.AccountNumber, b.BeneficiaryName, b.SwiftCode, b.SortOrder })
                    .ToList(),
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(long id)
    {
        var item = await _db.Set<CompanyPreset>().FindAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> Create([FromBody] CompanyPresetDto dto)
    {
        var preset = new CompanyPreset
        {
            Name = dto.Name,
            Location = dto.Location,
            Phone = dto.Phone,
            Website = dto.Website,
            Email = dto.Email,
            TermsAndConditions = dto.TermsAndConditions,
            LogoBase64 = dto.LogoBase64,
            LogoMimeType = dto.LogoMimeType,
            SortOrder = dto.SortOrder,
            ShipToAddress = dto.ShipToAddress,
            ShipToPhone = dto.ShipToPhone,
            FedexAccount = dto.FedexAccount,
            BankName = dto.BankName,
            BankAddress = dto.BankAddress,
            AccountNumber = dto.AccountNumber,
            BeneficiaryName = dto.BeneficiaryName,
            SwiftCode = dto.SwiftCode,
            PrimaryColor = dto.PrimaryColor ?? "#1a2744",
            AccentColor = dto.AccentColor ?? "#2563eb",
            CustomPdfHtml = dto.CustomPdfHtml,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<CompanyPreset>().Add(preset);
        await _db.SaveChangesAsync();
        return Ok(new { preset.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> Update(long id, [FromBody] CompanyPresetDto dto)
    {
        var preset = await _db.Set<CompanyPreset>().FindAsync(id);
        if (preset == null) return NotFound();

        preset.Name = dto.Name;
        preset.Location = dto.Location;
        preset.Phone = dto.Phone;
        preset.Website = dto.Website;
        preset.ShipToAddress = dto.ShipToAddress;
        preset.ShipToPhone = dto.ShipToPhone;
        preset.Email = dto.Email;
        preset.TermsAndConditions = dto.TermsAndConditions;
        preset.SortOrder = dto.SortOrder;
        preset.PrimaryColor = dto.PrimaryColor ?? preset.PrimaryColor;
        preset.AccentColor = dto.AccentColor ?? preset.AccentColor;
        preset.CustomPdfHtml = dto.CustomPdfHtml;
        preset.FedexAccount = dto.FedexAccount;
        preset.BankName = dto.BankName;
        preset.BankAddress = dto.BankAddress;
        preset.AccountNumber = dto.AccountNumber;
        preset.BeneficiaryName = dto.BeneficiaryName;
        preset.SwiftCode = dto.SwiftCode;
        preset.ModifyAt = DateTime.UtcNow;

        // Only update logo if provided
        if (dto.LogoBase64 != null)
        {
            preset.LogoBase64 = dto.LogoBase64;
            preset.LogoMimeType = dto.LogoMimeType;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> Delete(long id)
    {
        var preset = await _db.Set<CompanyPreset>().FindAsync(id);
        if (preset == null) return NotFound();
        preset.IsActive = false;
        preset.ModifyAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── Bank Accounts ──────────────────────────────────────────────────────────

    [HttpGet("{presetId}/bank-accounts")]
    public async Task<ActionResult> GetBankAccounts(long presetId)
    {
        var accounts = await _db.Set<CompanyPresetBankAccount>()
            .Where(b => b.CompanyPresetId == presetId)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.AccountName)
            .Select(b => new CompanyPresetBankAccountDto(b.Id, b.AccountName, b.BankName, b.BankAddress, b.AccountNumber, b.BeneficiaryName, b.SwiftCode, b.SortOrder))
            .ToListAsync();
        return Ok(accounts);
    }

    [HttpPost("{presetId}/bank-accounts")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> CreateBankAccount(long presetId, [FromBody] UpsertBankAccountRequest req)
    {
        var presetExists = await _db.Set<CompanyPreset>().AnyAsync(p => p.Id == presetId && p.IsActive);
        if (!presetExists) return NotFound();

        var account = new CompanyPresetBankAccount
        {
            CompanyPresetId = presetId,
            AccountName = req.AccountName,
            BankName = req.BankName,
            BankAddress = req.BankAddress,
            AccountNumber = req.AccountNumber,
            BeneficiaryName = req.BeneficiaryName,
            SwiftCode = req.SwiftCode,
            SortOrder = req.SortOrder,
        };
        _db.Set<CompanyPresetBankAccount>().Add(account);
        await _db.SaveChangesAsync();
        return Ok(new CompanyPresetBankAccountDto(account.Id, account.AccountName, account.BankName, account.BankAddress, account.AccountNumber, account.BeneficiaryName, account.SwiftCode, account.SortOrder));
    }

    [HttpPut("{presetId}/bank-accounts/{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> UpdateBankAccount(long presetId, long id, [FromBody] UpsertBankAccountRequest req)
    {
        var account = await _db.Set<CompanyPresetBankAccount>()
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyPresetId == presetId);
        if (account == null) return NotFound();

        account.AccountName = req.AccountName;
        account.BankName = req.BankName;
        account.BankAddress = req.BankAddress;
        account.AccountNumber = req.AccountNumber;
        account.BeneficiaryName = req.BeneficiaryName;
        account.SwiftCode = req.SwiftCode;
        account.SortOrder = req.SortOrder;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{presetId}/bank-accounts/{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> DeleteBankAccount(long presetId, long id)
    {
        var account = await _db.Set<CompanyPresetBankAccount>()
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyPresetId == presetId);
        if (account == null) return NotFound();
        _db.Set<CompanyPresetBankAccount>().Remove(account);
        await _db.SaveChangesAsync();
        return Ok();
    }

}

public class CompanyPresetDto
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? Email { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? LogoBase64 { get; set; }
    public string? LogoMimeType { get; set; }
    public int SortOrder { get; set; } = 0;
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? CustomPdfHtml { get; set; }
    public string? ShipToAddress { get; set; }
    public string? FedexAccount { get; set; }
    public string? ShipToPhone { get; set; }
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? AccountNumber { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? SwiftCode { get; set; }
}
