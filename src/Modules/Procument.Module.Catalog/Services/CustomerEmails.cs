using Procument.Module.Catalog.Entities;

namespace Procument.Module.Catalog.Services;

/// <summary>
/// Customer email helpers — the same rules as <see cref="SupplierEmails"/> (normalising, company-domain
/// matching, free-mail never matching) over a customer's Email, Emails and Contacts JSON.
/// </summary>
public static class CustomerEmails
{
    /// <summary>Every address on the customer — main Email, the Emails list and contact persons.</summary>
    public static List<string> AllEmails(Customer customer) =>
        SupplierEmails.AllEmails(customer.Email, customer.Contacts, customer.Emails);

    public static bool HasDomain(Customer customer, string domain) =>
        AllEmails(customer).Any(e => e.Contains('@')
            && string.Equals(SupplierEmails.Domain(e), domain, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Adds <paramref name="email"/> (already normalised) unless the customer has it: the main Email when
    /// that is empty and no contact name is given, else a contact person (named after the address when no
    /// name is given). Returns where it went, or null when it was already there.
    /// </summary>
    public static string? AddEmail(Customer customer, string email, string? contactName)
    {
        var (main, contacts, target) = SupplierEmails.AddEmail(customer.Email, customer.Contacts, AllEmails(customer), email, contactName);
        customer.Email = main;
        customer.Contacts = contacts;
        return target;
    }
}
