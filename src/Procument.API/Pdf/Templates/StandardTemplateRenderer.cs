using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// Template 3 — "Standard": deliberately halfway between Classic and Modern.
/// It keeps the fully ruled, boxed structure of a traditional business form, but uses the
/// company preset colours for the title rule, section captions, table header text and grand
/// total. No filled colour bands, no zebra striping, no gradient accent line.
/// </summary>
public static class StandardTemplateRenderer
{
    private const float B = 0.6f;                        // grid rule
    private static readonly string Rule = Colors.Grey.Lighten1;
    private static readonly string Band = Colors.Grey.Lighten3;
    private static readonly string Muted = Colors.Grey.Darken1;

    public static byte[] Render(PdfDocModel m)
    {
        var primary = m.Primary;
        var accent = m.Accent;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(20);
                page.MarginHorizontal(30);
                page.MarginBottom(12);
                page.DefaultTextStyle(x => x.FontSize(9f).FontColor(Muted));

                page.Content().Column(col =>
                {
                    col.Item().Element(c => Header(c, m, primary));
                    col.Item().PaddingTop(6).Height(2).Background(primary);

                    if (m.Meta.Count > 0)
                        col.Item().PaddingTop(8).Element(c => MetaStrip(c, m, primary));

                    if (!string.IsNullOrWhiteSpace(m.Notice))
                        col.Item().PaddingTop(8).Border(B).BorderColor(primary).Padding(5).AlignCenter()
                            .Text(t => t.Span(m.Notice!.ToUpperInvariant()).Bold().FontSize(9f).FontColor(primary));

                    if (m.Addresses.Count > 0)
                        col.Item().PaddingTop(10).Element(c => Addresses(c, m, primary));

                    col.Item().PaddingTop(12).Element(c => Caption(c, m.ItemsTitle, primary));
                    col.Item().PaddingTop(4).Element(c => Table(c, m, primary, accent));

                    if (m.InfoBlocks.Any(b => b.HasContent) || m.Totals.Count > 0)
                    {
                        col.Item().PaddingTop(12).Row(sr =>
                        {
                            sr.RelativeItem().Column(left =>
                            {
                                foreach (var block in m.InfoBlocks.Where(b => b.HasContent))
                                    left.Item().PaddingBottom(8).Element(c => InfoBlock(c, block, primary));
                            });
                            sr.ConstantItem(12);
                            if (m.Totals.Count > 0)
                                sr.AutoItem().Element(c => Totals(c, m, primary));
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(m.Comments))
                        col.Item().PaddingTop(10).Element(c => TextBlock(c, "Comments", m.Comments!, primary));

                    if (!string.IsNullOrWhiteSpace(m.Terms))
                        col.Item().PaddingTop(10).Element(c => TextBlock(c, "Terms & Conditions", m.Terms!, primary));
                });

                page.Footer().Element(c => Footer(c, m, primary));
            });
        });

        return doc.GeneratePdf();
    }

    // ───────────────────── HEADER ─────────────────────
    private static void Header(IContainer container, PdfDocModel m, string primary)
    {
        container.Row(row =>
        {
            row.RelativeItem(3).Column(left =>
            {
                var bytes = ClassicTemplateRenderer.DecodeLogo(m.LogoBase64);
                if (bytes != null) left.Item().Height(48).Image(bytes, ImageScaling.FitHeight);
                left.Item().PaddingTop(bytes != null ? 4 : 0)
                    .Text(t => t.Span(m.CompanyName ?? "").Bold().FontSize(13f).FontColor(primary));

                void Line(string? value)
                {
                    if (string.IsNullOrWhiteSpace(value)) return;
                    left.Item().Text(t => t.Span(value).FontSize(7.5f).FontColor(Muted));
                }
                Line(m.CompanyLocation);
                if (!string.IsNullOrWhiteSpace(m.CompanyPhone)) Line($"Tel: {m.CompanyPhone}");
                if (!string.IsNullOrWhiteSpace(m.CompanyWebsite)) Line($"Web: {m.CompanyWebsite}");
                if (!string.IsNullOrWhiteSpace(m.CompanyEmail)) Line($"Email: {m.CompanyEmail}");
            });

            row.RelativeItem(2).AlignRight().AlignBottom().Column(right =>
            {
                right.Item().AlignRight()
                    .Text(t => t.Span(m.DocTitle.ToUpperInvariant()).Bold().FontSize(19f).FontColor(primary));
                if (!string.IsNullOrWhiteSpace(m.DocNumber))
                    right.Item().AlignRight().PaddingTop(2)
                        .Text(t => t.Span(m.DocNumber).FontSize(10f).FontColor(Muted));
            });
        });
    }

    // ───────────────────── META STRIP ─────────────────────
    private static void MetaStrip(IContainer container, PdfDocModel m, string primary)
    {
        container.Border(B).BorderColor(Rule).Row(row =>
        {
            for (int i = 0; i < m.Meta.Count; i++)
            {
                var field = m.Meta[i];
                var cell = row.RelativeItem();
                if (i < m.Meta.Count - 1) cell = cell.BorderRight(B).BorderColor(Rule);
                cell.Padding(6).Column(c =>
                {
                    c.Item().Text(t => t.Span(field.Label.ToUpperInvariant())
                        .Bold().FontSize(6.5f).FontColor(primary).LetterSpacing(0.05f));
                    c.Item().PaddingTop(2)
                        .Text(t => t.Span(string.IsNullOrWhiteSpace(field.Value) ? "—" : field.Value!)
                            .FontSize(9f).FontColor(Muted));
                });
            }
        });
    }

    // ───────────────────── ADDRESSES ─────────────────────
    private static void Addresses(IContainer container, PdfDocModel m, string primary)
    {
        container.Row(row =>
        {
            for (int i = 0; i < m.Addresses.Count; i++)
            {
                var block = m.Addresses[i];
                var cell = row.RelativeItem();
                if (i < m.Addresses.Count - 1) cell = cell.PaddingRight(8);
                cell.Border(B).BorderColor(Rule).Column(c =>
                {
                    c.Item().Background(Band).BorderBottom(B).BorderColor(Rule).Padding(4)
                        .Text(t => t.Span(block.Title.ToUpperInvariant())
                            .Bold().FontSize(7f).FontColor(primary).LetterSpacing(0.05f));
                    c.Item().Padding(8).Column(body =>
                    {
                        Body(body, block, 0);
                        if (block.Appended != null)
                        {
                            body.Item().PaddingTop(10).Text(t => t.Span(block.Appended.Title.ToUpperInvariant())
                                .Bold().FontSize(7f).FontColor(primary).LetterSpacing(0.05f));
                            Body(body, block.Appended, 2);
                        }
                    });
                });
            }
        });

        // Name / address / labelled fields of one (possibly appended) address block.
        void Body(ColumnDescriptor body, PdfAddressBlock block, float topPad)
        {
            body.Item().PaddingTop(topPad)
                .Text(t => t.Span(block.Name ?? "—").Bold().FontSize(10f).FontColor(primary));
            if (!string.IsNullOrWhiteSpace(block.Address))
                body.Item().PaddingTop(2).Text(t => t.Span(block.Address).FontSize(8f).FontColor(Muted));
            foreach (var f in block.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
                body.Item().Text(t =>
                {
                    t.Span($"{f.Label}: ").Bold().FontSize(8f).FontColor(primary);
                    t.Span(f.Value).FontSize(8f).FontColor(Muted);
                });
        }
    }

    // ───────────────────── ITEMS TABLE ─────────────────────
    private static void Table(IContainer container, PdfDocModel m, string primary, string accent)
    {
        container.Border(B).BorderColor(Rule).Column(outer =>
        {
            outer.Item().Background(Band).BorderBottom(1f).BorderColor(primary).Row(hr =>
            {
                for (int i = 0; i < m.Columns.Count; i++)
                {
                    var col = m.Columns[i];
                    var cell = ColumnItem(hr, col);
                    if (i < m.Columns.Count - 1) cell = cell.BorderRight(B).BorderColor(Rule);
                    Align(cell.Padding(5), col.Align)
                        .Text(t => t.Span(col.Header).Bold().FontSize(7.5f).FontColor(primary));
                }
            });

            for (int r = 0; r < m.Rows.Count; r++)
            {
                var dataRow = m.Rows[r];
                var isLast = r == m.Rows.Count - 1;
                var hasNote = !string.IsNullOrWhiteSpace(dataRow.Note);

                outer.Item().ShowEntire().Column(group =>
                {
                    group.Item().Row(rr =>
                    {
                        for (int i = 0; i < m.Columns.Count; i++)
                        {
                            var col = m.Columns[i];
                            var value = i < dataRow.Cells.Count ? dataRow.Cells[i] : new PdfCell();
                            var cell = ColumnItem(rr, col);
                            if (i < m.Columns.Count - 1) cell = cell.BorderRight(B);
                            if (!isLast && !hasNote) cell = cell.BorderBottom(B);
                            cell = cell.BorderColor(Rule).Padding(5).AlignMiddle();

                            var color = value.Negative ? "#e53935"
                                      : value.Highlight ? accent
                                      : value.Bold ? primary
                                      : Muted;

                            if (!string.IsNullOrWhiteSpace(value.SubText))
                            {
                                Align(cell, col.Align).Column(c =>
                                {
                                    c.Item().Text(t => t.Span(value.Text).FontSize(8f).Bold().FontColor(color));
                                    c.Item().Text(t => t.Span(value.SubText).FontSize(6.5f).FontColor(Colors.Grey.Medium));
                                });
                            }
                            else
                            {
                                Align(cell, col.Align).Text(t =>
                                {
                                    var span = t.Span(value.Text).FontSize(8f).FontColor(color);
                                    if (value.Bold || value.Highlight) span.Bold();
                                });
                            }
                        }
                    });

                    if (hasNote)
                    {
                        var noteCell = group.Item();
                        if (!isLast) noteCell = noteCell.BorderBottom(B).BorderColor(Rule);
                        noteCell.PaddingHorizontal(5).PaddingBottom(4)
                            .Text(t => t.Span(dataRow.Note).FontSize(7.5f).FontColor(Muted));
                    }
                });
            }
        });
    }

    // ───────────────────── INFO BLOCK ─────────────────────
    private static void InfoBlock(IContainer container, PdfInfoBlock block, string primary)
    {
        container.Border(B).BorderColor(Rule).Column(c =>
        {
            c.Item().Background(Band).BorderBottom(B).BorderColor(Rule).Padding(4)
                .Text(t => t.Span(block.Title.ToUpperInvariant())
                    .Bold().FontSize(7f).FontColor(primary).LetterSpacing(0.05f));
            c.Item().Padding(8).Column(body =>
            {
                foreach (var f in block.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
                    body.Item().Text(t =>
                    {
                        t.Span($"{f.Label}: ").Bold().FontSize(8f).FontColor(primary);
                        t.Span(f.Value).FontSize(8f).FontColor(Muted);
                    });
            });
        });
    }

    // ───────────────────── TOTALS ─────────────────────
    private static void Totals(IContainer container, PdfDocModel m, string primary)
    {
        container.Width(235).Border(B).BorderColor(Rule).Column(col =>
        {
            for (int i = 0; i < m.Totals.Count; i++)
            {
                var line = m.Totals[i];
                var item = col.Item();
                if (line.IsGrand) item = item.BorderTop(1.4f).BorderColor(primary).Background(Band);
                else if (i < m.Totals.Count - 1) item = item.BorderBottom(B).BorderColor(Rule);

                item.Padding(6).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        var s = t.Span(line.Label).FontSize(8.5f).FontColor(line.IsGrand ? primary : Muted);
                        if (line.IsGrand) s.Bold();
                    });
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        var s = t.Span(Money(m, line))
                            .FontSize(line.IsGrand ? 11f : 8.5f)
                            .FontColor(line.IsNegative ? "#e53935" : primary);
                        if (line.IsGrand) s.Bold();
                    });
                });
            }
        });
    }

    // ───────────────────── FREE TEXT ─────────────────────
    private static void TextBlock(IContainer container, string title, string body, string primary)
    {
        container.Border(B).BorderColor(Rule).Column(col =>
        {
            col.Item().Background(Band).BorderBottom(B).BorderColor(Rule).Padding(4)
                .Text(t => t.Span(title.ToUpperInvariant()).Bold().FontSize(7f).FontColor(primary).LetterSpacing(0.05f));
            col.Item().Padding(8).Text(t => t.Span(body).FontSize(8f).FontColor(Muted));
        });
    }

    // ───────────────────── FOOTER ─────────────────────
    private static void Footer(IContainer container, PdfDocModel m, string primary)
    {
        container.BorderTop(1f).BorderColor(primary).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(t => t.Span(m.FooterText ?? "").FontSize(7.5f).FontColor(Colors.Grey.Medium));
            row.RelativeItem().AlignCenter().Text(t =>
            {
                t.Span("Page ").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                t.CurrentPageNumber().FontSize(7.5f).FontColor(Colors.Grey.Medium);
                t.Span(" / ").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                t.TotalPages().FontSize(7.5f).FontColor(Colors.Grey.Medium);
            });
            row.RelativeItem().AlignRight().Text(t => t.Span(m.CompanyEmail ?? "").FontSize(7.5f).Bold().FontColor(primary));
        });
    }

    // ───────────────────── SHARED BITS ─────────────────────
    private static void Caption(IContainer container, string text, string primary)
        => container.BorderLeft(3).BorderColor(primary).PaddingLeft(8)
            .Text(t => t.Span(text.ToUpperInvariant()).Bold().FontSize(8f).FontColor(primary).LetterSpacing(0.05f));

    private static IContainer ColumnItem(RowDescriptor row, PdfTableColumn col)
        => col.Width > 0 ? row.ConstantItem(col.Width) : row.RelativeItem(col.Relative);

    private static IContainer Align(IContainer container, PdfCellAlign align) => align switch
    {
        PdfCellAlign.Left => container.AlignLeft(),
        PdfCellAlign.Right => container.AlignRight(),
        _ => container.AlignCenter()
    };

    private static string Money(PdfDocModel m, PdfTotalLine line)
        => $"{(line.IsNegative ? "-" : "")}{m.CurrencySymbol}{PdfHelpers.FormatPrice(line.Amount)}";
}
