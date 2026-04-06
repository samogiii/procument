using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PdfController : ControllerBase
{
    [HttpPost("generate")]
    public IActionResult Generate([FromBody] QuotePdfRequest req)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var primary = req.PrimaryColor ?? "#1a2744";
        var accent = req.AccentColor ?? "#2563eb";
        var sym = req.CurrencySymbol ?? "$";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(20);
                page.MarginHorizontal(30);
                page.MarginBottom(10);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(primary));

                page.Header().Element(h => ComposeHeader(h, req, primary, accent));
                page.Content().Element(c => ComposeContent(c, req, primary, accent, sym));
                page.Footer().Element(f => ComposeFooter(f, req, primary));
            });
        });

        var pdf = document.GeneratePdf();
        return File(pdf, "application/pdf", $"{req.QuoteNumber ?? "Quote"}.pdf");
    }

    // ───────────────────── HEADER ─────────────────────
    private static void ComposeHeader(IContainer container, QuotePdfRequest req, string primary, string accent)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem(3).Column(left =>
                {
                    if (!string.IsNullOrEmpty(req.LogoBase64))
                    {
                        try
                        {
                            var raw = req.LogoBase64;
                            if (raw.Contains(",")) raw = raw[(raw.IndexOf(",") + 1)..];
                            var bytes = Convert.FromBase64String(raw);
                            left.Item().Height(60).Image(bytes, ImageScaling.FitHeight);
                        }
                        catch { }
                    }
                    left.Item().PaddingTop(4).Text(t => t.Span(req.CompanyName ?? "").Bold().FontSize(16).FontColor(primary));
                    if (!string.IsNullOrEmpty(req.CompanyLocation))
                        left.Item().Text(t => t.Span($"Add: {req.CompanyLocation}").FontSize(8).FontColor(Colors.Grey.Medium));
                    if (!string.IsNullOrEmpty(req.CompanyPhone))
                        left.Item().Text(t => t.Span($"Tel: {req.CompanyPhone}").FontSize(8).FontColor(Colors.Grey.Medium));
                    if (!string.IsNullOrEmpty(req.CompanyWebsite))
                        left.Item().Text(t => t.Span($"Website: {req.CompanyWebsite}").FontSize(8).FontColor(Colors.Grey.Medium));
                    if (!string.IsNullOrEmpty(req.CompanyEmail))
                        left.Item().Text(t => t.Span($"Email: {req.CompanyEmail}").FontSize(8).FontColor(Colors.Grey.Medium));
                });

                row.RelativeItem(2).AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text(t => t.Span("QUOTATION").Bold().FontSize(20).FontColor(primary));
                    right.Item().AlignRight().PaddingTop(4).Text(t => t.Span(req.QuoteNumber ?? "").FontSize(10).FontColor(Colors.Grey.Medium));
                });
            });

            // Accent gradient line
            col.Item().PaddingVertical(8).Row(row =>
            {
                row.RelativeItem(4).Height(3).Background(primary);
                row.RelativeItem(3).Height(3).Background(accent);
                row.RelativeItem(3).Height(3).Background(Colors.Grey.Lighten3);
            });
        });
    }

    // ───────────────────── CONTENT ─────────────────────
    private static void ComposeContent(IContainer container, QuotePdfRequest req, string primary, string accent, string sym)
    {
        container.Column(col =>
        {
            // Meta row
            col.Item().PaddingBottom(10).Row(row =>
            {
                void Meta(string label, string? value)
                {
                    row.RelativeItem().Text(t =>
                    {
                        t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                        t.Span(value ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                }
                Meta("Date", req.QuoteDate);
                Meta("Valid Until", req.ValidUntil);
                Meta("RFQ", req.RfqName);
                Meta("Currency", req.Currency);
            });

            // Bill To / Ship To
            col.Item().PaddingBottom(12).Row(row =>
            {
                void AddressBox(string title, string? name, string? address)
                {
                    row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                    {
                        c.Item().Text(t => t.Span(title).Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
                        c.Item().PaddingTop(4).Text(t => t.Span(name ?? "—").Bold().FontSize(10).FontColor(primary));
                        if (!string.IsNullOrEmpty(address))
                            c.Item().PaddingTop(2).Text(t => t.Span(address).FontSize(9).FontColor(Colors.Grey.Darken1));
                    });
                }
                AddressBox("BILL TO", req.CustomerName, req.CustomerBillTo);
                AddressBox("SHIP TO", req.CustomerName, req.CustomerShipTo ?? req.CustomerBillTo);
            });

            // Items table
            col.Item().PaddingBottom(10).Element(e => ComposeItemsTable(e, req, primary, accent, sym));

            // Totals
            col.Item().PaddingBottom(10).AlignRight().Width(220).Element(e => ComposeTotals(e, req, primary, sym));

            // Comments
            if (!string.IsNullOrEmpty(req.Comments))
            {
                col.Item().PaddingBottom(8).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text(t => t.Span("COMMENTS").Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
                    c.Item().PaddingTop(4).Text(t => t.Span(req.Comments).FontSize(9).FontColor(Colors.Grey.Darken1));
                });
            }

            // Terms
            if (!string.IsNullOrEmpty(req.Terms))
            {
                col.Item().PaddingBottom(8).Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                {
                    c.Item().Text(t => t.Span("TERMS & CONDITIONS").Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
                    c.Item().PaddingTop(4).Text(t => t.Span(req.Terms).FontSize(8).FontColor(Colors.Grey.Darken1));
                });
            }
        });
    }

    // ───────────────────── ITEMS TABLE ─────────────────────
    private static void ComposeItemsTable(IContainer container, QuotePdfRequest req, string primary, string accent, string sym)
    {
        var items = req.Items ?? [];

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(30);   // Ref
                c.ConstantColumn(22);   // #
                c.RelativeColumn(3);    // Part Number
                c.RelativeColumn(2.5f); // Description
                c.ConstantColumn(30);   // Qty
                c.ConstantColumn(30);   // Cond
                c.ConstantColumn(55);   // Lead Time
                c.ConstantColumn(60);   // Unit Price
                c.ConstantColumn(65);   // Total
            });

            // Header
            string[] headers = ["Ref", "#", "Part No.", "Description", "Qty", "CD", "Lead Time", "Unit Price", "Total"];
            for (int h = 0; h < headers.Length; h++)
            {
                var align = h >= 7 ? TextHorizontalAlignment.Right : (h >= 4 && h <= 6 ? TextHorizontalAlignment.Center : TextHorizontalAlignment.Left);
                table.Cell().Row(1).Column((uint)(h + 1))
                    .Background(primary).Padding(6)
                    .AlignCenter().Text(t => t.Span(headers[h]).FontSize(7.5f).Bold().FontColor(Colors.White));
            }

            // Rows
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var rowNum = (uint)(i + 2);
                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                var isAlt = !string.IsNullOrEmpty(it.Alt);
                var displayPN = isAlt ? it.Alt! : (it.PartNumberName ?? "—");
                var pnColor = isAlt ? accent : primary;

                void Cell(uint colNum, string text, string? color = null, bool bold = false, TextHorizontalAlignment align = TextHorizontalAlignment.Left)
                {
                    table.Cell().Row(rowNum).Column(colNum).Background(bg)
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .Padding(5).AlignLeft().Text(t =>
                        {
                            var s = t.Span(text).FontSize(8.5f);
                            if (color != null) s.FontColor(color);
                            if (bold) s.Bold();
                        });
                }

                Cell(1, it.RfqReference ?? "—", Colors.Grey.Medium);
                Cell(2, (i + 1).ToString(), Colors.Grey.Darken1);
                // Part number cell with alt note
                table.Cell().Row(rowNum).Column(3).Background(bg)
                    .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                    .Padding(5).Column(c =>
                    {
                        c.Item().Text(t => t.Span(displayPN).FontSize(8.5f).Bold().FontColor(pnColor));
                        if (isAlt)
                            c.Item().Text(t => t.Span($"(Req: {it.PartNumberName})").FontSize(7).FontColor(Colors.Grey.Medium));
                    });
                Cell(4, it.Description ?? "—", Colors.Grey.Darken1);
                Cell(5, it.Qty.ToString(), primary, bold: true);
                Cell(6, it.Condition ?? "—", primary);
                Cell(7, it.LeadTime ?? "—", Colors.Grey.Darken1);
                Cell(8, $"{sym}{FormatPrice(it.UnitPrice * (req.ExchangeRate ?? 1))}", primary);
                Cell(9, $"{sym}{FormatPrice(it.TotalPrice * (req.ExchangeRate ?? 1))}", primary, bold: true);
            }
        });
    }

    // ───────────────────── TOTALS ─────────────────────
    private static void ComposeTotals(IContainer container, QuotePdfRequest req, string primary, string sym)
    {
        var rate = req.ExchangeRate ?? 1;
        var subtotal = (req.Subtotal ?? 0) * rate;
        var tax = (req.Tax ?? 0) * rate;
        var shipping = (req.Shipping ?? 0) * rate;
        var other = (req.Other ?? 0) * rate;
        var grandTotal = subtotal + tax + shipping + other;

        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(col =>
        {
            void TotalRow(string label, decimal amount, string? bg = null, bool isGrand = false)
            {
                var row = col.Item();
                if (bg != null) row = col.Item().Background(bg);
                if (isGrand) row = col.Item().Background(primary);

                row.Padding(6).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        var s = t.Span(label).FontSize(9).FontColor(isGrand ? Colors.White : Colors.Grey.Darken1);
                        if (isGrand) s.Bold();
                    });
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        var s = t.Span($"{sym}{FormatPrice(amount)}").FontSize(isGrand ? 12 : 9).FontColor(isGrand ? Colors.White : primary);
                        if (isGrand) s.Bold();
                    });
                });
            }

            TotalRow("Subtotal", subtotal, Colors.Grey.Lighten5);
            TotalRow("Tax", tax);
            TotalRow("Shipping", shipping, Colors.Grey.Lighten5);
            TotalRow("Other", other);
            TotalRow("Total", grandTotal, isGrand: true);
        });
    }

    // ───────────────────── FOOTER ─────────────────────
    private static void ComposeFooter(IContainer container, QuotePdfRequest req, string primary)
    {
        container.BorderTop(2).BorderColor(primary).PaddingVertical(8).Row(row =>
        {
            row.RelativeItem().Text(t => t.Span(req.FooterText ?? "").FontSize(8).FontColor(Colors.Grey.Medium));
            row.RelativeItem().AlignRight().Text(t => t.Span(req.CompanyEmail ?? "").FontSize(8).Bold().FontColor(primary));
        });
    }

    private static string FormatPrice(decimal value) => value.ToString("#,##0.00");
}

// ───────────────────── REQUEST DTOs ─────────────────────
public class QuotePdfRequest
{
    // Company
    public string? CompanyName { get; set; }
    public string? CompanyLocation { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanyEmail { get; set; }
    public string? LogoBase64 { get; set; }

    // Theme
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }

    // Quote meta
    public string? QuoteNumber { get; set; }
    public string? QuoteDate { get; set; }
    public string? ValidUntil { get; set; }
    public string? RfqName { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }
    public decimal? ExchangeRate { get; set; }

    // Customer
    public string? CustomerName { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerShipTo { get; set; }

    // Items
    public List<QuotePdfItem>? Items { get; set; }

    // Totals
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? Other { get; set; }

    // Text
    public string? Comments { get; set; }
    public string? Terms { get; set; }
    public string? FooterText { get; set; }
}

public class QuotePdfItem
{
    public string? RfqReference { get; set; }
    public string? PartNumberName { get; set; }
    public string? Alt { get; set; }
    public string? Description { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }
    public string? LeadTime { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? CertName { get; set; }
    public string? TagDate { get; set; }
    public string? Note { get; set; }
}
