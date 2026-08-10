using PgAiTrading.Models;

namespace PgAiTrading.Services;

public sealed class EntryAlertEventArgs : EventArgs
{
    public string Symbol { get; init; } = string.Empty;
    public string Headline { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
}

public interface IEntryAlertService
{
    event Action<EntryAlertEventArgs>? EntryDetected;
    void Evaluate(string symbol, MultiTimeframeAnalysis analysis, Signal signal);
}

/// <summary>
/// Fires once when framework flips into a perfect (good-to-trade) entry for the tracked symbol.
/// Same edge-detect pattern as PGCryptoTrading, scoped to equity Dashboard analysis.
/// </summary>
public class EntryAlertService : IEntryAlertService
{
    private string? _trackedSymbol;
    private bool _wasPerfectEntry;

    public event Action<EntryAlertEventArgs>? EntryDetected;

    public void Evaluate(string symbol, MultiTimeframeAnalysis analysis, Signal signal)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        if (!string.Equals(_trackedSymbol, normalizedSymbol, StringComparison.OrdinalIgnoreCase))
        {
            _trackedSymbol = normalizedSymbol;
            _wasPerfectEntry = false;
        }

        var isPerfectEntry = AiInsightHelper.IsPerfectEntry(analysis, signal);
        if (isPerfectEntry && !_wasPerfectEntry)
        {
            var recommendation = AiInsightHelper.BuildRecommendation(signal, analysis);
            EntryDetected?.Invoke(new EntryAlertEventArgs
            {
                Symbol = normalizedSymbol,
                Headline = recommendation.ActionHeadline,
                Detail = recommendation.ActionDetail,
                Side = recommendation.ActionKind
            });
        }

        _wasPerfectEntry = isPerfectEntry;
    }
}
