using System.Text.RegularExpressions;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Derives the lead time we quote to the customer (Sell LeadTime) from the supplier's lead time
/// (Buy LeadTime). Stock counts as 0 weeks, days round up to whole weeks, months count as 4 weeks.
///   Default:          A-B weeks sells as (A+1)-(B+2) weeks — Stock → 1-2, 1 → 2-3, 1-2 → 2-4, 3-5 → 4-7.
///   Base 1 customers: A-B weeks sells as (A+4)-(B+4) weeks — Stock → 4, 1 → 5, 1-3 → 5-7.
/// Mirrors client/app/utils/leadTime.ts.
/// </summary>
public static class LeadTimeRules
{
    private static readonly Regex StockPattern = new(@"\b(stock|stk)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RangePattern = new(
        @"(\d+(?:\.\d+)?)\s*(?:(?:-|–|to|~)\s*(\d+(?:\.\d+)?))?\s*(days?|d|weeks?|wks?|w|months?|mos?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NoStockPattern = new(@"\b(no|not\s+in|out\s+of|zero|nil)\s+(stock|stk)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Buy lead time as it arrives from MailFlow: any stock wording (STOCK, stk, ex stock…) becomes
    /// "Stock"; "no stock" means the line was declined, so there is no lead time (null).
    /// </summary>
    public static string? NormalizeImported(string? leadTime)
    {
        var text = leadTime?.Trim();
        if (string.IsNullOrEmpty(text) || NoStockPattern.IsMatch(text)) return null;
        return StockPattern.IsMatch(text) ? "Stock" : text;
    }

    /// <summary>Returns the suggested Sell LeadTime, or null when the buy lead time can't be parsed.</summary>
    public static string? ToSellLeadTime(string? buyLeadTime, int? customerBase)
    {
        if (string.IsNullOrWhiteSpace(buyLeadTime)) return null;
        if (StockPattern.IsMatch(buyLeadTime)) return Format(0, 0, customerBase);

        var m = RangePattern.Match(buyLeadTime);
        if (!m.Success) return null;

        var unit = m.Groups[3].Value.ToLowerInvariant();
        var from = ToWeeks(double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), unit);
        var to = m.Groups[2].Success
            ? ToWeeks(double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), unit)
            : from;
        return Format(Math.Min(from, to), Math.Max(from, to), customerBase);
    }

    /// <summary>
    /// True when <paramref name="sellLeadTime"/> is what some rule would have suggested for
    /// <paramref name="buyLeadTime"/> — i.e. it was auto-filled, not typed by the user. Checks every
    /// rule so values derived before a customer's base rule changed still count as auto-filled.
    /// </summary>
    public static bool IsAutoSellLeadTime(string? sellLeadTime, string? buyLeadTime) =>
        sellLeadTime == ToSellLeadTime(buyLeadTime, null) || sellLeadTime == ToSellLeadTime(buyLeadTime, 1);

    private static int ToWeeks(double value, string unit) => unit[0] switch
    {
        'd' => (int)Math.Ceiling(value / 7),
        'm' => (int)Math.Ceiling(value * 4),
        _ => (int)Math.Ceiling(value),
    };

    private static string Format(int fromWeeks, int toWeeks, int? customerBase)
    {
        if (customerBase == 1)
        {
            var a = fromWeeks + 4;
            var b = toWeeks + 4;
            return a == b ? $"{a} Weeks" : $"{a}-{b} Weeks";
        }
        return $"{fromWeeks + 1}-{toWeeks + 2} Weeks";
    }
}
