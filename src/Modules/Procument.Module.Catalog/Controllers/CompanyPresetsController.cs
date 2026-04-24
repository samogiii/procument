using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.Catalog.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class CompanyPresetsController : ControllerBase
{
    private readonly DbContext _db;
    public CompanyPresetsController(DbContext db) => _db = db;

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
                p.LogoMimeType,
                p.SortOrder,
                p.IsActive,
                p.CreatedAt,
                p.ModifyAt,
                p.PrimaryColor,
                p.AccentColor,
                p.CustomPdfHtml,
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
        preset.Email = dto.Email;
        preset.TermsAndConditions = dto.TermsAndConditions;
        preset.SortOrder = dto.SortOrder;
        preset.PrimaryColor = dto.PrimaryColor ?? preset.PrimaryColor;
        preset.AccentColor = dto.AccentColor ?? preset.AccentColor;
        preset.CustomPdfHtml = dto.CustomPdfHtml;
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
}
