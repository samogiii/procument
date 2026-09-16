using System.Globalization;
using System.Text;

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
}
