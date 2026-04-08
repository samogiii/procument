using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// Purchase Order PDF.
/// Colors come from the selected Company Preset (PrimaryColor / AccentColor).
/// Signature sections: Vendor + Deliver To address boxes; Shipping/Incoterms block.
/// Unique column: Note (replaces Lead Time).
/// </summary>
public static class PurchaseOrderDocument
{
    public static byte[] Generate(PurchaseOrderPdfRequest req)
    {
        var primary = req.PrimaryColor ?? "#92400e";
        var accent  = req.AccentColor  ?? "#d97706";
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
                    col.Item().Element(c => PdfHelpers.DrawPageHeader(
                        c,
                        req.LogoBase64,
                        req.CompanyName, req.CompanyLocation,
                        req.CompanyPhone, req.CompanyWebsite, req.CompanyEmail,
                        "PURCHASE ORDER", req.PoNumber, primary));

                    // Accent line
                    col.Item().Element(c => PdfHelpers.DrawAccentLine(c, primary, accent));

                    // Meta row: Date | Ordered By | Status | Currency
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        void Meta(string label, string? val)
                            => row.RelativeItem().AlignLeft().Text(t =>
                            {
                                t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                t.Span(val ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        Meta("Date", req.PoDate);
                        Meta("Ordered By", req.OrderedBy);
                        Meta("Status", req.Status);
                        Meta("Currency", req.Currency);
                    });

                    // Vendor / Deliver To (both with phone + email extra lines)
                    col.Item().PaddingBottom(12).Row(row =>
                    {
                        PdfHelpers.DrawAddressBox(row.RelativeItem(), "VENDOR",
                            req.VendorName, req.VendorAddress,
                            string.IsNullOrEmpty(req.VendorPhone) ? null : $"Tel: {req.VendorPhone}",
                            string.IsNullOrEmpty(req.VendorEmail) ? null : $"Email: {req.VendorEmail}",
                            accent);
                        PdfHelpers.DrawAddressBox(row.RelativeItem(), "DELIVER TO",
                            req.DeliverToName, req.DeliverToAddress,
                            string.IsNullOrEmpty(req.DeliverToPhone) ? null : $"Tel: {req.DeliverToPhone}",
                            string.IsNullOrEmpty(req.DeliverToEmail) ? null : $"Email: {req.DeliverToEmail}",
                            accent);
                    });

                    // Items table
                    col.Item().PaddingBottom(10).Element(c => ComposeItemsTable(c, req, primary, sym));

                    // Totals + Shipping Info side by side
                    col.Item().PaddingBottom(10).Row(sRow =>
                    {
                        // Shipping Info (left)
                        sRow.RelativeItem().Column(left =>
                        {
                            var hasShipping = !string.IsNullOrWhiteSpace(req.ShippingMethod)
                                           || !string.IsNullOrWhiteSpace(req.Incoterms)
                                           || !string.IsNullOrWhiteSpace(req.FedExAccount);
                            if (!hasShipping) return;

                            left.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "Shipping Information", accent));
                            left.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10).Column(b =>
                                {
                                    void ShipRow(string label, string? val)
                                    {
                                        if (string.IsNullOrWhiteSpace(val)) return;
                                        b.Item().Text(t =>
                                        {
                                            t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                            t.Span(val).FontSize(9).FontColor(Colors.Grey.Darken1);
                                        });
                                    }
                                    ShipRow("Shipping Method", req.ShippingMethod);
                                    ShipRow("Incoterms", req.Incoterms);
                                    ShipRow("FedEx Account", req.FedExAccount);
                                    ShipRow("Service Priority", req.ServicePriority);
                                });
                        });

                        sRow.ConstantItem(12);

                        // Totals (right)
                        sRow.AutoItem().Element(c => PdfHelpers.DrawTotals(c,
                            req.Subtotal ?? 0, req.Tax ?? 0,
                            req.TotalShipping ?? 0, req.Other ?? 0,
                            primary, sym));
                    });

                    // Comments
                    col.Item().PaddingBottom(8).Element(c => PdfHelpers.DrawComments(c, req.Comments, accent));

                    // Terms & Conditions
                    col.Item().PaddingBottom(8).Element(c => PdfHelpers.DrawTerms(c, req.Terms, accent));
                });

                page.Footer().Element(c => PdfHelpers.DrawFooter(c, req.FooterText, req.CompanyEmail, primary));
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeItemsTable(IContainer container, PurchaseOrderPdfRequest req,
        string primary, string sym)
    {
        var items = req.Items ?? [];

        container.Column(outer =>
        {
            // Header
            outer.Item().Row(hr =>
            {
                string[] headers = ["#", "Part No.", "Description", "Qty", "CD", "Cert", "Buy Price", "Amount", "Note"];
                float[] widths   = [22,  0,           0,             28,    28,   60,    60,           65,      0];
                float[] rels     = [0,   2f,          2.5f,          0,     0,    0,     0,            0,       1.5f];

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

            // Data rows
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var idx = i;
                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                outer.Item().Row(r =>
                {
                    void Cell(IContainer cell, string text, string? color = null, bool bold = false, bool left = false)
                        => cell.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                            .Padding(5).AlignMiddle().Element(e => left ? e.AlignLeft() : e.AlignCenter())
                            .Text(t =>
                            {
                                var s = t.Span(text).FontSize(8.5f);
                                if (color != null) s.FontColor(color);
                                if (bold) s.Bold();
                            });

                    Cell(r.ConstantItem(22), (idx + 1).ToString(), Colors.Grey.Darken1);
                    Cell(r.RelativeItem(2f), it.PartNumber ?? "—", primary, bold: true);
                    Cell(r.RelativeItem(2.5f), it.Description ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(28), it.Qty.ToString(), primary, bold: true);
                    Cell(r.ConstantItem(28), it.Condition ?? "—", primary);
                    Cell(r.ConstantItem(60), it.Certification ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(60), $"{sym}{PdfHelpers.FormatPrice(it.UnitPrice)}", primary);
                    Cell(r.ConstantItem(65), $"{sym}{PdfHelpers.FormatPrice(it.TotalPrice)}", primary, bold: true);
                    Cell(r.RelativeItem(1.5f), it.Note ?? "", Colors.Grey.Darken1, left: true);
                });
            }
        });
    }
}
