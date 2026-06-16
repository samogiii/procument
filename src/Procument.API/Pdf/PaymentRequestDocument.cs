using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

public static class PaymentRequestDocument
{
    public static byte[] Generate(PaymentRequestPdfRequest req)
    {
        var sym = req.CurrencySymbol ?? "$";

        var bankFeeText = req.BankFeeOption switch
        {
            "OurCompanyLocal" => "本公司支付本地银行费用，受款人支付海外银行费用。\nOur company pays local bank fees; the recipient pays overseas bank fees.",
            "RecipientAll"    => "受款公司支付所有的银行费用\nThe recipient company pays all bank fees.",
            _                 => "本公司支付所有的银行费用\nOur company pays all bank fees.",
        };

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(30);
                page.MarginHorizontal(40);
                page.MarginBottom(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                // ── HEADER ───────────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("PAYMENT REQUEST").FontSize(20).Bold();
                    col.Item().PaddingTop(6).AlignCenter().Text(req.PrNumber ?? "").FontSize(11);
                    col.Item().PaddingTop(2).AlignCenter().Text(req.DocumentDate ?? DateTime.Now.ToString("yyyy-MM-dd")).FontSize(10);
                    col.Item().PaddingTop(10).LineHorizontal(1f).LineColor(Colors.Black);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    // ── MUST BE PAID BY ──────────────────────────────────────────────
                    col.Item().PaddingBottom(14).Text(text =>
                    {
                        text.Span("MUST BE PAID BY:  ").Bold().FontSize(11);
                        text.Span(req.CompanyPayingFrom ?? "—").Bold().FontSize(11);
                    });

                    // ── BANK DETAILS (two columns) ────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        // Left — Our company
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text("PAYING FROM").Bold().FontSize(9);
                            inner.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });

                                void L(string label, string? value)
                                {
                                    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(label).FontSize(9).Bold();
                                    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(value ?? "—").FontSize(9);
                                }

                                L("Company:", req.CompanyPayingFrom);
                                //L("Beneficiary:", req.OurBeneficiaryName);
                                //L("Account No:", req.OurAccountNumber);
                                //L("Bank Name:", req.OurBankName);
                                //L("SWIFT:", req.OurSwiftCode);
                                //L("Bank Address:", req.OurBankAddress);
                            });
                        });

                        row.ConstantItem(20);

                        // Right — Supplier
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text("PAYING TO").Bold().FontSize(9);
                            inner.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });

                                void R(string label, string? value)
                                {
                                    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(label).FontSize(9).Bold();
                                    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(value ?? "—").FontSize(9);
                                }

                                R("Company:", req.CompanyPayingTo);
                                R("Account No:", req.AccountNumber);
                                R("Bank Name:", req.BankName);
                                R("SWIFT:", req.SwiftCode);
                                R("ABA:", req.ABA);
                                R("Bank Address:", req.BankAddress);
                            });
                        });
                    });

                    // ── TOTAL AMOUNT (big, centered) ──────────────────────────────────
                    col.Item().PaddingTop(40).PaddingBottom(40).AlignCenter().Column(inner =>
                    {
                        inner.Item().AlignCenter().Text("TOTAL AMOUNT").FontSize(13).Bold();
                        inner.Item().PaddingTop(10).AlignCenter()
                            .Text($"{sym}{req.GrandTotal:N2}").FontSize(36).Bold();
                        inner.Item().PaddingTop(6).AlignCenter()
                            .Text(req.Currency ?? "USD").FontSize(12);
                    });

                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                    // ── BANK CHARGES ──────────────────────────────────────────────────
                    col.Item().PaddingTop(14).Text(bankFeeText).FontSize(9).LineHeight(1.5f);
                });

                // ── FOOTER ───────────────────────────────────────────────────────────
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ").FontSize(8);
                    x.CurrentPageNumber().FontSize(8);
                    x.Span(" of ").FontSize(8);
                    x.TotalPages().FontSize(8);
                });
            });
        });

        return doc.GeneratePdf();
    }
}
