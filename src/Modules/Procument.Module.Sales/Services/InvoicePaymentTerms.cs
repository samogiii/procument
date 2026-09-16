using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public static class InvoicePaymentTerms
{
    public const string Prepayment = "Prepayment";
    public const string Cad = "CAD";
    public const string Net = "Net";
    public const string Credit = "Credit";

    public static (string Term, int? Days) Normalize(string? value, int? requestedDays)
    {
        var raw = value?.Trim() ?? "";
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        var suffixDays = int.TryParse(digits, out var parsed) ? parsed : (int?)null;
        var days = requestedDays ?? suffixDays;

        if (raw.Equals(Prepayment, StringComparison.OrdinalIgnoreCase)) return (Prepayment, null);
        if (raw.StartsWith(Cad, StringComparison.OrdinalIgnoreCase))
            return (Cad, ValidateDays(days, Cad));
        if (raw.StartsWith(Net, StringComparison.OrdinalIgnoreCase))
            return (Net, ValidateDays(days, Net));
        if (raw.Equals(Credit, StringComparison.OrdinalIgnoreCase)) return (Credit, null);
        throw new InvalidOperationException("Payment term must be Prepayment, CAD, Net, or Credit.");
    }

    private static int ValidateDays(int? days, string term)
    {
        if (!days.HasValue || days.Value < 1 || days.Value > 3650)
            throw new InvalidOperationException($"{term} requires a day value between 1 and 3650.");
        return days.Value;
    }

    public static string? Display(Invoice invoice, decimal totalPaid, DateTime? now = null)
    {
        if (totalPaid >= invoice.TotalAmount || invoice.Status == "Finish") return null;
        if (invoice.PaymentStatus == Credit) return Credit;
        if (invoice.PaymentStatus == Prepayment) return Prepayment;
        if (!invoice.PaymentTermStartedAt.HasValue) return invoice.PaymentStatus == Net
            ? $"Net{invoice.PaymentTermDays}"
            : invoice.PaymentStatus == Cad ? $"CAD{invoice.PaymentTermDays}" : invoice.PaymentStatus;

        var elapsed = Math.Max(0, ((now ?? DateTime.UtcNow).Date - invoice.PaymentTermStartedAt.Value.Date).Days);
        if (invoice.PaymentStatus == Net)
            return $"Net{Math.Max(0, (invoice.PaymentTermDays ?? 0) - elapsed)}";
        if (invoice.PaymentStatus == Cad)
            return $"CAD{elapsed + 1}";
        return invoice.PaymentStatus;
    }

    public static bool IsDueWarning(Invoice invoice, decimal totalPaid, DateTime? now = null)
    {
        if (invoice.PaymentStatus != Net || totalPaid >= invoice.TotalAmount || !invoice.PaymentTermStartedAt.HasValue)
            return false;
        var elapsed = Math.Max(0, ((now ?? DateTime.UtcNow).Date - invoice.PaymentTermStartedAt.Value.Date).Days);
        return (invoice.PaymentTermDays ?? 0) - elapsed <= 3;
    }

    public static async Task<CreditAvailability> GetCreditAsync(
        DbContext db, long customerId, long? excludeInvoiceId = null)
    {
        var customer = await db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null) throw new InvalidOperationException("Customer not found.");

        var invoices = await db.Set<Invoice>().AsNoTracking()
            .Where(i => i.CustomerId == customerId && i.PaymentStatus == Credit && !i.IsCancelled
                     && i.Status != "Finish" && (!excludeInvoiceId.HasValue || i.Id != excludeInvoiceId.Value))
            .Select(i => new { i.Id, i.InvoiceNumber, i.TotalAmount, i.Status, i.CreatedAt })
            .ToListAsync();
        var ids = invoices.Select(i => i.Id).ToList();
        var paid = ids.Count == 0
            ? new Dictionary<long, decimal>()
            : await db.Set<CustomerPayment>().AsNoTracking()
                .Where(p => ids.Contains(p.InvoiceId))
                .GroupBy(p => p.InvoiceId)
                .Select(g => new { InvoiceId = g.Key, Amount = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.Amount);

        var open = invoices.Select(i => new CreditInvoiceBalance(
            i.Id, i.InvoiceNumber, i.TotalAmount, paid.GetValueOrDefault(i.Id),
            Math.Max(0, i.TotalAmount - paid.GetValueOrDefault(i.Id)), i.Status, i.CreatedAt))
            .Where(i => i.OutstandingAmount > 0).ToList();
        var maximum = customer.CreditEnabled ? Math.Max(0, customer.MaxCredit ?? 0) : 0;
        var used = open.Sum(i => i.OutstandingAmount);
        return new CreditAvailability(customer.Id, customer.Name, customer.CustomerCode,
            customer.CreditEnabled, maximum, used, Math.Max(0, maximum - used), open);
    }

    public static async Task EnsureCreditAvailableAsync(DbContext db, long customerId, decimal amount, long? excludeInvoiceId = null)
    {
        var credit = await GetCreditAsync(db, customerId, excludeInvoiceId);
        if (!credit.Enabled)
            throw new InvalidOperationException("Credit is not enabled for this customer. Enable it in Catalog or change the payment term.");
        if (amount <= credit.AvailableCredit) return;
        var openNumbers = credit.OpenInvoices.Count == 0
            ? "none"
            : string.Join(", ", credit.OpenInvoices.Select(i => $"{i.InvoiceNumber} (${i.OutstandingAmount:N2})"));
        throw new InvalidOperationException(
            $"Customer credit is insufficient. Available: {credit.AvailableCredit:N2}; requested: {amount:N2}. Open Credit PIs: {openNumbers}. Edit the customer credit or change the payment term.");
    }
}

public record CreditInvoiceBalance(long InvoiceId, string InvoiceNumber, decimal InvoiceTotal,
    decimal PaidAmount, decimal OutstandingAmount, string Status, DateTime CreatedAt);
public record CreditAvailability(long CustomerId, string CustomerName, string? CustomerCode,
    bool Enabled, decimal MaxCredit, decimal UsedCredit, decimal AvailableCredit,
    List<CreditInvoiceBalance> OpenInvoices);
