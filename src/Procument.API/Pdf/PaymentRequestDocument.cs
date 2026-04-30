using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

public static class PaymentRequestDocument
{
    public static byte[] Generate(PaymentRequestPdfRequest req)
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
                page.MarginBottom(20);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("Payment Request").FontSize(20).Bold().FontColor(primary);
                    col.Item().PaddingTop(10).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Row(row =>
                    {
                        row.RelativeItem().Padding(5).Row(r => {
                            r.RelativeItem().Text("PR Number:").Bold();
                            r.RelativeItem().Text(req.PrNumber ?? "—");
                        });
                        row.RelativeItem().Padding(5).Row(r => {
                            r.RelativeItem().Text("Date:").Bold();
                            r.RelativeItem().Text(req.DocumentDate ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                        });
                    });
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    // BANK INFORMATION
                    col.Item().Text("BANK INFORMATION").FontSize(14).Bold().FontColor(primary);
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        void AddRow(string label, string? value)
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(5).Text(label).Bold();
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value ?? "—");
                        }

                        AddRow("Company Paying From:", req.CompanyPayingFrom);
                        AddRow("Company Paying To:", req.CompanyPayingTo);
                        AddRow("Account Number:", req.AccountNumber);
                        AddRow("Bank Name:", req.BankName);
                        AddRow("SWIFT Code:", req.SwiftCode);
                        AddRow("ABA (Routing Number):", req.ABA);
                        AddRow("Company Address:", req.CompanyAddress);
                        AddRow("Bank Address:", req.BankAddress);
                    });

                    // PAYMENT DETAILS
                    col.Item().PaddingTop(20).Text("PAYMENT DETAILS").FontSize(14).Bold().FontColor(primary);
                    col.Item().PaddingTop(5).Text("Selected Parts:").FontSize(10).Italic();

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(primary).Padding(5).AlignCenter().Text("Part Number").FontColor(Colors.White).Bold();
                            header.Cell().Background(primary).Padding(5).AlignCenter().Text("Description").FontColor(Colors.White).Bold();
                            header.Cell().Background(primary).Padding(5).AlignCenter().Text("Quantity").FontColor(Colors.White).Bold();
                            header.Cell().Background(primary).Padding(5).AlignCenter().Text("Unit Price").FontColor(Colors.White).Bold();
                            header.Cell().Background(primary).Padding(5).AlignCenter().Text("Total").FontColor(Colors.White).Bold();
                        });

                        foreach (var item in req.Items ?? new List<PaymentRequestPdfItem>())
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(item.PartNumber);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(item.Description);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(item.Qty.ToString());
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text($"{sym}{item.UnitPrice:N2}");
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text($"{sym}{item.TotalPrice:N2}");
                        }
                    });

                    // Summary block
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        void AddSummaryRow(string label, string? value)
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(label).Bold();
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value ?? "—");
                        }

                        AddSummaryRow("Supplier:", req.SupplierName);
                        AddSummaryRow("Items Total:", $"{sym}{req.ItemsTotal:N2}");
                        AddSummaryRow("Wire Fee:", $"{sym}{req.WireFee:N2}");
                        AddSummaryRow("Currency:", req.Currency);
                        AddSummaryRow("Supplier PO:", req.PoNumber);
                    });

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });
                        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Status:").Bold();
                        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(req.Status ?? "PENDING APPROVAL").Bold();
                    });

                    // Final Total
                    col.Item().PaddingTop(20).Background(primary).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL AMOUNT (Items + Wire Fee):").Bold().FontColor(Colors.White);
                        row.RelativeItem().AlignRight().Text($"{sym}{req.GrandTotal:N2} ({req.Currency})").Bold().FontColor(Colors.White);
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }
}
