using System.Text.RegularExpressions;

namespace Procument.Shared.Services;

/// <summary>
/// Alt P/N filler: users type "-", "_" or "no" (and the like) into an Alt box to mean
/// "no alternative" (Sam). Those are never stored as an Alt or added to a part family.
/// </summary>
public static class AltPartNumbers
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    // Compared upper case without whitespace, so "no alt" = "NOALT".
    private static readonly HashSet<string> Placeholders = new() { "NO", "N/A", "NA", "NONE", "NOALT", "SAME" };
    private static readonly char[] ListSeparators = { ',', ';', '\n' };

    /// <summary>Blank, only dashes / underscores ("-", "__"), or a "no alternative" word.</summary>
    public static bool IsPlaceholder(string? alt)
    {
        var key = Whitespace.Replace(alt ?? "", "").ToUpperInvariant();
        return key.Length == 0 || key.All(c => c is '-' or '_') || Placeholders.Contains(key);
    }

    /// <summary>The trimmed Alt P/N, or null when it is blank or filler.</summary>
    public static string? Clean(string? alt) => IsPlaceholder(alt) ? null : alt!.Trim();

    /// <summary>A comma / semicolon separated Alt list without the filler entries; null when none are left.</summary>
    public static string? CleanList(string? alternatives)
    {
        if (alternatives == null) return null;
        var parts = alternatives.Split(ListSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!parts.Any(IsPlaceholder)) return alternatives;
        var kept = parts.Where(p => !IsPlaceholder(p)).ToList();
        return kept.Count == 0 ? null : string.Join(", ", kept);
    }
}
