using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// Proforma Invoice PDF.
/// Colors come from the selected Company Preset (PrimaryColor / AccentColor).
/// Signature section: Bank Details block below the items table.
/// Unique columns: Lead Time, Delivery Date.
/// </summary>
public static class InvoiceDocument
{
    public static byte[] Generate(InvoicePdfRequest req)
    {
        var primary = req.PrimaryColor ?? "#0f766e";
        var accent  = req.AccentColor  ?? "#10b981";
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
                        "PROFORMA INVOICE", req.InvoiceNumber, primary));

                    // Accent line
                    col.Item().Element(c => PdfHelpers.DrawAccentLine(c, primary, accent));

                    // Meta row: Date | Due Date | Customer PO | Currency
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        void Meta(string label, string? val)
                            => row.RelativeItem().AlignLeft().Text(t =>
                            {
                                t.Span($"{label}: ").Bold().FontSize(9).FontColor(primary);
                                t.Span(val ?? "—").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        Meta("Date", req.InvoiceDate);
                        Meta("Ship Date", req.DueDate);
                        Meta("Customer PO", req.CustomerPONumber);
                        //Meta("Status", req.Status);
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
                                    bc.Item().Text(req.CustomerName).Bold().FontSize(11).FontColor(primary);
                                    if (!string.IsNullOrWhiteSpace(req.CustomerBillTo))
                                    {
                                        bc.Item().PaddingTop(4).Text(req.CustomerBillTo).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (!string.IsNullOrWhiteSpace(req.CustomerContactPerson))
                                    {
                                        bc.Item().PaddingTop(2).Text($"Contact: {req.CustomerContactPerson}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (!string.IsNullOrWhiteSpace(req.CustomerBillToEmail))
                                    {
                                        bc.Item().PaddingTop(2).Text($"Email: {req.CustomerBillToEmail}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (!string.IsNullOrWhiteSpace(req.CustomerBillToPhone))
                                    {
                                        bc.Item().PaddingTop(2).Text($"Phone: {req.CustomerBillToPhone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                });
                        });

                        // Ship To
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Element(c => PdfHelpers.DrawSectionLabel(c, "SHIP TO", accent));
                            col.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10).Column(sc =>
                                {
                                    sc.Item().Text(req.CustomerName).Bold().FontSize(11).FontColor(primary);
                                    if (!string.IsNullOrWhiteSpace(req.CustomerShipTo))
                                    {
                                        sc.Item().PaddingTop(4).Text(req.CustomerShipTo).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (!string.IsNullOrWhiteSpace(req.CustomerShipToContactPerson))
                                    {
                                        sc.Item().PaddingTop(2).Text($"Contact: {req.CustomerShipToContactPerson}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (!string.IsNullOrWhiteSpace(req.CustomerShipToEmail))
                                    {
                                        sc.Item().PaddingTop(2).Text($"Email: {req.CustomerShipToEmail}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (!string.IsNullOrWhiteSpace(req.CustomerShipToPhone))
                                    {
                                        sc.Item().PaddingTop(2).Text($"Phone: {req.CustomerShipToPhone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                    if (!string.IsNullOrWhiteSpace(req.CustomerShipToAccount))
                                    {
                                        sc.Item().PaddingTop(2).Text($"Account: {req.CustomerShipToAccount}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                });
                        });
                    });

                    // Items table
                    col.Item().PaddingBottom(10).Element(c => ComposeItemsTable(c, req, primary, accent, sym));

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
                            req.Shipping ?? 0, req.Other ?? 0,
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

    private static void ComposeItemsTable(IContainer container, InvoicePdfRequest req,
        string primary, string accent, string sym)
    {
        var items = req.Items ?? [];

        container.Column(outer =>
        {
            // Header row
            outer.Item().Row(hr =>
            {
                string[] headers = ["Ref", "#", "Part No.", "Description", "Qty", "CD", "Cert", "Unit Price", "Total", "Discount", "Delivery"];
                float[] widths   = [26,    20,  0,           0,             26,    26,   55,    55,           58,    55,        58];
                float[] rels     = [0,     0,   2.2f,        2.2f,          0,     0,    0,     0,            0,     0,         0];

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
                    void Cell(IContainer cell, string text, string? color = null, bool bold = false)
                        => cell.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                            .Padding(5).AlignCenter().AlignMiddle().Text(t =>
                            {
                                var s = t.Span(text).FontSize(8.5f);
                                if (color != null) s.FontColor(color);
                                if (bold) s.Bold();
                            });

                    Cell(r.ConstantItem(26), it.RfqReference ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(20), (idx + 1).ToString(), Colors.Grey.Darken1);
                    Cell(r.RelativeItem(2.2f), it.PartNumberName ?? "—", primary, bold: true);
                    Cell(r.RelativeItem(2.2f), it.Description ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(26), it.Qty.ToString(), primary, bold: true);
                    Cell(r.ConstantItem(26), it.Condition ?? "—", primary);
                    Cell(r.ConstantItem(55), it.CertName ?? "—", Colors.Grey.Darken1);
                    Cell(r.ConstantItem(55), $"{sym}{PdfHelpers.FormatPrice(it.UnitPrice)}", primary);
                    Cell(r.ConstantItem(58), $"{sym}{PdfHelpers.FormatPrice(it.TotalPrice)}", primary, bold: true);
                    Cell(r.ConstantItem(55), it.Discount.HasValue ? $"-{sym}{PdfHelpers.FormatPrice(it.Discount.Value)}" : "—", it.Discount.HasValue ? "#e53935" : Colors.Grey.Darken1);
                    Cell(r.ConstantItem(58), it.LeadTime ?? "—", Colors.Grey.Darken1);
                });
            }
        });
    }
}
