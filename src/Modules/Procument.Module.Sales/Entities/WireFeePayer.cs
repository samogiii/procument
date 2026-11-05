namespace Procument.Module.Sales.Entities;

/// <summary>
/// Who pays the wire fee when a wallet sends money (PaymentBox.WireFeePaidBy, copied onto each transfer / withdraw):
/// Ours = all of it off our (sending) wallet, Split = half each, Theirs = all of it off the receiver (for a transfer the
/// destination wallet; for a withdraw the supplier, so nothing off ours).
/// </summary>
public static class WireFeePayer
{
    public const string Ours = "Ours";
    public const string Split = "Split";
    public const string Theirs = "Theirs";

    /// <summary>Null / unknown = not set.</summary>
    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "ours" or "our" => Ours,
        "split" or "sha" or "50" => Split,
        "theirs" or "their" or "ben" => Theirs,
        _ => null,
    };

    /// <summary>
    /// Not set on the wallet: a transfer keeps the old rule (the destination pays the wire fee), a withdraw the old one
    /// too (our wallet pays it).
    /// </summary>
    public static string ForTransfer(string? walletSetting) => Normalize(walletSetting) ?? Theirs;
    public static string ForWithdraw(string? walletSetting) => Normalize(walletSetting) ?? Ours;

    /// <summary>The sending wallet's part of the wire fee.</summary>
    public static decimal OurShare(string payer) => Normalize(payer) switch
    {
        Ours => 1m,
        Split => 0.5m,
        _ => 0m,
    };
}
