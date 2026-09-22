using Procument.Module.OurInventory.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>
/// Stock report / valuation: every lot matching the page filters, grouped by owner company and
/// warehouse, with subtotals. Cost columns only appear when the caller may see cost.
/// </summary>
public static class StockReportDocument
{
    public static byte[] Generate(StockValuationResponse report, PdfBranding brand, string? filterLabel)
    {
        var primary = brand.Primary;
        var accent = brand.Accent;
        var withCost = report.IncludesCost;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginTop(18);
                page.MarginHorizontal(24);
                page.MarginBottom(10);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(primary));

                page.Header().Column(col =>
                {
                    col.Item().Element(c => PdfHelpers.DrawPageHeader(c, brand.LogoBase64, brand.CompanyName, brand.Location,
                        brand.Phone, brand.Website, brand.Email, withCost ? "STOCK VALUATION" : "STOCK REPORT",
                        $"As of {report.GeneratedAt:dd MMM yyyy HH:mm} UTC", primary));
                    col.Item().Element(c => PdfHelpers.DrawAccentLine(c, primary, accent));
                });

                page.Content().Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(filterLabel))
                        col.Item().PaddingBottom(6).Text(t =>
                        {
                            t.Span("Filters: ").Bold().FontSize(8);
                            t.Span(filterLabel).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });

                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        void Stat(string label, string value) => row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(6).Column(c =>
                            {
                                c.Item().Text(t => t.Span(label.ToUpperInvariant()).FontSize(7).Bold().FontColor(accent));
                                c.Item().Text(t => t.Span(value).FontSize(12).Bold());
                            });
                        Stat("Lots", report.LotCount.ToString("#,##0"));
                        Stat("On hand", StockReceiptDocument.Qty(report.TotalQtyOnHand));
                        Stat("Reserved", StockReceiptDocument.Qty(report.TotalQtyReserved));
                        Stat("Available", StockReceiptDocument.Qty(report.TotalQtyOnHand - report.TotalQtyReserved));
                        if (withCost) Stat("Value", $"${PdfHelpers.FormatPrice(report.TotalValue ?? 0)}");
                    });

                    if (report.Groups.Count == 0)
                        col.Item().PaddingTop(20).AlignCenter().Text(t => t.Span("No stock matches these filters.").FontColor(Colors.Grey.Medium));

                    foreach (var group in report.Groups)
                    {
                        col.Item().PaddingTop(8).PaddingBottom(4).Element(c =>
                            PdfHelpers.DrawSectionLabel(c, $"{group.CompanyPresetName} — {group.WarehouseName}", accent));
                        col.Item().Element(c => ComposeGroup(c, group, withCost, primary));
                    }
                });

                page.Footer().Element(c => StockReceiptDocument.DrawGeneratedFooter(c, withCost ? "Stock valuation" : "Stock report", primary));
            });
        }).GeneratePdf();
    }

    private static void ComposeGroup(IContainer container, StockValuationGroup group, bool withCost, string primary)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.6f); // P/N
                c.RelativeColumn(2.4f); // description
                c.ConstantColumn(34);   // cond
                c.RelativeColumn(1f);   // cert
                c.ConstantColumn(50);   // bin
                c.ConstantColumn(50);   // on hand
                c.ConstantColumn(50);   // reserved
                c.ConstantColumn(54);   // available
                c.ConstantColumn(40);   // min
                if (withCost)
                {
                    c.ConstantColumn(64); // avg cost
                    c.ConstantColumn(74); // value
                }
            });

            var headers = new List<string> { "Part No.", "Description", "CD", "Cert", "Bin", "On hand", "Reserved", "Available", "Min" };
            if (withCost) headers.AddRange(["Avg cost", "Value"]);
            table.Header(h =>
            {
                foreach (var label in headers)
                    h.Cell().Background(primary).Padding(4).AlignCenter()
                        .Text(t => t.Span(label).FontSize(7.5f).Bold().FontColor(Colors.White));
            });

            for (var i = 0; i < group.Items.Count; i++)
            {
                var item = group.Items[i];
                var bg = item.IsLowStock ? Colors.Orange.Lighten5 : (i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5);
                IContainer Cell() => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignMiddle();

                Cell().Text(t => t.Span(item.PartNumber).Bold());
                Cell().Text(t => t.Span(item.Description ?? "").FontSize(7.5f).FontColor(Colors.Grey.Darken1));
                Cell().AlignCenter().Text(item.Condition);
                Cell().AlignCenter().Text(t => t.Span(item.CertName ?? "—").FontSize(7.5f));
                Cell().AlignCenter().Text(t => t.Span(item.BinLocation ?? "—").FontSize(7.5f));
                Cell().AlignRight().Text(StockReceiptDocument.Qty(item.QtyOnHand));
                Cell().AlignRight().Text(StockReceiptDocument.Qty(item.QtyReserved));
                Cell().AlignRight().Text(t => t.Span(StockReceiptDocument.Qty(item.QtyAvailable)).Bold()
                    .FontColor(item.IsLowStock ? Colors.Orange.Darken3 : primary));
                Cell().AlignRight().Text(t => t.Span(item.MinQty.HasValue ? StockReceiptDocument.Qty(item.MinQty.Value) : "—").FontColor(Colors.Grey.Darken1));
                if (withCost)
                {
                    Cell().AlignRight().Text($"${PdfHelpers.FormatPrice(item.AvgUnitCost ?? 0)}");
                    Cell().AlignRight().Text(t => t.Span($"${PdfHelpers.FormatPrice(item.TotalAmount ?? 0)}").Bold());
                }
            }

            // Subtotal row
            IContainer Style(IContainer c) => c.Background(Colors.Grey.Lighten4).BorderTop(1).BorderColor(primary).Padding(3).AlignMiddle();
            IContainer Total() => Style(table.Cell());
            Style(table.Cell().ColumnSpan(5)).AlignRight().Text(t => t.Span($"Subtotal · {group.Items.Count} lot(s)").Bold());
            Total().AlignRight().Text(t => t.Span(StockReceiptDocument.Qty(group.QtyOnHand)).Bold());
            Total().AlignRight().Text(t => t.Span(StockReceiptDocument.Qty(group.QtyReserved)).Bold());
            Total().AlignRight().Text(t => t.Span(StockReceiptDocument.Qty(group.QtyOnHand - group.QtyReserved)).Bold());
            Total().Text("");
            if (withCost)
            {
                Total().Text("");
                Total().AlignRight().Text(t => t.Span($"${PdfHelpers.FormatPrice(group.Value ?? 0)}").Bold());
            }
        });
    }
}
