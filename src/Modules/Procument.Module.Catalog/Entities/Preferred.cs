using Procument.Shared.Entities;

namespace Procument.Module.Catalog.Entities;

/// <summary>
/// Procurement Plus "preferreds" — parts a user works on and suppliers we prefer — kept in their own
/// table. Neither has to exist in the catalog: the name, alternatives, emails and phone live here.
/// A catalog part / supplier can be linked (optional); a supplier needs one only when its prices are
/// written to RFQs (Done), since RFQ supplier quotes point at a catalog supplier.
/// </summary>
public class Preferred : BaseEntity
{
    /// <summary><see cref="PreferredKinds"/>: Part, Supplier, Rfq or PpePart.</summary>
    public string Kind { get; set; } = PreferredKinds.Part;
    /// <summary>
    /// The user whose row it is: parts / RFQs = their working list; suppliers = their own preferred
    /// suppliers (each user has their own list, Sam 2026-10-25); PpePart = the assigned PPE.
    /// </summary>
    public long? UserId { get; set; }
    /// <summary>Part number, or supplier / company name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Parts: upper-case P/N without whitespace. Suppliers: SupplierNameNormalizer.Key.</summary>
    public string NameKey { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Parts: alternative P/Ns, comma-separated.</summary>
    public string? Alternatives { get; set; }
    /// <summary>Suppliers: email addresses, comma-separated (matched exactly and by company domain).</summary>
    public string? Emails { get; set; }
    public string? Phone { get; set; }
    /// <summary>Suppliers: how we ask for a quote — see <see cref="PreferredSupplierMethods"/>.</summary>
    public string? SourceMethod { get; set; }
    public string? Note { get; set; }
    /// <summary>Optional catalog part (its new name and alternatives also match).</summary>
    public long? PartNumberId { get; set; }
    /// <summary>Optional catalog supplier — required only to write prices onto RFQs.</summary>
    public long? SupplierId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public long? AddedByUserId { get; set; }
    /// <summary>Parts: removed from the working list (can be shown again); null = shown.</summary>
    public DateTime? HiddenAt { get; set; }

    /// <summary>Parts: how it is sourced — <see cref="PreferredTracks"/> (ILS files, a Project, or WeChat).</summary>
    public string Track { get; set; } = PreferredTracks.Ils;
    /// <summary>Parts: quantity / condition we need (typed for custom parts, else from the RFQ item).</summary>
    public double? Qty { get; set; }
    public string? Condition { get; set; }
    /// <summary>Parts: the price we aim for — required for Project and WeChat parts.</summary>
    public decimal? TargetPrice { get; set; }
    /// <summary>Rfq rows: the RFQ on the user's "My RFQs" list (By RFQ tab). Plain id — RFQs live in another module.</summary>
    public long? RfqId { get; set; }
    /// <summary>
    /// PpePart rows: the user's folders the part is in (<see cref="PreferredKinds.PpeFolder"/> ids, comma-separated);
    /// null = only in the public folder (every part is listed there).
    /// </summary>
    public string? FolderIds { get; set; }

    public PartNumber? CatalogPart { get; set; }
    public Supplier? Supplier { get; set; }
}

public static class PreferredTracks
{
    /// <summary>Sourced from uploaded ILS / Excel availability files.</summary>
    public const string Ils = "ILS";
    /// <summary>Nobody lists it — worked on as a project (custom suppliers, tracing timeline).</summary>
    public const string Project = "Project";
    /// <summary>Sourced over WeChat (custom suppliers, tracing timeline).</summary>
    public const string WeChat = "WeChat";

    public static readonly string[] All = [Ils, Project, WeChat];

    public static string Normalize(string? track) =>
        All.FirstOrDefault(t => string.Equals(t, track?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Ils;

    /// <summary>Project and WeChat parts need a target price.</summary>
    public static bool NeedsTargetPrice(string track) => track is Project or WeChat;
}

/// <summary>One entry in a preferred part's tracing timeline: a note the user typed, or something that happened.</summary>
public class PreferredEvent : BaseEntity
{
    public long PreferredId { get; set; }
    public long? UserId { get; set; }
    /// <summary>Note (typed) or a system event: Added, Track, Target, Supplier, Removed, Done, Sourcing.</summary>
    public string Kind { get; set; } = "Note";
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Preferred Preferred { get; set; } = null!;
}

public static class PreferredKinds
{
    public const string Part = "Part";
    public const string Supplier = "Supplier";
    /// <summary>An RFQ a user keeps on their "My RFQs" list (By RFQ tab).</summary>
    public const string Rfq = "Rfq";
    /// <summary>
    /// A part assigned to a user (the PPE — Procurement Part Expert) from Email RFQs; listed on the new
    /// Procurement Plus page (PpeService). Procurement ProMax only reads <see cref="Part"/> rows.
    /// </summary>
    public const string PpePart = "PpePart";
    /// <summary>A user's own folder on the new Procurement Plus page (Name = folder name); parts list it in FolderIds.</summary>
    public const string PpeFolder = "PpeFolder";
}

public static class PreferredSupplierMethods
{
    public const string Email = "Email";
    public const string Search = "Search";
    public const string Ils = "ILS";

    /// <summary>WeChat is not a sending method — it is a part track (<see cref="PreferredTracks.WeChat"/>).</summary>
    public static readonly string[] All = [Email, Search, Ils];

    /// <summary>A known method (case-insensitive), else Email.</summary>
    public static string Normalize(string? method) =>
        All.FirstOrDefault(m => string.Equals(m, method?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Email;

    /// <summary>
    /// Every method a supplier line is searched by (a line can be on ILS, emailed and searched at once):
    /// the known ones, comma-joined in a fixed order ("ILS,Email,Search"); none = null.
    /// </summary>
    public static string? Join(IEnumerable<string?>? methods)
    {
        var picked = (methods ?? [])
            .SelectMany(m => (m ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(m => All.FirstOrDefault(a => string.Equals(a, m, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .ToHashSet();
        return picked.Count == 0 ? null : string.Join(",", new[] { Ils, Email, Search }.Where(picked.Contains));
    }
}

/// <summary>
/// A company name as written in an availability file (ILS export, Excel) linked to a catalog
/// supplier by the user, so the same name matches automatically next time.
/// </summary>
public class SupplierAlias : BaseEntity
{
    /// <summary>SupplierNameNormalizer.Key of the name — unique.</summary>
    public string AliasKey { get; set; } = string.Empty;
    /// <summary>The name as first written.</summary>
    public string Alias { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedByUserId { get; set; }

    public Supplier Supplier { get; set; } = null!;
}
