using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Catalog.Services;

/// <summary>
/// Part families: a part number, its new name and its catalog alternatives are all one family, and
/// the links chain both ways — X lists Y and Y lists Z puts X, Y and Z in one family (Sam), so a
/// supplier quote for any of them answers an RFQ for any other.
/// </summary>
public static class PartFamilies
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    // Stops a runaway walk on bad catalog data; real families are a handful of part numbers.
    private const int MaxRounds = 10;
    // Filler typed into an Alt box; following it would chain unrelated parts into one family.
    private static readonly HashSet<string> Placeholders = new() { "N/A", "NA", "NONE", "NIL", "SAME", "TBA", "TBD", "-", "--" };

    /// <summary>Too short or filler ("N/A", "SAME") to link parts by.</summary>
    public static bool IsPlaceholder(string? partNumber)
    {
        var key = Key(partNumber);
        return key.Length < 3 || Placeholders.Contains(key) || AltPartNumbers.IsPlaceholder(key);
    }

    /// <summary>Part number compare key: no whitespace, upper case.</summary>
    public static string Key(string? partNumber) => Whitespace.Replace(partNumber ?? "", "").ToUpperInvariant();

    /// <summary>The families of these part numbers, walking the Alternatives table in both directions.</summary>
    public static async Task<PartFamilyMap> LoadAsync(DbContext db, IEnumerable<string?> partNumbers, CancellationToken cancellationToken = default)
    {
        var map = new PartFamilyMap();
        var frontier = new List<string>();
        foreach (var name in partNumbers)
            if (!IsPlaceholder(name) && map.Touch(name)) frontier.Add(name!);

        for (var round = 0; round < MaxRounds && frontier.Count > 0; round++)
        {
            // Trimmed and whitespace-free spellings, like the RFQ item queries.
            var names = frontier.Select(n => n.Trim())
                .Concat(frontier.Select(n => Whitespace.Replace(n, "")))
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var parts = await db.Set<PartNumber>().AsNoTracking()
                .Where(p => names.Contains(p.Name)
                            || (p.NewName != null && names.Contains(p.NewName))
                            || p.Alternatives.Any(a => names.Contains(a.Name)))
                .Select(p => new { p.Name, p.NewName, Alternatives = p.Alternatives.Select(a => a.Name).ToList() })
                .ToListAsync(cancellationToken);

            frontier = new List<string>();
            foreach (var part in parts)
            {
                foreach (var name in new[] { part.Name, part.NewName }.Concat(part.Alternatives))
                {
                    if (IsPlaceholder(name)) continue;
                    if (map.Touch(name)) frontier.Add(name!);
                    map.Join(part.Name, name);
                }
            }
        }
        return map;
    }
}

/// <summary>Part number keys grouped into families (see <see cref="PartFamilies"/>).</summary>
public sealed class PartFamilyMap
{
    private readonly Dictionary<string, string> _parent = new();
    private readonly Dictionary<string, string> _names = new();
    private Dictionary<string, HashSet<string>>? _families;

    /// <summary>Every part number key in the family of this part number, itself included.</summary>
    public IReadOnlySet<string> FamilyOf(string? partNumber)
    {
        var key = PartFamilies.Key(partNumber);
        if (key.Length == 0) return new HashSet<string>();
        _families ??= _parent.Keys.ToList().GroupBy(Root).ToDictionary(g => g.Key, g => g.ToHashSet());
        return _parent.ContainsKey(key) ? _families[Root(key)] : new HashSet<string> { key };
    }

    /// <summary>The catalog spelling of a part number key (the first one seen).</summary>
    public string? NameOf(string key) => _names.GetValueOrDefault(key);

    /// <summary>Every spelling seen, for querying by part number.</summary>
    public IEnumerable<string> Names => _names.Values;

    /// <summary>Adds the part number; true when it was new.</summary>
    internal bool Touch(string? partNumber)
    {
        var key = PartFamilies.Key(partNumber);
        if (key.Length == 0 || _parent.ContainsKey(key)) return false;
        _parent[key] = key;
        _names[key] = partNumber!.Trim();
        _families = null;
        return true;
    }

    internal void Join(string? a, string? b)
    {
        var ka = PartFamilies.Key(a);
        var kb = PartFamilies.Key(b);
        if (ka.Length == 0 || kb.Length == 0) return;
        Touch(a);
        Touch(b);
        var ra = Root(ka);
        var rb = Root(kb);
        if (ra == rb) return;
        _parent[rb] = ra;
        _families = null;
    }

    private string Root(string key)
    {
        while (_parent[key] != key)
        {
            _parent[key] = _parent[_parent[key]];
            key = _parent[key];
        }
        return key;
    }
}
