using System.Text.RegularExpressions;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Whether two quotes of one supplier are the very same offer (Sam 2026-10-26: "if the supplier gives us 2 prices for
/// one part number, do not replace the existing one at all — check CR, condition, serial number, price, all of the
/// fields, and only when they are exactly the same, rewrite it"). Every field the supplier gives must be equal: price,
/// qty, condition, alt P/N, serial number, CR, cert, tag date / type / by, trace to, lead time, warranty, MOQ and price
/// type. Text is compared trimmed and case-insensitive; blank equals blank only. Our own fields (notes, shipping,
/// coefs, sell lead time, cert references) are not the supplier's offer and are not compared.
/// </summary>
public static class QuoteIdentity
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static string PnKey(string? partNumber) => Whitespace.Replace(partNumber ?? "", "").ToUpperInvariant();
    private static string Text(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private static bool SameText(string? a, string? b) => Text(a) == Text(b);
    private static bool SameQty(double? a, double? b) => a == null ? b == null : b != null && Math.Abs(a.Value - b.Value) < 1e-9;

    /// <summary>
    /// An RFQ supplier quote and the line about to be written on that RFQ item: condition / alt / qty as the line
    /// would write them there (the item's condition and qty when the line has none).
    /// </summary>
    public static bool Same(ProcumentRecord quote, PendingSupplierQuote line, string? condition, string? alt, double qty) =>
        line.Price != null && quote.Price == line.Price.Value
        && SameQty(quote.Qty, qty)
        && SameText(quote.Condition, condition)
        && PnKey(quote.Alt) == PnKey(alt)
        && SameDetails(quote.SerialNumber, quote.CR, quote.CertName, quote.TagDate, quote.LeadTime, quote.TaggedBy, quote.TagType,
            quote.TraceTo, quote.Warranty, quote.MoQ, quote.PriceType, line);

    /// <summary>Two lines (e.g. a Procurement Plus line and an Email Quotes line), each alt given against the same part.</summary>
    public static bool Same(PendingSupplierQuote a, string? altA, PendingSupplierQuote b, string? altB) =>
        a.Price == b.Price && SameExceptPrice(a, altA, b, altB);

    /// <summary>Every field but the price (the caller compares the price its own way, e.g. as read from the mail).</summary>
    public static bool SameExceptPrice(PendingSupplierQuote a, string? altA, PendingSupplierQuote b, string? altB) =>
        SameQty(a.Qty, b.Qty)
        && SameText(a.Condition, b.Condition)
        && PnKey(altA) == PnKey(altB)
        && SameDetails(a.SerialNumber, a.CR, a.CertName, a.TagDate, a.LeadTime, a.TaggedBy, a.TagType,
            a.TraceTo, a.Warranty, a.MoQ, a.PriceType, b);

    /// <summary>
    /// An RFQ supplier quote's offer as one key: two quotes of one RFQ item with the same key are a duplicate (Sam
    /// 2026-10-28: "if all values are the same" — the supplier email is not part of it).
    /// </summary>
    public static string Key(ProcumentRecord q) => string.Join("\u001f",
        q.SupplierId,
        q.Price.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
        q.Qty.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
        Text(q.Condition), PnKey(q.Alt), PnKey(q.SerialNumber), Text(q.CR), Text(q.CertName),
        q.TagDate?.ToString("yyyy-MM-dd") ?? "", Text(q.LeadTime), Text(q.TaggedBy), Text(q.TagType),
        Text(q.TraceTo), Text(q.Warranty), Text(q.MoQ), Text(q.PriceType));

    private static bool SameDetails(string? serial, string? cr, string? cert, DateOnly? tagDate, string? leadTime, string? taggedBy,
        string? tagType, string? traceTo, string? warranty, string? moq, string? priceType, PendingSupplierQuote line) =>
        PnKey(serial) == PnKey(line.SerialNumber)
        && SameText(cr, line.CR)
        && SameText(cert, line.CertName)
        && tagDate == line.TagDate
        && SameText(leadTime, line.LeadTime)
        && SameText(taggedBy, line.TaggedBy)
        && SameText(tagType, line.TagType)
        && SameText(traceTo, line.TraceTo)
        && SameText(warranty, line.Warranty)
        && SameText(moq, line.MoQ)
        && SameText(priceType, line.PriceType);
}
