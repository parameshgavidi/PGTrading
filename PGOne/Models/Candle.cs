namespace PGOne.Models;

public class Candle
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? SuperTrend { get; set; }

    // Keltner Channels (populated on the 5m series only).
    public decimal? KeltnerMid { get; set; }
    public decimal? KeltnerUpperInner { get; set; }
    public decimal? KeltnerLowerInner { get; set; }
    public decimal? KeltnerUpperOuter { get; set; }
    public decimal? KeltnerLowerOuter { get; set; }

    // Session-anchored VWAP (populated on the 5m series only).
    public decimal? Vwap { get; set; }
}
