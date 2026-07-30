namespace PGOne.Models;

/// <summary>Cached daily candles from long-term scan phase 1.</summary>
public sealed class LongTermPrefetch
{
    public string Symbol { get; init; } = string.Empty;
    public List<Candle> DailyCandles { get; init; } = new();
}
