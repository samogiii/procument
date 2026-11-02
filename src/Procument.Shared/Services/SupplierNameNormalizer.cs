using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Procument.Shared.Services;

/// <summary>
/// Makes supplier-name comparisons resilient to casing, repeated whitespace,
/// non-breaking spaces, Unicode presentation variants, and invisible format marks.
/// </summary>
public static class SupplierNameNormalizer
{
    public static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormKC);
        var result = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format)
                continue;

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character);
        }

        return result.ToString();
    }

    public static string Key(string? value) => Clean(value).ToUpperInvariant();

    private static readonly Regex NonAlphaNum = new(@"[^A-Z0-9]+", RegexOptions.Compiled);
    private static readonly HashSet<string> CompanySuffixes = new(StringComparer.Ordinal)
    {
        "INC", "INCORPORATED", "LLC", "LTD", "LIMITED", "CORP", "CORPORATION", "CO", "COMPANY", "GMBH", "AG",
        "SA", "SAS", "SARL", "SRL", "SPA", "BV", "NV", "PLC", "PTE", "PTY", "LLP", "LP", "KG", "OY", "AB", "AS",
    };
    // Suffixes also stripped when typed glued to a number ("Supplier 1inc").
    private static readonly Regex GluedSuffix = new(@"(?<=\d)(INC|LLC|LTD|CORP|GMBH|PLC|LLP)$", RegexOptions.Compiled);

    /// <summary>
    /// A company name for matching only: upper case, "&amp;" = AND, punctuation dropped, a leading THE and trailing
    /// company suffixes (INC, LLC, LTD, CORP, GMBH…) removed, spaces squeezed out. "" when nothing is left.
    /// "Supplier1", "Supplier 1 INC." and "Supplier 1inc" all give "SUPPLIER1".
    /// </summary>
    public static string CompanyKey(string? name)
    {
        var words = NonAlphaNum.Split(Key(name).Replace("&", " AND "))
            .Where(w => w.Length > 0)
            .ToList();
        if (words.Count > 1 && words[0] == "THE") words.RemoveAt(0);
        while (words.Count > 1 && CompanySuffixes.Contains(words[^1])) words.RemoveAt(words.Count - 1);
        if (words.Count > 0) words[^1] = GluedSuffix.Replace(words[^1], "");
        return string.Concat(words);
    }
}
