using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

/// <summary>
/// Builds the Base 1 document numbers (B1 numbers) used by Base 1 customers.
///
/// Format: <c>{letter}{customer digits}-{y}{MM}{dd}-{seq}</c> — e.g. customer <c>C101</c>
/// quoted on 2026-07-01 gets <c>Q101-60701-10</c>. The leading letter identifies the
/// document type (Q = Quote, P = Sales Order / proforma, I = Final Invoice) and replaces
/// the customer code's own leading letter; <c>y</c> is the last digit of the year;
/// <c>seq</c> restarts at 10 for every customer at the start of each calendar month.
///
/// Only quotes generate a B1 number. A Sales Order copies its source quote's with the
/// letter swapped to P, and a Final Invoice copies the proforma's with I — so the three
/// documents of one deal always carry the same digits. Customers outside Base 1, and
/// customers with no customer code, get none at all (null).
/// </summary>
public interface IB1NumberService
{
    /// <summary>
    /// Next B1 quote number for a customer, or null when the customer is not eligible
    /// (base other than 1, missing customer code, or customer not found).
    /// </summary>
    Task<string?> GenerateB1QuoteNumberAsync(long customerId, DateTime onDate);

    /// <summary>Re-letters an existing B1 number for another document type. Null in, null out.</summary>
    string? Relabel(string? b1Number, char documentLetter);

    /// <summary>Trims user input and collapses blank to null, so "clear the number" round-trips.</summary>
    string? Normalize(string? input);

    /// <summary>True when another quote already holds this B1 number — the unique index would reject it.</summary>
    Task<bool> IsB1QuoteNumberTakenAsync(string b1Number, long exceptQuoteId);
}

/// <summary>Outcome of a manual B1 number edit, so controllers can pick the right status code.</summary>
public enum B1NumberUpdateResult
{
    Ok,
    NotFound,
    Forbidden,
    /// <summary>Another quote already uses this B1 number.</summary>
    Duplicate,
}

public class B1NumberService : IB1NumberService
{
    /// <summary>Only Base 1 customers are numbered this way.</summary>
    private const int NumberedBase = 1;

    /// <summary>Every month's sequence starts here rather than at 1.</summary>
    private const int FirstSequence = 10;

    public const char QuoteLetter = 'Q';
    public const char InvoiceLetter = 'P';
    public const char FinalInvoiceLetter = 'I';

    private readonly DbContext _db;

    public B1NumberService(DbContext db)
    {
        _db = db;
    }

    public async Task<string?> GenerateB1QuoteNumberAsync(long customerId, DateTime onDate)
    {
        var customer = await _db.Set<Customer>()
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new { c.Base, c.CustomerCode })
            .FirstOrDefaultAsync();

        if (customer?.Base != NumberedBase) return null;

        var prefix = BuildPrefix(customer.CustomerCode, QuoteLetter);
        if (prefix == null) return null;

        // The sequence is per customer per calendar month, so only that month's quotes matter.
        var monthStart = new DateTime(onDate.Year, onDate.Month, 1, 0, 0, 0, onDate.Kind);
        var monthEnd = monthStart.AddMonths(1);

        var existing = await _db.Set<Quote>()
            .AsNoTracking()
            .Where(q => q.CustomerId == customerId
                     && q.B1QuoteNumber != null
                     && q.CreatedAt >= monthStart
                     && q.CreatedAt < monthEnd)
            .Select(q => q.B1QuoteNumber!)
            .ToListAsync();

        var next = existing.Select(ParseSequence).DefaultIfEmpty(FirstSequence - 1).Max() + 1;
        if (next < FirstSequence) next = FirstSequence;

        return $"{prefix}-{BuildDatePart(onDate)}-{next}";
    }

    public string? Relabel(string? b1Number, char documentLetter)
    {
        if (string.IsNullOrWhiteSpace(b1Number)) return null;
        return documentLetter + b1Number.Trim()[1..];
    }

    public string? Normalize(string? input)
        => string.IsNullOrWhiteSpace(input) ? null : input.Trim();

    public Task<bool> IsB1QuoteNumberTakenAsync(string b1Number, long exceptQuoteId)
        => _db.Set<Quote>().AsNoTracking().AnyAsync(q => q.B1QuoteNumber == b1Number && q.Id != exceptQuoteId);

    /// <summary>
    /// "C101" + 'Q' → "Q101". The customer code's own leading letter is dropped, so the
    /// document letter always sits in front of the customer's digits. Codes that are only
    /// one character long carry no digits and are rejected.
    /// </summary>
    private static string? BuildPrefix(string? customerCode, char documentLetter)
    {
        var code = customerCode?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length < 2) return null;
        return documentLetter + code[1..];
    }

    /// <summary>2026-07-01 → "60701": last digit of the year, then MMdd.</summary>
    private static string BuildDatePart(DateTime date) => $"{date.Year % 10}{date:MMdd}";

    /// <summary>Last dash-separated segment of a B1 number, or 0 when it doesn't parse.</summary>
    private static int ParseSequence(string b1Number)
    {
        var lastDash = b1Number.LastIndexOf('-');
        if (lastDash < 0 || lastDash == b1Number.Length - 1) return 0;
        return int.TryParse(b1Number[(lastDash + 1)..], out var seq) ? seq : 0;
    }
}
