namespace PGOne.Models;

public enum TrendStrength
{
    Weak,
    Moderate,
    Strong
}

public class MultiTimeframeAnalysis
{
    public TrendDirection Trend1H { get; set; }
    public TrendDirection Trend15M { get; set; }
    public TrendDirection Trend5M { get; set; }

    // RSI(14) shown in the panel (1H).
    public decimal Rsi { get; set; }

    // RSI(28) 1H bias per the trade framework.
    public decimal RsiTrend { get; set; }
    public TrendDirection RsiBias { get; set; }

    public decimal Adx { get; set; }
    public TrendStrength Strength1H { get; set; }

    public string Cpr { get; set; } = "Neutral";

    // 5m VWAP context.
    public decimal Vwap5M { get; set; }
    public bool AboveVwap { get; set; }

    // Range-bound regime (1H RSI between bear/bull thresholds) → Keltner mode.
    public bool IsRangebound { get; set; }

    // Reversal guard (any timeframe RSI below the reversal threshold).
    public bool WaitForReversal { get; set; }
    public string? ReversalReason { get; set; }

    public decimal Rsi5M { get; set; }
    public decimal Rsi15M { get; set; }

    public int OverallScore { get; set; }
    public string Strength { get; set; } = "Moderate";
}
