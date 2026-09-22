using Procument.Module.OurInventory.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>Company letterhead for server-built documents (taken from a Company Preset).</summary>
public sealed record PdfBranding(
    string CompanyName,
    string? Location,
    string? Phone,
    string? Website,
    string? Email,
    string? LogoBase64,
    string Primary,
    string Accent)
{
    public static readonly PdfBranding Plain = new("Our Stock", null, null, null, null, null, "#1a2744", "#2563eb");
}

/// <summary>
/// Goods Receipt Note (GRN) for a Stock PO: what arrived, where it was put, and signature boxes
/// for the warehouse. Built from <see cref="StockReceiptNoteResponse"/>.
/// </summary>
public static class StockReceiptDocument
{
    public static byte[] Generate(StockReceiptNoteResponse note, PdfBranding brand)
    {
        var primary = brand.Primary;
        var accent = brand.Accent;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(20);
                page.MarginHorizontal(30);
                page.MarginBottom(10);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(primary));

                page.Content().Column(col =>
                {
                    col.Item().Element(c => PdfHelpers.DrawPageHeader(c, brand.LogoBase64, brand.CompanyName, brand.Location,
                        brand.Phone, brand.Website, brand.Email, "GOODS RECEIPT", note.NoteNumber, primary));
                    col.Item().Element(c => PdfHelpers.DrawAccentLine(c, primary, accent));

                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        void Meta(string label, string? value) => row.RelativeItem().Text(t =>
                        {
                            t.Span($"{label}: ").Bold().FontSize(9);
                            t.Span(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        Meta("PO", note.PONumber);
                        Meta("PO date", note.PODate?.ToString("dd MMM yyyy"));
                        Meta("Supplier PI", note.SupplierPIRef);
                        Meta("Received", note.ReceivedFrom.Date == note.ReceivedTo.Date
                            ? note.ReceivedTo.ToString("dd MMM yyyy")
                            : $"{note.ReceivedFrom:dd MMM} – {note.ReceivedTo:dd MMM yyyy}");
                    });

                    col.Item().PaddingBottom(12).Row(row =>
                    {
                        PdfHelpers.DrawAddressBox(row.RelativeItem(), "RECEIVED FROM", note.SupplierName, null, null, null, accent);
                        PdfHelpers.DrawAddressBox(row.RelativeItem(), "RECEIVED AT", note.WarehouseNames, null, null, null, accent);
                        PdfHelpers.DrawAddressBox(row.RelativeItem(), "BOOKED BY", note.ReceivedBy, null, null, null, accent);
                    });

                    if (note.IsPartialSelection)
                        col.Item().PaddingBottom(6).Text(t => t.Span("This note covers selected receipts only. \"Total rec.\" shows everything received on the PO so far.")
                            .Italic().FontSize(8).FontColor(Colors.Grey.Darken1));

                    col.Item().PaddingBottom(12).Element(c => ComposeLines(c, note, primary));

                    if (note.Notes.Count > 0)
                        col.Item().PaddingBottom(12).Element(c => PdfHelpers.DrawComments(c, string.Join("\n", note.Notes), accent));

                    col.Item().PaddingTop(16).Row(row =>
                    {
                        foreach (var title in new[] { "Received by", "Checked by (QA)", "Approved by" })
                        {
                            row.RelativeItem().PaddingHorizontal(6).Column(sig =>
                            {
                                sig.Item().Height(42).BorderBottom(0.75f).BorderColor(Colors.Grey.Medium);
                                sig.Item().PaddingTop(3).Text(t => t.Span(title).Bold().FontSize(8));
                                sig.Item().Text(t => t.Span("Name / Signature / Date").FontSize(7).FontColor(Colors.Grey.Medium));
                            });
                        }
                    });
                });

                page.Footer().Element(c => DrawGeneratedFooter(c, $"{note.NoteNumber} · {note.PONumber}", primary));
            });
        }).GeneratePdf();
    }

    private static void ComposeLines(IContainer container, StockReceiptNoteResponse note, string primary)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(22);   // #
                c.RelativeColumn(2.4f); // P/N + description
                c.ConstantColumn(30);   // cond
                c.ConstantColumn(40);   // ordered
                c.ConstantColumn(42);   // this note
                c.ConstantColumn(42);   // total
                c.ConstantColumn(50);   // remaining
                c.RelativeColumn(1.1f); // cert
                c.ConstantColumn(44);   // bin
                c.RelativeColumn(1.6f); // track / serials
            });

            table.Header(h =>
            {
                foreach (var label in new[] { "#", "Part No. / Description", "CD", "Ordered", "This note", "Total rec.", "Remaining", "Cert", "Bin", "Track # / S/N" })
                    h.Cell().Background(primary).Padding(5).AlignCenter()
                        .Text(t => t.Span(label).FontSize(7.5f).Bold().FontColor(Colors.White));
            });

            for (var i = 0; i < note.Lines.Count; i++)
            {
                var line = note.Lines[i];
                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                IContainer Cell() => table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignMiddle();

                Cell().AlignCenter().Text(t => t.Span(line.PORef.ToString()).FontColor(Colors.Grey.Darken1));
                Cell().Column(c =>
                {
                    c.Item().Text(t => t.Span(line.PartNumber).Bold());
                    if (!string.IsNullOrWhiteSpace(line.Description))
                        c.Item().Text(t => t.Span(line.Description).FontSize(7.5f).FontColor(Colors.Grey.Darken1));
                });
                Cell().AlignCenter().Text(line.Condition);
                Cell().AlignCenter().Text(Qty(line.QtyOrdered));
                Cell().AlignCenter().Text(t => t.Span(Qty(line.QtyThisNote)).Bold());
                Cell().AlignCenter().Text(Qty(line.QtyReceivedTotal));
                Cell().AlignCenter().Text(t => t.Span(Qty(line.QtyRemaining))
                    .FontColor(line.QtyRemaining > 0 ? Colors.Orange.Darken2 : Colors.Green.Darken2));
                Cell().AlignCenter().Text(t => t.Span(line.CertName ?? "—").FontSize(8).FontColor(Colors.Grey.Darken1));
                Cell().AlignCenter().Text(t => t.Span(line.BinLocation ?? "—").FontSize(8));
                Cell().Column(c =>
                {
                    if (line.TrackNumbers.Count > 0)
                        c.Item().Text(t => t.Span(string.Join(", ", line.TrackNumbers)).FontSize(7.5f));
                    if (line.Serials.Count > 0)
                        c.Item().Text(t => t.Span($"S/N {string.Join(", ", line.Serials)}").FontSize(7.5f).FontColor(Colors.Grey.Darken1));
                    if (line.TrackNumbers.Count == 0 && line.Serials.Count == 0)
                        c.Item().Text(t => t.Span("manual").FontSize(7.5f).FontColor(Colors.Grey.Medium));
                });
            }
        });
    }

    internal static string Qty(decimal q) => q.ToString("#,##0.##");

    /// <summary>Footer with a reference on the left and "Page x of y" on the right.</summary>
    internal static void DrawGeneratedFooter(IContainer container, string reference, string primary)
    {
        container.BorderTop(1.5f).BorderColor(primary).PaddingVertical(6).Row(row =>
        {
            row.RelativeItem().Text(t => t.Span($"{reference} · generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC")
                .FontSize(7.5f).FontColor(Colors.Grey.Medium));
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Colors.Grey.Medium));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }
}
