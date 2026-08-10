namespace PGOneTrade.Models;

/// <summary>Auto Buy places long CNC entries only — never SELL.</summary>
public static class AutoBuyOrderPolicy
{
    public static bool IsAllowedEntrySide(string transactionType) =>
        string.Equals(transactionType, AutoBuyDefaults.EntrySide, StringComparison.OrdinalIgnoreCase);

    public static string EntrySideOrThrow(string transactionType)
    {
        if (!IsAllowedEntrySide(transactionType))
            throw new InvalidOperationException(
                $"Auto Buy only places {AutoBuyDefaults.EntrySide} orders — SELL is not allowed.");

        return transactionType;
    }
}
