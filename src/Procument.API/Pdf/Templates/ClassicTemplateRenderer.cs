using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// Template 2 — "Classic": the plain agency/government form look.
/// Everything is black on white, boxed with hairline rules, uppercase section bars and a
/// signature grid at the foot. Brand colours are deliberately ignored so the document
/// photocopies and faxes cleanly; only the values differ from the Modern template.
/// </summary>
public static class ClassicTemplateRenderer
{
    private const float B = 0.9f;             // hairline rule weight
    private const string Ink = "#000000";
    private static readonly string Bar = Colors.Grey.Lighten2;

    public static byte[] Render(PdfDocModel m)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(18);
                page.MarginHorizontal(26);
                page.MarginBottom(14);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Ink));

                page.Content().Column(col =>
                {
                    // Header + meta strip stack flush so they read as one boxed masthead
                    col.Item().Element(c => Header(c, m));
                    if (m.Meta.Count > 0) col.Item().Element(c => MetaStrip(c, m));

                    if (!string.IsNullOrWhiteSpace(m.Notice))
                        col.Item().BorderLeft(B).BorderRight(B).BorderBottom(B).BorderColor(Ink)
                            .Padding(4).AlignCenter()
                            .Text(t => t.Span(m.Notice!.ToUpperInvariant()).Bold().FontSize(9f));

                    if (m.Addresses.Count > 0)
                        col.Item().PaddingTop(7).Element(c => Addresses(c, m));

                    // Line items
                    col.Item().PaddingTop(7).Element(c => SectionBar(c, m.ItemsTitle));
                    col.Item().Element(c => Table(c, m));

                    // Supporting blocks (bank / shipping) on the left, totals on the right
                    if (m.InfoBlocks.Any(b => b.HasContent) || m.Totals.Count > 0)
                    {
                        col.Item().PaddingTop(7).Row(sr =>
                        {
                            sr.RelativeItem().Column(left =>
                            {
                                foreach (var block in m.InfoBlocks.Where(b => b.HasContent))
                                    left.Item().PaddingBottom(6).Element(c => InfoBlock(c, block));
                            });
                            sr.ConstantItem(10);
                            if (m.Totals.Count > 0)
                                sr.AutoItem().Element(c => Totals(c, m));
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(m.Comments))
                        col.Item().PaddingTop(7).Element(c => TextBlock(c, "Comments", m.Comments!));

                    if (!string.IsNullOrWhiteSpace(m.Terms))
                        col.Item().PaddingTop(7).Element(c => TextBlock(c, "Terms and Conditions", m.Terms!));

                    if (m.ShowSignatureBlock)
                        col.Item().PaddingTop(7).Element(c => Signature(c, m));
                });

                page.Footer().Element(c => Footer(c, m));
            });
        });

        return doc.GeneratePdf();
    }

    // ───────────────────── HEADER ─────────────────────
    private static void Header(IContainer container, PdfDocModel m)
    {
        container.Border(B).BorderColor(Ink).Row(row =>
        {
            // Left — logo + issuer name
            row.RelativeItem(3).BorderRight(B).BorderColor(Ink).Padding(6).AlignMiddle().Column(left =>
            {
                var bytes = DecodeLogo(m.LogoBase64);
                if (bytes != null) left.Item().Height(40).Image(bytes, ImageScaling.FitHeight);
                if (!string.IsNullOrWhiteSpace(m.CompanyName))
                    left.Item().PaddingTop(bytes != null ? 4 : 0)
                        .Text(t => t.Span(m.CompanyName).Bold().FontSize(10.5f));
            });

            // Centre — document title
            row.RelativeItem(4).BorderRight(B).BorderColor(Ink).Padding(6).AlignMiddle().Column(mid =>
            {
                mid.Item().AlignCenter()
                    .Text(t => t.Span(m.DocTitle.ToUpperInvariant()).Bold().FontSize(15f).LetterSpacing(0.05f));
                if (!string.IsNullOrWhiteSpace(m.DocNumber))
                    mid.Item().AlignCenter().PaddingTop(2)
                        .Text(t => t.Span($"No. {m.DocNumber}").FontSize(9f));
            });

            // Right — issuer contact block
            row.RelativeItem(3).Padding(6).AlignMiddle().Column(right =>
            {
                void Line(string? value)
                {
                    if (string.IsNullOrWhiteSpace(value)) return;
                    right.Item().Text(t => t.Span(value).FontSize(7.5f));
                }
                Line(m.CompanyLocation);
                if (!string.IsNullOrWhiteSpace(m.CompanyPhone)) Line($"Tel: {m.CompanyPhone}");
                if (!string.IsNullOrWhiteSpace(m.CompanyWebsite)) Line($"Web: {m.CompanyWebsite}");
                if (!string.IsNullOrWhiteSpace(m.CompanyEmail)) Line($"Email: {m.CompanyEmail}");
            });
        });
    }

    // ───────────────────── META STRIP ─────────────────────
    private static void MetaStrip(IContainer container, PdfDocModel m)
    {
        container.BorderLeft(B).BorderRight(B).BorderBottom(B).BorderColor(Ink).Row(row =>
        {
            for (int i = 0; i < m.Meta.Count; i++)
            {
                var field = m.Meta[i];
                var cell = row.RelativeItem();
                if (i < m.Meta.Count - 1) cell = cell.BorderRight(B).BorderColor(Ink);
                cell.Padding(4).Column(c =>
                {
                    c.Item().Text(t => t.Span(field.Label.ToUpperInvariant()).Bold().FontSize(6.5f));
                    c.Item().PaddingTop(1).Text(t => t.Span(Plain(field.Value)).FontSize(8.5f));
                });
            }
        });
    }

    // ───────────────────── ADDRESSES ─────────────────────
    private static void Addresses(IContainer container, PdfDocModel m)
    {
        container.Border(B).BorderColor(Ink).Row(row =>
        {
            for (int i = 0; i < m.Addresses.Count; i++)
            {
                var block = m.Addresses[i];
                var cell = row.RelativeItem();
                if (i < m.Addresses.Count - 1) cell = cell.BorderRight(B).BorderColor(Ink);
                cell.Column(c =>
                {
                    c.Item().Background(Bar).BorderBottom(B).BorderColor(Ink).Padding(3)
                        .Text(t => t.Span(block.Title.ToUpperInvariant()).Bold().FontSize(7f).LetterSpacing(0.05f));
                    c.Item().Padding(5).Column(body =>
                    {
                        Body(body, block, 0);
                        if (block.Appended != null)
                        {
                            body.Item().PaddingTop(9)
                                .Text(t => t.Span(block.Appended.Title.ToUpperInvariant()).Bold().FontSize(7f));
                            Body(body, block.Appended, 1);
                        }
                    });
                });
            }
        });

        // Name / address / labelled fields of one (possibly appended) address block.
        static void Body(ColumnDescriptor body, PdfAddressBlock block, float topPad)
        {
            body.Item().PaddingTop(topPad).Text(t => t.Span(Plain(block.Name)).Bold().FontSize(9f));
            if (!string.IsNullOrWhiteSpace(block.Address))
                body.Item().PaddingTop(1).Text(t => t.Span(block.Address).FontSize(7.5f));
            foreach (var f in block.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
                body.Item().Text(t =>
                {
                    t.Span($"{f.Label}: ").Bold().FontSize(7.5f);
                    t.Span(f.Value).FontSize(7.5f);
                });
        }
    }

    // ───────────────────── ITEMS TABLE ─────────────────────
    private static void Table(IContainer container, PdfDocModel m)
    {
        container.BorderLeft(B).BorderRight(B).BorderBottom(B).BorderColor(Ink).Column(outer =>
        {
            // Header row
            outer.Item().Background(Bar).BorderBottom(B).BorderColor(Ink).Row(hr =>
            {
                for (int i = 0; i < m.Columns.Count; i++)
                {
                    var col = m.Columns[i];
                    var cell = ColumnItem(hr, col);
                    if (i < m.Columns.Count - 1) cell = cell.BorderRight(B).BorderColor(Ink);
                    Align(cell.Padding(4), col.Align)
                        .Text(t => t.Span(col.Header).Bold().FontSize(7f));
                }
            });

            // Data rows
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
                            cell = cell.BorderColor(Ink).Padding(3.5f).AlignMiddle();

                            if (!string.IsNullOrWhiteSpace(value.SubText))
                            {
                                Align(cell, col.Align).Column(c =>
                                {
                                    c.Item().Text(t => t.Span(Plain(value.Text)).FontSize(7.5f).Bold());
                                    c.Item().Text(t => t.Span(value.SubText).FontSize(6.5f));
                                });
                            }
                            else
                            {
                                Align(cell, col.Align).Text(t =>
                                {
                                    var span = t.Span(Plain(value.Text)).FontSize(7.5f);
                                    if (value.Bold || value.Highlight) span.Bold();
                                });
                            }
                        }
                    });

                    if (hasNote)
                    {
                        var noteCell = group.Item();
                        if (!isLast) noteCell = noteCell.BorderBottom(B).BorderColor(Ink);
                        noteCell.PaddingHorizontal(4).PaddingBottom(3)
                            .Text(t => t.Span(dataRow.Note).FontSize(7f).Italic());
                    }
                });
            }
        });
    }

    // ───────────────────── INFO BLOCK ─────────────────────
    private static void InfoBlock(IContainer container, PdfInfoBlock block)
    {
        container.Border(B).BorderColor(Ink).Column(c =>
        {
            c.Item().Background(Bar).BorderBottom(B).BorderColor(Ink).Padding(3)
                .Text(t => t.Span(block.Title.ToUpperInvariant()).Bold().FontSize(7f).LetterSpacing(0.05f));
            c.Item().Padding(5).Column(body =>
            {
                foreach (var f in block.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
                    body.Item().Text(t =>
                    {
                        t.Span($"{f.Label}: ").Bold().FontSize(7.5f);
                        t.Span(f.Value).FontSize(7.5f);
                    });
            });
        });
    }

    // ───────────────────── TOTALS ─────────────────────
    private static void Totals(IContainer container, PdfDocModel m)
    {
        container.Width(230).Border(B).BorderColor(Ink).Column(col =>
        {
            for (int i = 0; i < m.Totals.Count; i++)
            {
                var line = m.Totals[i];
                var item = col.Item();
                if (i < m.Totals.Count - 1) item = item.BorderBottom(B).BorderColor(Ink);
                if (line.IsGrand) item = item.Background(Bar);

                item.Padding(4).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        var s = t.Span(line.Label).FontSize(8f);
                        if (line.IsGrand) s.Bold();
                    });
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        var s = t.Span(Money(m, line)).FontSize(line.IsGrand ? 9.5f : 8f);
                        if (line.IsGrand) s.Bold();
                    });
                });
            }
        });
    }

    // ───────────────────── FREE TEXT ─────────────────────
    private static void TextBlock(IContainer container, string title, string body)
    {
        container.Column(col =>
        {
            col.Item().Element(c => SectionBar(c, title));
            col.Item().BorderLeft(B).BorderRight(B).BorderBottom(B).BorderColor(Ink).Padding(6)
                .Text(t => t.Span(body).FontSize(7.5f));
        });
    }

    // ───────────────────── SIGNATURE GRID ─────────────────────
    private static void Signature(IContainer container, PdfDocModel m)
    {
        container.Column(col =>
        {
            col.Item().Element(c => SectionBar(c, "This section to be completed by the issuer"));
            col.Item().BorderLeft(B).BorderRight(B).BorderBottom(B).BorderColor(Ink).Column(inner =>
            {
                inner.Item().Row(r =>
                {
                    Filled(r, "Company Name", m.CompanyName, 2.4f);
                    Filled(r, "Address", m.CompanyLocation, 3f);
                    Filled(r, "Phone", m.CompanyPhone, 1.6f);
                    Filled(r, "Email", m.CompanyEmail, 2.2f, last: true);
                });
                inner.Item().BorderTop(B).BorderColor(Ink).Row(r =>
                {
                    Blank(r, "Signature", 2.4f);
                    Blank(r, "Typed Name and Title", 2.4f);
                    Blank(r, "Date", 1.4f, last: true);
                });
            });
        });

        static void Filled(RowDescriptor row, string label, string? value, float relative, bool last = false)
        {
            var cell = row.RelativeItem(relative);
            if (!last) cell = cell.BorderRight(B).BorderColor(Ink);
            cell.Padding(4).Column(c =>
            {
                c.Item().Text(t => t.Span(label).FontSize(6.5f));
                c.Item().PaddingTop(1).Text(t => t.Span(Plain(value)).FontSize(8f));
            });
        }

        static void Blank(RowDescriptor row, string label, float relative, bool last = false)
        {
            var cell = row.RelativeItem(relative);
            if (!last) cell = cell.BorderRight(B).BorderColor(Ink);
            cell.Padding(4).Column(c =>
            {
                c.Item().Height(22).Text(string.Empty);
                c.Item().BorderTop(B).BorderColor(Ink).PaddingTop(2)
                    .Text(t => t.Span(label).FontSize(6.5f));
            });
        }
    }

    // ───────────────────── FOOTER ─────────────────────
    private static void Footer(IContainer container, PdfDocModel m)
    {
        container.BorderTop(B).BorderColor(Ink).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text(t => t.Span(m.FooterText ?? "").FontSize(7f));
            row.RelativeItem().AlignCenter().Text(t =>
            {
                t.Span("Page ").FontSize(7f);
                t.CurrentPageNumber().FontSize(7f);
                t.Span(" of ").FontSize(7f);
                t.TotalPages().FontSize(7f);
            });
            row.RelativeItem().AlignRight().Text(t => t.Span(m.CompanyEmail ?? "").FontSize(7f));
        });
    }

    // ───────────────────── SHARED BITS ─────────────────────
    private static void SectionBar(IContainer container, string text)
        => container.Border(B).BorderColor(Ink).Background(Bar).Padding(4)
            .Text(t => t.Span(text.ToUpperInvariant()).Bold().FontSize(8f).LetterSpacing(0.05f));

    private static IContainer ColumnItem(RowDescriptor row, PdfTableColumn col)
        => col.Width > 0 ? row.ConstantItem(col.Width) : row.RelativeItem(col.Relative);

    private static IContainer Align(IContainer container, PdfCellAlign align) => align switch
    {
        PdfCellAlign.Left => container.AlignLeft(),
        PdfCellAlign.Right => container.AlignRight(),
        _ => container.AlignCenter()
    };

    /// <summary>Blank rather than an em-dash — a form reads better with empty boxes.</summary>
    private static string Plain(string? value)
        => string.IsNullOrWhiteSpace(value) || value == "—" ? "" : value!;

    private static string Money(PdfDocModel m, PdfTotalLine line)
        => $"{(line.IsNegative ? "-" : "")}{m.CurrencySymbol}{PdfHelpers.FormatPrice(line.Amount)}";

    internal static byte[]? DecodeLogo(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var raw = base64;
            if (raw.Contains(',')) raw = raw[(raw.IndexOf(',') + 1)..];
            return Convert.FromBase64String(raw);
        }
        catch { return null; }
    }
}
