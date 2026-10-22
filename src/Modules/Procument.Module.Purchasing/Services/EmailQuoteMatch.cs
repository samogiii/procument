using System.Text.RegularExpressions;
using Procument.Module.Catalog.Services;
using Procument.Module.Purchasing.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// A Procurement Plus line and an Email Quotes line (MailFlow) for the same supplier, part and condition —
/// the supplier quoted us by mail for a line added from an ILS / Excel file. Used when an email is imported
/// (the line takes the email's price) and when a part card is built (the line is flagged "quoted by email").
/// </summary>
public static class EmailQuoteMatch
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static string PnKey(string? partNumber) => Whitespace.Replace(partNumber ?? "", "").ToUpperInvariant();
    private static string CondKey(string? condition) => (condition ?? "").Trim().ToUpperInvariant();

    /// <summary>The part numbers a line is for: its P/N and the alternative it offers.</summary>
    public static IEnumerable<string> PartKeys(PendingSupplierQuote q) =>
        new[] { q.PartNumber, q.Alt }.Select(PnKey).Where(k => k.Length > 0);

    /// <summary>Same catalog supplier, same address, same company domain (not free mail), or same name.</summary>
    public static bool SameSupplier(PendingSupplierQuote a, PendingSupplierQuote b)
    {
        if (a.SupplierId != null && a.SupplierId == b.SupplierId) return true;
        var ea = SupplierEmails.Normalize(a.SupplierEmail);
        var eb = SupplierEmails.Normalize(b.SupplierEmail);
        if (ea != null && eb != null)
        {
            if (ea == eb) return true;
            var domain = SupplierEmails.Domain(ea);
            if (!SupplierEmails.IsGenericDomain(domain) && domain == SupplierEmails.Domain(eb)) return true;
        }
        var na = SupplierNameNormalizer.Key(a.SupplierNameRaw);
        return na.Length > 0 && na == SupplierNameNormalizer.Key(b.SupplierNameRaw);
    }

    /// <summary>The Procurement Plus line and the email line are the same supplier offering the same part in the same condition.</summary>
    public static bool Matches(PendingSupplierQuote line, PendingSupplierQuote email) =>
        CondKey(line.Condition) == CondKey(email.Condition)
        && PartKeys(line).Intersect(PartKeys(email)).Any()
        && SameSupplier(line, email);

    /// <summary>
    /// The email's quote onto the line: its price always (the mail is newer than the file), the other
    /// details only where the line has none yet.
    /// </summary>
    public static void CopyInto(PendingSupplierQuote line, PendingSupplierQuote email)
    {
        if (email.Price != null)
        {
            line.Price = email.Price;
            line.PriceRaw = email.PriceRaw;
        }
        line.SupplierId ??= email.SupplierId;
        line.Qty ??= email.Qty;
        line.CertName ??= email.CertName;
        line.TagDate ??= email.TagDate;
        line.TagDateRaw ??= email.TagDateRaw;
        line.LeadTime ??= email.LeadTime;
        line.TaggedBy ??= email.TaggedBy;
        line.TagType ??= email.TagType;
        line.TraceTo ??= email.TraceTo;
        line.SerialNumber ??= email.SerialNumber;
        line.Warranty ??= email.Warranty;
        line.CR ??= email.CR;
        line.MoQ ??= email.MoQ;
        line.PriceType ??= email.PriceType;
        line.Remark ??= email.Remark;
    }
}
