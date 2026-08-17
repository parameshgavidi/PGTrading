namespace PgAiTrading.Models;

/// <summary>
/// Persisted record of an Auto Buy entry that failed to place
/// (order rejected, error, etc.) — shown below the Add company block.
/// </summary>
public sealed class AutoBuyFailedEntry
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public string Timeframe { get; set; } = "5m";
    public int Quantity { get; set; }
    public string Status { get; set; } = "Order failed";
    public string? Detail { get; set; }
    /// <summary>Public/local IP observed when the failure happened.</summary>
    public string? IpAddress { get; set; }
    public DateTime FailedAt { get; set; } = DateTime.Now;

    public static AutoBuyFailedEntry Clone(AutoBuyFailedEntry source) =>
        new()
        {
            Symbol = (source.Symbol ?? string.Empty).Trim().ToUpperInvariant(),
            Exchange = string.IsNullOrWhiteSpace(source.Exchange)
                ? "NSE"
                : source.Exchange.Trim().ToUpperInvariant(),
            Timeframe = AutoBuyTimeframes.Normalize(source.Timeframe),
            Quantity = Math.Max(0, source.Quantity),
            Status = string.IsNullOrWhiteSpace(source.Status) ? "Order failed" : source.Status.Trim(),
            Detail = source.Detail,
            IpAddress = string.IsNullOrWhiteSpace(source.IpAddress) ? null : source.IpAddress.Trim(),
            FailedAt = source.FailedAt == default ? DateTime.Now : source.FailedAt
        };
}
