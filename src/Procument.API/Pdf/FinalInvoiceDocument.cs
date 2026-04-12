using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// Final Invoice PDF.
/// Colors come from the selected Company Preset (PrimaryColor / AccentColor).
/// Signature section: Track # + Carrier columns; Proforma Ref in meta row.
/// </summary>
public static class FinalInvoiceDocument
{
    public static byte[] Generate(FinalInvoicePdfRequest req)
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
                    col.Item().Element(c => PdfHelpers.DrawPageHeader(
                        c,
                        req.LogoBase64,
                        req.CompanyName, req.CompanyLocation,
                        req.CompanyPhone, req.CompanyWebsite, req.CompanyEmail,
                        "INVOICE",req.InvoiceNumber, primary));

                    // Accent line
                    col.Item().Element(c => PdfHelpers.DrawAccentLine(c, primary, accent));

                    // Meta row: Date | Due Date | Customer PO | Proforma Ref | Currency
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        void Meta(string label, string? val)
                            => row.RelativeItem().AlignLeft().Text(t =>
                            {
                                t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                t.Span(val ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        Meta("Date", req.InvoiceDate);
                        //Meta("Due Date", req.DueDate);
                        Meta("Customer PO", req.CustomerPONumber);
                        Meta("PI Ref", req.ProformaRef);
                        Meta("Currency", req.Currency);
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
                                    bc.Item().Text(req.CustomerName).Bold().FontSize(10).FontColor(primary);
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
                                    bc.Item().Text(req.CustomerName).Bold().FontSize(10).FontColor(primary);
                                    if (!string.IsNullOrWhiteSpace(req.CustomerShipTo))
                                        bc.Item().Text(req.CustomerShipTo).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    Field("Contact Person", req.CustomerShipToContactPerson);
                                    Field("Email", req.CustomerShipToEmail);
                                    Field("Phone", req.CustomerShipToPhone);
                                    Field("Account", req.CustomerShipToAccount);
                                });
                        });
                    });

                    // Items table (incl. Discount, Track # + Carrier)
                    col.Item().PaddingBottom(10).Element(c => ComposeItemsTable(c, req, primary, sym));

                    //// Totals + Shipping Info + Bank Details
                    //col.Item().PaddingBottom(10).Row(sRow =>
                    //{
                    //    // Shipping Info (left)
                    //    sRow.RelativeItem().Column(left =>
                    //    {
                    //        if (string.IsNullOrWhiteSpace(req.ShippingMethod)) return;
                    //        left.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "Shipping Information", accent));
                    //        left.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                    //            .Padding(10).Column(b =>
                    //            {
                    //                b.Item().Text(t =>
                    //                {
                    //                    t.Span("Shipping Method: ").Bold().FontSize(9).FontColor(primary);
                    //                    t.Span(req.ShippingMethod).FontSize(9).FontColor(Colors.Grey.Darken1);
                    //                });
                    //            });
                    //    });

                    //    sRow.ConstantItem(12);

                    //    // Bank Details (middle)
                    //    sRow.RelativeItem().Column(bank =>
                    //    {
                    //        if (string.IsNullOrWhiteSpace(req.BankName)) return;
                    //        bank.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "Bank Details", accent));
                    //        bank.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                    //            .Padding(10).Column(b =>
                    //            {
                    //                void Field(string label, string? val)
                    //                {
                    //                    if (string.IsNullOrWhiteSpace(val)) return;
                    //                    b.Item().Text(t =>
                    //                    {
                    //                        t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                    //                        t.Span(val).FontSize(9).FontColor(Colors.Grey.Darken1);
                    //                    });
                    //                }
                    //                Field("Beneficiary Name", req.BeneficiaryName);
                    //                Field("Beneficiary Address", req.BeneficiaryAddress);
                    //                Field("Bank Name", req.BankName);
                    //                Field("Bank Address", req.BankAddress);
                    //                Field("Bank Account", req.BankAccount);
                    //                Field("Swift Code", req.SwiftCode);
                    //            });
                    //    });

                    //    sRow.ConstantItem(12);

                    //    // Totals (right)
                    //    sRow.AutoItem().Element(c => PdfHelpers.DrawTotals(c,
                    //        req.Subtotal ?? 0, req.Tax ?? 0,
                    //        req.ShippingCost ?? 0, req.Other ?? 0,
                    //        primary, sym));
                    //});
                    // Totals + Bank Details side by side
                    var totalDiscount = (req.Items ?? []).Where(i => i.Discount.HasValue).Sum(i => i.Discount!.Value);
                    col.Item().PaddingBottom(10).Row(sRow =>
                    {
                        // Bank details (left side)
                        sRow.RelativeItem().Column(left =>
                        {
                            var hasBank = !string.IsNullOrWhiteSpace(req.BeneficiaryName)
                                       || !string.IsNullOrWhiteSpace(req.BankName)
                                       || !string.IsNullOrWhiteSpace(req.BankAccount)
                                       || !string.IsNullOrWhiteSpace(req.SwiftCode);
                            if (!hasBank) return;

                            left.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "Bank Information", accent));
                            left.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10).Column(b =>
                                {
                                    void BankRow(string label, string? val)
                                    {
                                        if (string.IsNullOrWhiteSpace(val)) return;
                                        b.Item().Text(t =>
                                        {
                                            t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                            t.Span(val).FontSize(9).FontColor(Colors.Grey.Darken1);
                                        });
                                    }
                                    BankRow("Beneficiary Name", req.BeneficiaryName);
                                    BankRow("Beneficiary Address", req.BeneficiaryAddress);
                                    BankRow("Bank Name", req.BankName);
                                    BankRow("Bank Address", req.BankAddress);
                                    BankRow("Account Number", req.BankAccount);
                                    BankRow("SWIFT Code", req.SwiftCode);
                                });
                        });

                        sRow.ConstantItem(12); // spacer

                        // Totals (right side)
                        sRow.AutoItem().Element(c => PdfHelpers.DrawTotals(c,
                            req.Subtotal ?? 0, req.Tax ?? 0,
                            req.ShippingCost ?? 0, req.Other ?? 0,
                            primary, sym, totalDiscount));
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

    private static void ComposeItemsTable(IContainer container, FinalInvoicePdfRequest req,
        string primary, string sym)
    {
        var items = req.Items ?? [];

        container.Column(outer =>
        {
            // Header — 10 columns; narrower to fit Track # and Carrier
            outer.Item().Row(hr =>
            {
                string[] headers = ["Ref", "#", "Part No.", "Description", "Qty", "CD", "Cert", "Unit Price", "Total", "Discount", "Track #", "Carrier"];
                float[] widths   = [24,    18,  0,           0,             24,    24,   46,    50,           54,      50,        0,         0];
                float[] rels     = [0,     0,   1.6f,        1.8f,          0,     0,    0,     0,            0,       0,         1.1f,      1.1f];

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

                    Cell(r.ConstantItem(24), it.RfqReference ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(18), (idx + 1).ToString(), Colors.Grey.Darken1);
                    Cell(r.RelativeItem(1.6f), it.PartNumber ?? "—", primary, bold: true);
                    Cell(r.RelativeItem(1.8f), it.Description ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(24), it.Qty.ToString(), primary, bold: true);
                    Cell(r.ConstantItem(24), it.Condition ?? "—", primary);
                    Cell(r.ConstantItem(46), it.Certification ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(50), $"{sym}{PdfHelpers.FormatPrice(it.UnitPrice)}", primary);
                    Cell(r.ConstantItem(54), $"{sym}{PdfHelpers.FormatPrice(it.TotalPrice)}", primary, bold: true);
                    Cell(r.ConstantItem(50), it.Discount.HasValue ? $"-{sym}{PdfHelpers.FormatPrice(it.Discount.Value)}" : "—", it.Discount.HasValue ? "#e53935" : Colors.Grey.Darken1);
                    Cell(r.RelativeItem(1.1f), it.TrackNumber ?? "—", Colors.Grey.Darken1);
                    Cell(r.RelativeItem(1.1f), it.Carrier ?? "—", Colors.Grey.Darken1);
                });
            }
        });
    }
}
