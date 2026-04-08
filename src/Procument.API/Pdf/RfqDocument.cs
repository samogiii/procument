using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// RFQ PDF.
/// Colors come from the selected Company Preset (PrimaryColor / AccentColor).
/// Minimal layout: no address boxes, 5-column items table with alternatives sub-rows.
/// </summary>
public static class RfqDocument
{
    public static byte[] Generate(RfqPdfRequest req)
    {
        var primary = req.PrimaryColor ?? "#1e293b";
        var accent  = req.AccentColor  ?? "#0891b2";

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
                    // Header: company text (left) + "RFQ" + ID (right)
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(3).Column(left =>
                        {
                            if (!string.IsNullOrEmpty(req.LogoBase64))
                            {
                                try
                                {
                                    var raw = req.LogoBase64;
                                    if (raw.Contains(',')) raw = raw[(raw.IndexOf(',') + 1)..];
                                    var bytes = Convert.FromBase64String(raw);
                                    left.Item().Height(52).Image(bytes, ImageScaling.FitHeight);
                                }
                                catch { }
                            }
                            left.Item().PaddingTop(4).Text(t =>
                                t.Span(req.HeaderText ?? "").Bold().FontSize(16).FontColor(primary));
                        });

                        row.RelativeItem(2).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight()
                                .Text(t => t.Span("REQUEST FOR QUOTATION").Bold().FontSize(16).FontColor(primary));
                            right.Item().AlignRight().PaddingTop(4)
                                .Text(t => t.Span(req.RfqName ?? (req.RfqId.HasValue ? $"#{req.RfqId}" : ""))
                                    .FontSize(10).FontColor(Colors.Grey.Medium));
                        });
                    });

                    // Accent line
                    col.Item().Element(c => PdfHelpers.DrawAccentLine(c, primary, accent));

                    // Info block: RFQ Details | Dates
                    col.Item().PaddingVertical(10).Row(row =>
                    {
                        row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                        {
                            c.Item().Text(t => t.Span("RFQ DETAILS")
                                .Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
                            c.Item().PaddingTop(6).Text(t =>
                            {
                                t.Span("Name: ").Bold().FontSize(9).FontColor(primary);
                                t.Span(req.RfqName ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                            c.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("Items: ").Bold().FontSize(9).FontColor(primary);
                                var cnt = req.Items?.Count ?? 0;
                                t.Span($"{cnt} part{(cnt != 1 ? "s" : "")}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        });

                        row.ConstantItem(8);

                        row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                        {
                            c.Item().Text(t => t.Span("IMPORTANT DATES")
                                .Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
                            c.Item().PaddingTop(6).Text(t =>
                            {
                                t.Span("Date: ").Bold().FontSize(9).FontColor(primary);
                                t.Span(req.RfqDate ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });

                    // Items table (5 columns + alternatives sub-rows)
                    col.Item().PaddingBottom(10).Element(c => ComposeItemsTable(c, req, primary, accent));

                    // Terms & Conditions
                    col.Item().PaddingBottom(8).Element(c => PdfHelpers.DrawTerms(c, req.Terms, accent));
                });

                // Footer
                page.Footer().BorderTop(2).BorderColor(primary).PaddingVertical(8).Row(row =>
                {
                    row.RelativeItem().Text(t =>
                        t.Span(req.FooterText ?? "Terms and conditions apply.")
                            .FontSize(8).FontColor(Colors.Grey.Medium));
                    row.RelativeItem().AlignRight().Text(t =>
                        t.Span("Page 1").FontSize(8).FontColor(Colors.Grey.Medium));
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeItemsTable(IContainer container, RfqPdfRequest req,
        string primary, string accent)
    {
        var items = req.Items ?? [];

        container.Column(outer =>
        {
            // Header — 5 columns
            outer.Item().Row(hr =>
            {
                string[] headers = ["#", "Part Number", "Description", "Qty", "Condition"];
                float[] widths   = [24,  0,              0,             30,    70];
                float[] rels     = [0,   2.5f,           3f,            0,     0];

                for (int h = 0; h < headers.Length; h++)
                {
                    var label = headers[h];
                    var w = widths[h];
                    var rel = rels[h];
                    var cell = w > 0 ? (IContainer)hr.ConstantItem(w) : hr.RelativeItem(rel);
                    cell.Background(primary).Padding(7)
                        .AlignCenter().Text(t => t.Span(label).FontSize(8f).Bold().FontColor(Colors.White));
                }
            });

            // Data rows + alternatives sub-row
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var idx = i;
                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                var hasSubrow = !string.IsNullOrWhiteSpace(it.Remark)
                             || (it.Alternatives != null && it.Alternatives.Count > 0);

                outer.Item().ShowEntire().Column(group =>
                {
                    // Main row
                    group.Item().Row(r =>
                    {
                        void Cell(IContainer cell, string text, string? color = null, bool bold = false, bool mono = false)
                            => cell.Background(bg)
                                .BorderBottom(hasSubrow ? 0f : 0.5f).BorderColor(Colors.Grey.Lighten3)
                                .Padding(6).AlignCenter().AlignMiddle().Text(t =>
                                {
                                    var s = t.Span(text).FontSize(9f);
                                    if (color != null) s.FontColor(color);
                                    if (bold) s.Bold();
                                    if (mono) s.FontFamily("Courier New");
                                });

                        Cell(r.ConstantItem(24), (idx + 1).ToString(), Colors.Grey.Darken1);
                        Cell(r.RelativeItem(2.5f), it.PartNumberName ?? "—", accent, bold: true, mono: true);
                        Cell(r.RelativeItem(3f), it.Description ?? "—", Colors.Grey.Darken1);
                        Cell(r.ConstantItem(30), it.Qty.ToString(), primary, bold: true);

                        // Condition badge-style
                        r.ConstantItem(70).Background(bg)
                            .BorderBottom(hasSubrow ? 0f : 0.5f).BorderColor(Colors.Grey.Lighten3)
                            .Padding(4).AlignCenter().AlignMiddle().Column(cc =>
                            {
                                var cond = it.Condition ?? "—";
                                cc.Item().AlignCenter().Background(Colors.LightBlue.Lighten4)
                                    .Padding(3).Text(t => t.Span(cond).FontSize(9).Bold().FontColor(Colors.LightBlue.Darken3));
                            });
                    });

                    // Sub-row: Remark + Alternatives
                    if (hasSubrow)
                    {
                        var altsText = it.Alternatives is { Count: > 0 }
                            ? string.Join(" | ", it.Alternatives)
                            : "—";

                        group.Item().Background(bg)
                            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                            .PaddingVertical(3).PaddingLeft(34).PaddingRight(8)
                            .Text(t =>
                            {
                                t.Span("Remark: ").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                                t.Span((it.Remark ?? "—") + "   ").FontSize(8).FontColor(Colors.Grey.Darken1);
                                t.Span("Alternatives: ").Bold().FontSize(8).FontColor(accent);
                                t.Span(altsText).FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                    }
                });
            }
        });
    }
}
