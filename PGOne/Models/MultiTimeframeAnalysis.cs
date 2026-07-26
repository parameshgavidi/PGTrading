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

    // 5m SuperTrend (7,2.5) — entry trigger timeframe.
    public TrendDirection Trend5MEntry { get; set; }

    // RSI(14) panel on 1H; RSI(28) trend bias on 1H.
    public decimal Rsi { get; set; }

    // RSI(28) on 1H — retained for display.
    public decimal RsiTrend { get; set; }
    public TrendDirection RsiBias { get; set; }

    public decimal Adx { get; set; }
    public decimal Adx15M { get; set; }
    public TrendStrength Strength1H { get; set; }

    public string Cpr { get; set; } = "Neutral";
    public bool CprNarrow { get; set; }
    public decimal CprWidthPercent { get; set; }

    // 5m VWAP context.
    public decimal Vwap5M { get; set; }
    public bool AboveVwap { get; set; }

    // Rotation regime (ADX < 18 inside VA) or legacy RSI-neutral range.
    public bool IsRangebound { get; set; }
    public bool IsRotationRegime { get; set; }

    // Reversal guard — 5m RSI below threshold: no new entries.
    public bool WaitForReversal { get; set; }
    public string? ReversalReason { get; set; }

    public decimal Rsi5M { get; set; }
    public decimal Rsi15M { get; set; }

    public int OverallScore { get; set; }
    public string Strength { get; set; } = "Moderate";

    // Framework outputs.
    public TrendDirection MarketBias { get; set; }
    public TrendDirection TradeDirection { get; set; }
    public bool EntryTriggered { get; set; }
    public bool TpoConfirmed { get; set; }
    public bool FootprintConfirmed { get; set; }
    public bool FrameworkReady { get; set; }
    public string FrameworkStatus { get; set; } = "Wait";

    public TpoConfirmationAnalysis Tpo { get; set; } = new();
    public FootprintAnalysis Footprint { get; set; } = new();
    public VolumeProfileLevels VolumeProfile { get; set; } = new();
}
