namespace PgAiTrading.Models;

/// <summary>
/// Versioned long-term scan snapshot — shown instantly on next open; refreshed in background.
/// </summary>
public sealed class LongTermScanDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public DateTime? ScannedAtUtc { get; set; }
    public int UniverseCount { get; set; }
    public int EvaluatedCount { get; set; }
    public string? StatusMessage { get; set; }
    public List<StockScanRow> Items { get; set; } = new();

    public static LongTermScanDocument FromResults(
        IEnumerable<StockScanRow> items,
        DateTime scannedAtUtc,
        int universeCount,
        int evaluatedCount,
        string? statusMessage) =>
        new()
        {
            Version = CurrentVersion,
            ScannedAtUtc = scannedAtUtc,
            UniverseCount = universeCount,
            EvaluatedCount = evaluatedCount,
            StatusMessage = statusMessage,
            Items = items.Select(CloneRow).ToList()
        };

    private static StockScanRow CloneRow(StockScanRow row) =>
        new()
        {
            Symbol = row.Symbol,
            Exchange = row.Exchange,
            LastPrice = row.LastPrice,
            Quantity = row.Quantity,
            OrderValue = row.OrderValue,
            FrameworkSatisfied = row.FrameworkSatisfied,
            FrameworkStatus = row.FrameworkStatus,
            FrameworkScore = row.FrameworkScore
        };
}
