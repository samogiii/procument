using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// DP (DasturPardakht) PDF.
/// Auto-generated when the Import Details (Bank Detail) form is saved on a PO.
/// Contains the items table (Part Number, Qty, PO Supplier, Quote Price, PO Price, PO Total)
/// and the supplier's bank detail block at the bottom.
/// </summary>
public static class DpDocument
{
    public static byte[] Generate(DpPdfRequest req)
    {
        var primary = req.PrimaryColor ?? "#312e81";
        var accent  = req.AccentColor  ?? "#6366f1";
        var sym     = req.CurrencySymbol ?? "$";

        var doc = Document.Create(container =>
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
                    // Header
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
                            if (!string.IsNullOrEmpty(req.CompanyEmail))
                                left.Item().Text(t => t.Span($"Email: {req.CompanyEmail}").FontSize(8).FontColor(Colors.Grey.Medium));
                        });

                        row.RelativeItem(2).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text(t => t.Span("DASTURPARDAKHT").Bold().FontSize(20).FontColor(primary));
                            right.Item().AlignRight().PaddingTop(4).Text(t => t.Span(req.PoNumber ?? "").FontSize(10).FontColor(Colors.Grey.Medium));
                            if (!string.IsNullOrEmpty(req.DocumentDate))
                                right.Item().AlignRight().Text(t => t.Span($"Date: {req.DocumentDate}").FontSize(9).FontColor(Colors.Grey.Medium));
                        });
                    });

                    // Accent line
                    col.Item().PaddingVertical(8).Row(row =>
                    {
                        row.RelativeItem(4).Height(3).Background(primary);
                        row.RelativeItem(3).Height(3).Background(accent);
                        row.RelativeItem(3).Height(3).Background(Colors.Grey.Lighten3);
                    });

                    // Supplier / PO meta row
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        void Meta(string label, string? val)
                            => row.RelativeItem().AlignLeft().Text(t =>
                            {
                                t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                t.Span(val ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        Meta("PO", req.PoNumber);
                        Meta("Supplier", req.SupplierName);
                        Meta("Currency", req.Currency);
                    });

                    // Items table
                    col.Item().PaddingBottom(14).Element(c => ComposeItemsTable(c, req, primary, sym));

                    // Totals
                    col.Item().PaddingBottom(14).AlignRight().Width(220).Element(c => ComposeTotals(c, req, primary, sym));

                    // Company Preset Instruction
                    if (!string.IsNullOrEmpty(req.CompanyPresetName))
                    {
                        col.Item().PaddingBottom(10).Text(t =>
                        {
                            t.Span("Note: ").Bold().FontSize(10).FontColor(accent);
                            t.Span($"This DP must Pay with this Company Preset: ").FontSize(10).FontColor(primary);
                            t.Span(req.CompanyPresetName).Bold().FontSize(10).FontColor(primary);
                        });
                    }

                    // Bank Detail block
                    col.Item().Element(c => ComposeBankBlock(c, req, primary, accent));
                });

                page.Footer().BorderTop(2).BorderColor(primary).PaddingVertical(8).Row(row =>
                {
                    row.RelativeItem().Text(t => t.Span(req.FooterText ?? "").FontSize(8).FontColor(Colors.Grey.Medium));
                    row.RelativeItem().AlignRight().Text(t => t.Span(req.CompanyEmail ?? "").FontSize(8).Bold().FontColor(primary));
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeItemsTable(IContainer container, DpPdfRequest req, string primary, string sym)
    {
        var items = req.Items ?? new List<DpPdfItem>();

        container.Column(outer =>
        {
            // Header row
            outer.Item().Row(hr =>
            {
                string[] headers = ["#", "Part Number", "Qty", "PO Supplier", "Quote Price", "PO Price", "PO Total"];
                float[] widths   = [22,   0,             40,    0,             70,            70,         80];
                float[] rels     = [0,    2f,            0,     2f,            0,             0,          0];

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

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var idx = i;
                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                outer.Item().Row(r =>
                {
                    void Cell(IContainer cell, string text, string? color = null, bool bold = false)
                        => cell.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                            .Padding(5).AlignCenter().AlignMiddle().Text(t =>
                            {
                                var s = t.Span(text).FontSize(8.5f);
                                if (color != null) s.FontColor(color);
                                if (bold) s.Bold();
                            });

                    Cell(r.ConstantItem(22), (idx + 1).ToString(), Colors.Grey.Darken1);
                    Cell(r.RelativeItem(2f), it.PartNumber ?? "—", primary, bold: true);
                    Cell(r.ConstantItem(40), it.Qty.ToString(), primary, bold: true);
                    Cell(r.RelativeItem(2f), it.PoSupplier ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(70), it.QuotePrice.HasValue ? $"{sym}{FormatPrice(it.QuotePrice.Value)}" : "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(70), $"{sym}{FormatPrice(it.PoPrice)}", primary);
                    Cell(r.ConstantItem(80), $"{sym}{FormatPrice(it.PoTotal)}", primary, bold: true);
                });
            }
        });
    }

    private static void ComposeTotals(IContainer container, DpPdfRequest req, string primary, string sym)
    {
        var grand = req.GrandTotal ?? (req.Items?.Sum(i => i.PoTotal) ?? 0m);

        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(col =>
        {
            col.Item().Background(primary).Padding(8).Row(r =>
            {
                r.RelativeItem().Text(t => t.Span("TOTAL").Bold().FontSize(10).FontColor(Colors.White));
                r.RelativeItem().AlignRight().Text(t => t.Span($"{sym}{FormatPrice(grand)}").Bold().FontSize(12).FontColor(Colors.White));
            });
        });
    }

    private static void ComposeBankBlock(IContainer container, DpPdfRequest req, string primary, string accent)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(col =>
        {
            col.Item().Text(t => t.Span("BANK DETAILS").Bold().FontSize(10).FontColor(accent).LetterSpacing(0.1f));
            col.Item().PaddingTop(8).Row(row =>
            {
                void Field(IContainer cell, string label, string? val)
                {
                    cell.Column(c =>
                    {
                        c.Item().Text(t => t.Span(label).Bold().FontSize(8).FontColor(Colors.Grey.Darken1));
                        c.Item().PaddingTop(2).Text(t => t.Span(string.IsNullOrWhiteSpace(val) ? "—" : val).FontSize(9).FontColor(primary));
                    });
                }
                Field(row.RelativeItem(), "Bank Name", req.BankName);
                Field(row.RelativeItem(), "Account Number", req.BankAccountNumber);
                Field(row.RelativeItem(), "Bank City", req.BankCity);
            });
            col.Item().PaddingTop(10).Row(row =>
            {
                void Field(IContainer cell, string label, string? val)
                {
                    cell.Column(c =>
                    {
                        c.Item().Text(t => t.Span(label).Bold().FontSize(8).FontColor(Colors.Grey.Darken1));
                        c.Item().PaddingTop(2).Text(t => t.Span(string.IsNullOrWhiteSpace(val) ? "—" : val).FontSize(9).FontColor(primary));
                    });
                }
                Field(row.RelativeItem(), "Bank Country", req.BankCountry);
                Field(row.RelativeItem(), "Bank Address", req.BankAddress);
                Field(row.RelativeItem(), "Swift / Code", req.SwiftCode);
            });
            if (!string.IsNullOrWhiteSpace(req.Notes))
            {
                col.Item().PaddingTop(10).Text(t =>
                {
                    t.Span("Notes: ").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(req.Notes).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private static string FormatPrice(decimal value) => value.ToString("#,##0.00");
}
