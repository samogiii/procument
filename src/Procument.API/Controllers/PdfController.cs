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

                page.Content().Column(col =>
                {
                    col.Item().Element(h => ComposeHeader(h, req, primary, accent));
                    col.Item().Element(c => ComposeContent(c, req, primary, accent, sym));
                });
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
                    row.RelativeItem().AlignCenter().Text(t =>
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
                    row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text(t => t.Span(title).Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
                        c.Item().AlignCenter().PaddingTop(4).Text(t => t.Span(name ?? "—").Bold().FontSize(10).FontColor(primary));
                        if (!string.IsNullOrEmpty(address))
                            c.Item().AlignCenter().PaddingTop(2).Text(t => t.Span(address).FontSize(9).FontColor(Colors.Grey.Darken1));
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

        container.Column(outer =>
        {
            // Header row
            outer.Item().Row(hr =>
            {
                string[] headers = ["#", "Ref", "Part No.", "Description", "Qty", "CD", "Lead Time", "Unit Price", "Total"];
                float[] widths = [22, 30, 0, 0, 30, 30, 55, 60, 65];
                float[] rels = [0, 0, 3, 2.5f, 0, 0, 0, 0, 0];

                for (int h = 0; h < headers.Length; h++)
                {
                    var label = headers[h];
                    var w = widths[h];
                    var rel = rels[h];
                    var cell = w > 0 ? (IContainer)hr.ConstantItem(w) : hr.RelativeItem(rel);
                    cell.Background(primary).Padding(6)
                        .AlignCenter().Text(t => t.Span(label).FontSize(7.5f).Bold().FontColor(Colors.White));
                }
            });

            // Item rows — each item + its detail kept together via ShowEntire
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var idx = i;
                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                var isAlt = !string.IsNullOrEmpty(it.Alt);
                var displayPN = isAlt ? it.Alt! : (it.PartNumberName ?? "—");
                var pnColor = isAlt ? accent : primary;
                var hasDetails = !string.IsNullOrEmpty(it.CertName) || !string.IsNullOrEmpty(it.TagDate) || !string.IsNullOrEmpty(it.Note);

                outer.Item().ShowEntire().Column(group =>
                {
                    // Main data row
                    group.Item().Row(r =>
                    {
                        void Cell(IContainer cell, string text, string? color = null, bool bold = false)
                        {
                            cell.Background(bg)
                                .BorderBottom(hasDetails ? 0f : 0.5f).BorderColor(Colors.Grey.Lighten3)
                                .Padding(5).AlignCenter().AlignMiddle().Text(t =>
                                {
                                    var s = t.Span(text).FontSize(8.5f);
                                    if (color != null) s.FontColor(color);
                                    if (bold) s.Bold();
                                });
                        }

                        Cell(r.ConstantItem(22), (idx + 1).ToString(), Colors.Grey.Darken1);
                        Cell(r.ConstantItem(30), it.RfqReference ?? "—", Colors.Grey.Medium);

                        // Part number cell
                        r.RelativeItem(3).Background(bg)
                            .BorderBottom(hasDetails ? 0f : 0.5f).BorderColor(Colors.Grey.Lighten3)
                            .Padding(5).AlignCenter().AlignMiddle().Column(c =>
                            {
                                c.Item().Text(t => t.Span(displayPN).FontSize(8.5f).Bold().FontColor(pnColor));
                                if (isAlt)
                                    c.Item().Text(t => t.Span($"(Alt to: {it.PartNumberName})").FontSize(7).FontColor(Colors.Grey.Medium));
                            });

                        Cell(r.RelativeItem(2.5f), it.Description ?? "—", Colors.Grey.Darken1);
                        Cell(r.ConstantItem(30), it.Qty.ToString(), primary, bold: true);
                        Cell(r.ConstantItem(30), it.Condition ?? "—", primary);
                        Cell(r.ConstantItem(55), it.LeadTime ?? "—", Colors.Grey.Darken1);
                        Cell(r.ConstantItem(60), $"{sym}{FormatPrice(it.UnitPrice * (req.ExchangeRate ?? 1))}", primary);
                        Cell(r.ConstantItem(65), $"{sym}{FormatPrice(it.TotalPrice * (req.ExchangeRate ?? 1))}", primary, bold: true);
                    });

                    // Detail sub-row: Cert, Tag Date, Note
                    if (hasDetails)
                    {
                        group.Item().Background(bg)
                            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                            .PaddingVertical(2).PaddingBottom(6)
                            .Text(t =>
                            {
                                if (!string.IsNullOrEmpty(it.CertName))
                                {
                                    t.Span("Cert: ").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                                    t.Span(it.CertName + "   ").FontSize(8).FontColor(Colors.Grey.Darken1);
                                }
                                if (!string.IsNullOrEmpty(it.TagDate))
                                {
                                    t.Span("Tag Date: ").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                                    var tagYear = it.TagDate.Length >= 4 ? it.TagDate[..4] : it.TagDate;
                                    t.Span(tagYear + "   ").FontSize(8).FontColor(Colors.Grey.Darken1);
                                }
                                if (!string.IsNullOrEmpty(it.Note))
                                {
                                    t.Span("Note: ").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                                    t.Span(it.Note).FontSize(8).FontColor(Colors.Grey.Darken1);
                                }
                            });
                    }
                });
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
