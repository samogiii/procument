using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Procument.API.Pdf;

/// <summary>Shared PDF building utilities used by all document generators.</summary>
public static class PdfHelpers
{
    public static string FormatPrice(decimal value) => value.ToString("#,##0.00");

    /// <summary>Two-band gradient-style accent line below the header.</summary>
    public static void DrawAccentLine(IContainer container, string primary, string accent)
    {
        container.PaddingVertical(6).Row(row =>
        {
            row.RelativeItem(4).Height(3).Background(primary);
            row.RelativeItem(3).Height(3).Background(accent);
            row.RelativeItem(3).Height(3).Background(Colors.Grey.Lighten3);
        });
    }

    /// <summary>Logo + company info on left, document title + number on right.</summary>
    public static void DrawPageHeader(
        IContainer container,
        string? logoBase64,
        string? companyName,
        string? location,
        string? phone,
        string? website,
        string? email,
        string? docTitle,
        string? docNumber,
        string primary)
    {
        container.Row(row =>
        {
            row.RelativeItem(3).Column(left =>
            {
                if (!string.IsNullOrEmpty(logoBase64))
                {
                    try
                    {
                        var raw = logoBase64;
                        if (raw.Contains(',')) raw = raw[(raw.IndexOf(',') + 1)..];
                        var bytes = Convert.FromBase64String(raw);
                        left.Item().Height(58).Image(bytes, ImageScaling.FitHeight);
                    }
                    catch { /* bad base64 — skip */ }
                }
                left.Item().PaddingTop(4).Text(t => t.Span(companyName ?? "").Bold().FontSize(16).FontColor(primary));
                if (!string.IsNullOrEmpty(location))
                    left.Item().Text(t => t.Span($"Add: {location}").FontSize(8).FontColor(Colors.Grey.Medium));
                if (!string.IsNullOrEmpty(phone))
                    left.Item().Text(t => t.Span($"Tel: {phone}").FontSize(8).FontColor(Colors.Grey.Medium));
                if (!string.IsNullOrEmpty(website))
                    left.Item().Text(t => t.Span($"Web: {website}").FontSize(8).FontColor(Colors.Grey.Medium));
                if (!string.IsNullOrEmpty(email))
                    left.Item().Text(t => t.Span($"Email: {email}").FontSize(8).FontColor(Colors.Grey.Medium));
            });

            row.RelativeItem(2).AlignRight().Column(right =>
            {
                right.Item().AlignRight().Text(t => t.Span(docTitle).Bold().FontSize(22).FontColor(primary));
                right.Item().AlignRight().PaddingTop(4)
                    .Text(t => t.Span(docNumber ?? "").FontSize(10).FontColor(Colors.Grey.Medium));
            });
        });
    }

    /// <summary>Bordered address box with coloured title label.
    /// <paramref name="appendTitle"/> / <paramref name="appendLines"/> add an optional secondary
    /// block inside the same box (used for FFW under Ship To). When no append line has content the
    /// box renders exactly as before — no label, no blank line.</summary>
    public static void DrawAddressBox(
        IContainer container,
        string title,
        string? name,
        string? address,
        string? extraLine1,
        string? extraLine2,
        string accent,
        string? appendTitle = null,
        IEnumerable<string?>? appendLines = null)
    {
        var appended = appendLines?.Where(l => !string.IsNullOrWhiteSpace(l)).ToList() ?? [];

        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
        {
            c.Item().AlignCenter()
                .Text(t => t.Span(title).Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
            c.Item().AlignCenter().PaddingTop(4)
                .Text(t => t.Span(name ?? "—").Bold().FontSize(10));
            if (!string.IsNullOrEmpty(address))
                c.Item().AlignCenter().PaddingTop(2)
                    .Text(t => t.Span(address).FontSize(9).FontColor(Colors.Grey.Darken1));
            if (!string.IsNullOrEmpty(extraLine1))
                c.Item().AlignCenter().PaddingTop(2)
                    .Text(t => t.Span(extraLine1).FontSize(9).FontColor(Colors.Grey.Darken1));
            if (!string.IsNullOrEmpty(extraLine2))
                c.Item().AlignCenter().PaddingTop(2)
                    .Text(t => t.Span(extraLine2).FontSize(9).FontColor(Colors.Grey.Darken1));

            if (appended.Count == 0) return;

            // The "<br>" — extra top padding on the first appended element instead of an empty row.
            if (!string.IsNullOrWhiteSpace(appendTitle))
                c.Item().AlignCenter().PaddingTop(10)
                    .Text(t => t.Span(appendTitle).Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));

            for (int i = 0; i < appended.Count; i++)
            {
                var pad = i == 0 && string.IsNullOrWhiteSpace(appendTitle) ? 10 : (i == 0 ? 4 : 2);
                c.Item().AlignCenter().PaddingTop(pad).Text(t =>
                {
                    var span = t.Span(appended[i]).FontSize(9);
                    span.FontColor(i == 0 ? Colors.Black : Colors.Grey.Darken1);
                    if (i == 0) span.Bold();
                });
            }
        });
    }

    /// <summary>Section label with a thick left-colour border (like CSS border-left).</summary>
    public static void DrawSectionLabel(IContainer container, string text, string accent)
    {
        container.BorderLeft(4).BorderColor(accent).PaddingLeft(8)
            .Text(t => t.Span(text.ToUpperInvariant()).Bold().FontSize(8)
                .FontColor(accent).LetterSpacing(0.1f));
    }

    /// <summary>Top-bordered footer row with free text + email.</summary>
    public static void DrawFooter(IContainer container, string? footerText, string? email, string primary)
    {
        container.BorderTop(2).BorderColor(primary).PaddingVertical(8).Row(row =>
        {
            row.RelativeItem().Text(t => t.Span(footerText ?? "").FontSize(8).FontColor(Colors.Grey.Medium));
            row.RelativeItem().AlignRight()
                .Text(t => t.Span(email ?? "").FontSize(8).Bold().FontColor(primary));
        });
    }

    /// <summary>Shared totals block (right-aligned, ~220pt wide).
    /// <paramref name="processingFee"/> is rendered as a "Processing Fee" line above Tax;
    /// it's optional so non-PO documents can keep their existing layout untouched.</summary>
    public static void DrawTotals(
        IContainer container,
        decimal subtotal,
        decimal tax,
        decimal shipping,
        decimal other,
        string primary,
        string sym,
        decimal discount = 0,
        decimal processingFee = 0,
        decimal? taxPercent = null)
    {
        var grandTotal = subtotal - discount + tax + shipping + other + processingFee;
        container.Width(220).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(col =>
        {
            void Row(string label, decimal amount, string? bg = null, bool isGrand = false, string? overrideColor = null, string? prefix = null)
            {
                var item = isGrand ? col.Item().Background(primary) : (bg != null ? col.Item().Background(bg) : col.Item());
                item.Padding(6).Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        var s = t.Span(label).FontSize(9).FontColor(isGrand ? Colors.White : Colors.Grey.Darken1);
                        if (isGrand) s.Bold();
                    });
                    r.RelativeItem().AlignRight().Text(t =>
                    {
                        var color = isGrand ? "#fff" : (overrideColor ?? primary);
                        var s = t.Span($"{prefix ?? sym}{FormatPrice(amount)}")
                            .FontSize(isGrand ? 12 : 9)
                            .FontColor(color);
                        if (isGrand) s.Bold();
                    });
                });
            }

            Row("Subtotal", subtotal, Colors.Grey.Lighten5);
            if (discount > 0) Row("Discount", discount, overrideColor: "#e53935", prefix: $"-{sym}");
            
            // Name the rate when the caller computed the tax from one, e.g. "Tax (13%)".
            var taxLabel = taxPercent is > 0
                ? $"Tax ({taxPercent.Value.ToString("0.##", CultureInfo.InvariantCulture)}%)"
                : "Tax";
            Row(taxLabel, tax, Colors.Grey.Lighten5);
            Row("Shipping", shipping);
            if (processingFee > 0) { Row("Processing Fee", processingFee); grandTotal -= other; }
            else
            {
                Row("Processing Fee", other, Colors.Grey.Lighten5);
                grandTotal -= processingFee;
            }
                
            
            Row("Total", grandTotal, isGrand: true);
        });
    }

    /// <summary>Comments box with accent label.</summary>
    public static void DrawComments(IContainer container, string? comments, string accent)
    {
        if (string.IsNullOrWhiteSpace(comments)) return;
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
        {
            c.Item().Text(t => t.Span("COMMENTS").Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
            c.Item().PaddingTop(4).Text(t => t.Span(comments).FontSize(9).FontColor(Colors.Grey.Darken1));
        });
    }

    /// <summary>Terms & Conditions box — grey tinted background, accent label.</summary>
    public static void DrawTerms(IContainer container, string? terms, string accent)
    {
        if (string.IsNullOrWhiteSpace(terms)) return;
        container.Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
        {
            c.Item().Text(t => t.Span("TERMS & CONDITIONS").Bold().FontSize(8).FontColor(accent).LetterSpacing(0.1f));
            c.Item().PaddingTop(4).Text(t => t.Span(terms).FontSize(8).FontColor(Colors.Grey.Darken1));
        });
    }
}
