using System.Net.Mail;
using System.Text.Json.Nodes;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.Catalog.Services;

/// <summary>
/// Supplier email helpers: normalising addresses, matching suppliers by email domain, and
/// adding addresses to a supplier's main Email / Contacts JSON
/// (<c>[{name, email, emails?: string[], phone?, title?}]</c> — <c>email</c> stays the first
/// address so older readers keep working, <c>emails</c> holds all of them).
/// </summary>
public static class SupplierEmails
{
    // Free-mail providers are shared by unrelated people, so their domain never identifies a supplier.
    private static readonly HashSet<string> GenericDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "msn.com", "me.com", "mac.com", "qq.com", "163.com", "126.com",
        "sina.com", "foxmail.com", "naver.com", "rediffmail.com", "inbox.ru", "bk.ru", "list.ru", "rambler.ru",
        "tutanota.com", "fastmail.com", "hey.com", "web.de", "t-online.de", "libero.it", "orange.fr", "free.fr",
    };

    private static readonly HashSet<string> GenericProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "yahoo", "ymail", "hotmail", "outlook", "live", "aol", "icloud", "gmx", "yandex", "mail",
        "protonmail", "proton", "zoho",
    };

    /// <summary>Trimmed, lower-cased address, or null when it is not a valid email.</summary>
    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var trimmed = email.Trim().ToLowerInvariant();
        if (trimmed.Count(c => c == '@') != 1 || trimmed.Any(char.IsWhiteSpace)) return null;
        if (!MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed) return null;
        var domain = Domain(trimmed);
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.') ? trimmed : null;
    }

    public static string Domain(string email) => email[(email.LastIndexOf('@') + 1)..].Trim().ToLowerInvariant();

    public static bool IsGenericDomain(string domain) =>
        GenericDomains.Contains(domain) || GenericProviders.Contains(domain.Split('.')[0]);

    /// <summary>Every address on the supplier — main Email (may hold several, comma/semicolon separated) and contacts.</summary>
    public static List<string> AllEmails(Supplier supplier) => AllEmails(supplier.Email, supplier.Contacts);

    /// <summary>
    /// Every address in a main-email field and a contacts JSON (same shape for suppliers and customers),
    /// plus any extra address fields (comma/semicolon separated).
    /// </summary>
    public static List<string> AllEmails(string? mainEmail, string? contactsJson, params string?[] extraEmails)
    {
        var result = SplitEmails(mainEmail);
        foreach (var extra in extraEmails) result.AddRange(SplitEmails(extra));
        foreach (var contact in ParseContacts(contactsJson).OfType<JsonObject>())
            result.AddRange(ContactEmails(contact));
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool HasDomain(Supplier supplier, string domain) =>
        AllEmails(supplier).Any(e => e.Contains('@') && string.Equals(Domain(e), domain, StringComparison.OrdinalIgnoreCase));

    /// <summary>Contact persons as <c>{name, emails, phone}</c>, for pickers.</summary>
    public static List<SupplierContactView> ContactViews(Supplier supplier) =>
        ParseContacts(supplier.Contacts).OfType<JsonObject>()
            .Select(c => new SupplierContactView
            {
                Name = (string?)c["name"] ?? "",
                Emails = ContactEmails(c),
                Phone = (string?)c["phone"],
            })
            .ToList();

    /// <summary>
    /// Adds <paramref name="email"/> (already normalised) to the supplier unless it is already there.
    /// With a contact name it goes on that contact person (created when missing); without one it
    /// becomes the main Email when that is empty, otherwise a new contact person named after the address.
    /// Returns where it was stored, or null when the address already existed.
    /// </summary>
    public static string? AddEmail(Supplier supplier, string email, string? contactName)
    {
        var (main, contacts, target) = AddEmail(supplier.Email, supplier.Contacts, AllEmails(supplier), email, contactName);
        supplier.Email = main;
        supplier.Contacts = contacts;
        return target;
    }

    /// <summary>
    /// <see cref="AddEmail(Supplier, string, string?)"/> on plain fields, for any entity with a main email
    /// and a contacts JSON (customers too). <paramref name="existing"/> = every address it already has.
    /// Returns the new field values and where the address went (null Target = it was already there).
    /// </summary>
    public static (string? MainEmail, string? Contacts, string? Target) AddEmail(
        string? mainEmail, string? contactsJson, IEnumerable<string> existing, string email, string? contactName)
    {
        if (existing.Contains(email, StringComparer.OrdinalIgnoreCase)) return (mainEmail, contactsJson, null);

        var name = contactName?.Trim();
        if (string.IsNullOrEmpty(name) && string.IsNullOrWhiteSpace(mainEmail))
            return (email, contactsJson, "Main email");

        var contacts = ParseContacts(contactsJson);
        if (string.IsNullOrEmpty(name)) name = email[..email.IndexOf('@')];

        var contact = contacts.OfType<JsonObject>()
            .FirstOrDefault(c => string.Equals(((string?)c["name"])?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (contact == null)
        {
            contact = new JsonObject { ["name"] = name, ["email"] = email, ["phone"] = "" };
            contacts.Add(contact);
        }

        var emails = ContactEmails(contact);
        emails.Add(email);
        contact["emails"] = new JsonArray(emails.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray());
        if (string.IsNullOrWhiteSpace((string?)contact["email"])) contact["email"] = emails[0];

        return (mainEmail, contacts.ToJsonString(), $"Contact: {name}");
    }

    private static List<string> ContactEmails(JsonObject contact)
    {
        var result = new List<string>();
        if (contact["emails"] is JsonArray list)
            result.AddRange(list.Select(e => e?.GetValueKind() == System.Text.Json.JsonValueKind.String ? (string?)e : null)
                .Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!.Trim()));
        if (contact["email"]?.GetValueKind() == System.Text.Json.JsonValueKind.String)
            result.InsertRange(0, SplitEmails((string?)contact["email"]));
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> SplitEmails(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static JsonArray ParseContacts(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new JsonArray();
        try { return JsonNode.Parse(raw) as JsonArray ?? new JsonArray(); }
        catch (System.Text.Json.JsonException) { return new JsonArray(); }
    }
}

public class SupplierContactView
{
    public string Name { get; set; } = "";
    public List<string> Emails { get; set; } = new();
    public string? Phone { get; set; }
}
