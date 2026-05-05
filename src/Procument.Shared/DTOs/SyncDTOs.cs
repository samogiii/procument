using System.Text.Json.Serialization;

namespace Procument.Shared.DTOs;

public class SyncExchangeRequest
{
    public string SenderPublicKey { get; set; } = string.Empty;
    public string EncryptedKey { get; set; } = string.Empty; // RSA encrypted AES key
    public string Iv { get; set; } = string.Empty;
    public string AuthTag { get; set; } = string.Empty;
    public string CipherText { get; set; } = string.Empty; // AES encrypted JSON
    public string Signature { get; set; } = string.Empty; // RSA signature
}

public class SyncExchangeResponse
{
    public string EncryptedKey { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string AuthTag { get; set; } = string.Empty;
    public string CipherText { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public class SyncPayload
{
    public DateTime LastSyncTime { get; set; }
    [JsonPropertyName("rfqs")]
    public List<SyncRFQData> RFQs { get; set; } = new();
}

public class SyncRFQData
{
    public long Id { get; set; } // Sender's Local ID
    public long? MainAppId { get; set; } // ID in Main App (if known)
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Remark { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ExType { get; set; }
    public DateTime? LeadTime { get; set; }
    public string? Notes { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerContactPerson { get; set; }
    public string? CustomerShipTo { get; set; }
    public string? CustomerBillTo { get; set; }
    public string? CustomerShippingAccount { get; set; }
    public string? CustomerDescription { get; set; }
    public bool CustomerIsActive { get; set; } = true;
    public DateTime? CustomerModifyAt { get; set; }
    public string? CustomerTermsAndConditions { get; set; }
    public string? CustomerCurrencyType { get; set; }
    public int? CustomerExWork { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime ReceivedDate { get; set; }
    public List<SyncRFQItemData> Items { get; set; } = new();
}

public class SyncRFQItemData
{
    public long Id { get; set; }
    public long? MainAppId { get; set; }
    public string? Alt { get; set; }
    public string? Condition { get; set; }
    public int Qty { get; set; }
    public string? Unit { get; set; }
    public string? Priority { get; set; }
    public string? Remark { get; set; }
    public long PartNumberId { get; set; }
    public string PartNumberName { get; set; } = string.Empty;
    public string? PartNumberDescription { get; set; }
}
