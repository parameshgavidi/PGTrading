namespace PGOneTrade.Models;

public class CandleSeriesResult
{
    public List<Candle> Candles { get; init; } = new();
    public bool IsFromZerodha { get; init; }
    public string? Error { get; init; }
}
