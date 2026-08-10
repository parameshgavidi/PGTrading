namespace PGOneTrade.Models;

/// <summary>Cached 1H + 5m candles from intraday scan phase 1 to avoid re-fetching in phase 2.</summary>
public sealed class IntradayPrefetch
{
    public string Symbol { get; init; } = string.Empty;
    public string InstrumentKey { get; init; } = string.Empty;
    public List<Candle> Candles1H { get; init; } = new();
    public List<Candle> Candles5M { get; init; } = new();
}
