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

    /// <summary>SuperTrend ST(7, 2.5) — entry / trailing stop timeframe.</summary>
    public decimal? SuperTrendEntry { get; set; }

    // Keltner Channels (populated on the 5m series only).
    public decimal? KeltnerMid { get; set; }
    public decimal? KeltnerUpperInner { get; set; }
    public decimal? KeltnerLowerInner { get; set; }
    public decimal? KeltnerUpperOuter { get; set; }
    public decimal? KeltnerLowerOuter { get; set; }

    // Session-anchored VWAP (populated on the 5m series only).
    public decimal? Vwap { get; set; }

    // EMAs on close (populated on 5m intraday charts).
    public decimal? Ema9 { get; set; }
    public decimal? Ema20 { get; set; }
    public decimal? Ema50 { get; set; }
    public decimal? Ema200 { get; set; }
}
