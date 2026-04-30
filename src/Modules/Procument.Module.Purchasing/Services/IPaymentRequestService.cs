using Procument.Module.Purchasing.Entities;
using Procument.Shared.DTOs;

namespace Procument.Module.Purchasing.Services;

public interface IPaymentRequestService
{
    Task<PaymentRequestResponse> GetByIdAsync(long id);
    Task<List<PaymentRequestResponse>> GetAllAsync();
    Task<PaymentRequestResponse> CreateAsync(long poId);
    Task<bool> UpdateStatusAsync(long id, string status);
    Task<bool> DeleteAsync(long id);
    Task<PaymentRequestResponse?> GetByPoIdAsync(long poId);
}

public class PaymentRequestResponse
{
    public long Id { get; set; }
    public long? PrNumber { get; set; }
    public string? Status { get; set; }
    public long? POId { get; set; }
    public string? PONumber { get; set; }
    public string? SupplierName { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Bank Info (Flattened for UI)
    public string? CompanyPayingFrom { get; set; }
    public string? CompanyPayingTo { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? SwiftCode { get; set; }
    public string? ABA { get; set; }
    public string? CompanyAddress { get; set; }
    public string? BankAddress { get; set; }
    
    // Summary
    public decimal ItemsTotal { get; set; }
    public decimal WireFee { get; set; }
    public decimal GrandTotal => ItemsTotal + WireFee;
}
