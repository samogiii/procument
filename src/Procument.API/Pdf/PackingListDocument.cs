using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// Packing List PDF.
/// Simplified layout: Header, Bill To/Ship To, PO Ref, Items table, Contact Email footer.
/// No bank details, terms, conditions, or comments.
/// </summary>
public static class PackingListDocument
{
    public static byte[] Generate(PackingListPdfRequest req)
    {
        var primary = req.PrimaryColor ?? "#312e81";
        var accent  = req.AccentColor  ?? "#6366f1";

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
                    col.Item().Element(c => PdfHelpers.DrawPageHeader(
                        c,
                        req.LogoBase64,
                        req.CompanyName, req.CompanyLocation,
                        req.CompanyPhone, req.CompanyWebsite, req.CompanyEmail,
                        "PACKING LIST", $"For {req.InvoiceNumber}", primary));

                    // Accent line
                    col.Item().Element(c => PdfHelpers.DrawAccentLine(c, primary, accent));

                    // Meta row: Date | Customer PO | Proforma Ref
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        void Meta(string label, string? val)
                            => row.RelativeItem().AlignLeft().Text(t =>
                            {
                                t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                t.Span(val ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        Meta("Date", req.InvoiceDate);
                        Meta("Customer PO", req.CustomerPONumber);
                        Meta("PI Ref", req.ProformaRef);
                    });

                    // Bill To / Ship To
                    col.Item().PaddingBottom(12).Row(row =>
                    {
                        // Bill To
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "BILL TO", accent));
                            col.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10).Column(bc =>
                                {
                                    void Field(string label, string? val)
                                    {
                                        if (string.IsNullOrWhiteSpace(val)) return;
                                        bc.Item().Text(t =>
                                        {
                                            t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                            t.Span(val).FontSize(9).FontColor(Colors.Grey.Darken1);
                                        });
                                    }
                                    bc.Item().Text(req.CustomerBillToName ?? req.CustomerName ?? "—").Bold().FontSize(10).FontColor(primary);
                                    if (!string.IsNullOrWhiteSpace(req.CustomerBillTo))
                                        bc.Item().Text(req.CustomerBillTo).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    Field("Contact Person", req.CustomerBillToContactPerson);
                                    Field("Email", req.CustomerBillToEmail);
                                    Field("Phone", req.CustomerBillToPhone);
                                });
                        });

                        // Ship To
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "SHIP TO", accent));
                            col.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10).Column(bc =>
                                {
                                    void Field(string label, string? val)
                                    {
                                        if (string.IsNullOrWhiteSpace(val)) return;
                                        bc.Item().Text(t =>
                                        {
                                            t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                            t.Span(val).FontSize(9).FontColor(Colors.Grey.Darken1);
                                        });
                                    }
                                    bc.Item().Text(req.CustomerShipToName ?? req.CustomerName ?? "—").Bold().FontSize(10).FontColor(primary);
                                    if (!string.IsNullOrWhiteSpace(req.CustomerShipTo))
                                        bc.Item().Text(req.CustomerShipTo).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    Field("Contact Person", req.CustomerShipToContactPerson);
                                    Field("Email", req.CustomerShipToEmail);
                                    Field("Phone", req.CustomerShipToPhone);
                                    Field("Account", req.CustomerShipToAccount);
                                });
                        });
                    });

                    // Items table
                    col.Item().PaddingBottom(10).Element(c => ComposeItemsTable(c, req, primary));

                    // Packages / shipping dimensions
                    if (req.Packages != null && req.Packages.Count > 0)
                        col.Item().PaddingBottom(10).Element(c => ComposePackagesSection(c, req.Packages, primary, accent));
                });

                // Footer with contact email
                page.Footer().Element(c => DrawContactFooter(c, req.CompanyEmail, primary));
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeItemsTable(IContainer container, PackingListPdfRequest req, string primary)
    {
        var items = req.Items ?? [];

        container.Column(outer =>
        {
            // Header
            outer.Item().Row(hr =>
            {
                string[] headers = ["#", "Part Number", "Description", "Qty", "CD", "Certification"];
                float[] widths   = [20,   0,             0,           40,   40,  60];
                float[] rels     = [0,    1.5f,           2f,          0,    0,   0];

                for (int h = 0; h < headers.Length; h++)
                {
                    var label = headers[h];
                    var w = widths[h];
                    var rel = rels[h];
                    var cell = w > 0 ? (IContainer)hr.ConstantItem(w) : hr.RelativeItem(rel);
                    cell.Background(primary).Padding(6)
                        .AlignCenter().Text(t => t.Span(label).FontSize(7f).Bold().FontColor(Colors.White));
                }
            });

            // Data rows
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
                                var s = t.Span(text).FontSize(8f);
                                if (color != null) s.FontColor(color);
                                if (bold) s.Bold();
                            });

                    Cell(r.ConstantItem(20), (idx + 1).ToString(), Colors.Grey.Darken1);
                    Cell(r.RelativeItem(1.5f), it.PartNumber ?? "—", primary, bold: true);
                    Cell(r.RelativeItem(2f), it.Description ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(40), it.Qty.ToString(), primary, bold: true);
                    Cell(r.ConstantItem(40), it.Condition ?? "—", primary);
                    Cell(r.ConstantItem(60), it.Certification ?? "—", Colors.Grey.Darken1);
                });
            }
        });
    }

    private static void ComposePackagesSection(IContainer container, List<PackingListPackage> packages, string primary, string accent)
    {
        container.Column(col =>
        {
            // Section label
            col.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "SHIPPING DETAILS", accent));

            // Header row
            col.Item().PaddingTop(6).Row(hr =>
            {
                hr.ConstantItem(30).Background(primary).Padding(6)
                    .AlignCenter().Text(t => t.Span("Pkg").FontSize(7f).Bold().FontColor(Colors.White));
                hr.RelativeItem().Background(primary).Padding(6)
                    .AlignCenter().Text(t => t.Span("Weight").FontSize(7f).Bold().FontColor(Colors.White));
                hr.RelativeItem().Background(primary).Padding(6)
                    .AlignCenter().Text(t => t.Span("Dimensions").FontSize(7f).Bold().FontColor(Colors.White));
            });

            // Data rows
            for (int i = 0; i < packages.Count; i++)
            {
                var pkg = packages[i];
                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                col.Item().Row(r =>
                {
                    void Cell(IContainer c, string text)
                        => c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                            .Padding(6).AlignCenter().AlignMiddle()
                            .Text(t => t.Span(text).FontSize(9f).FontColor(primary));

                    Cell(r.ConstantItem(30), (i + 1).ToString());
                    Cell(r.RelativeItem(), pkg.Weight ?? "—");
                    Cell(r.RelativeItem(), pkg.Dimensions ?? "—");
                });
            }
        });
    }

    private static void DrawContactFooter(IContainer container, string? email, string primary)
    {
        container.BorderTop(2).BorderColor(primary).PaddingVertical(12).AlignCenter().Column(col =>
        {
            col.Item().Text(t =>
            {
                t.Span("IF you have any questions about this purchase order, please contact: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrWhiteSpace(email))
                    t.Span(email).FontSize(9).Bold().FontColor(primary);
            });
        });
    }
}
