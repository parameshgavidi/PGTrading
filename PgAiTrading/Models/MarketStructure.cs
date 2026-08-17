namespace PgAiTrading.Models;

public enum StructureBias
{
    Bullish,
    Bearish,
    Mixed,
    Insufficient
}

public enum StructureEvent
{
    None,
    BosBullish,
    BosBearish,
    ChochBullish,
    ChochBearish
}

public sealed class SwingPoint
{
    public int Index { get; init; }
    public DateTime Timestamp { get; init; }
    public decimal Price { get; init; }
    public bool IsHigh { get; init; }
}

/// <summary>
/// HH/HL or LH/LL market structure for one timeframe.
/// 1H = major direction; 15M = setup; 5M = entry only (not overall bias).
/// </summary>
public sealed class MarketStructureAnalysis
{
    public StructureBias Bias { get; set; } = StructureBias.Insufficient;
    public bool HasHigherHigh { get; set; }
    public bool HasHigherLow { get; set; }
    public bool HasLowerHigh { get; set; }
    public bool HasLowerLow { get; set; }
    public decimal? LastSwingHigh { get; set; }
    public decimal? LastSwingLow { get; set; }
    public StructureEvent LatestEvent { get; set; } = StructureEvent.None;
    public bool BosBullish { get; set; }
    public bool BosBearish { get; set; }
    public string Summary { get; set; } = "Insufficient swings";
    public IReadOnlyList<SwingPoint> Swings { get; set; } = [];

    public TrendDirection AsTrendDirection => Bias switch
    {
        StructureBias.Bullish => TrendDirection.Buy,
        StructureBias.Bearish => TrendDirection.Sell,
        _ => TrendDirection.Neutral
    };

    public bool Confirms(TrendDirection direction) => direction switch
    {
        TrendDirection.Buy => Bias == StructureBias.Bullish,
        TrendDirection.Sell => Bias == StructureBias.Bearish,
        _ => false
    };
}

public sealed class MultiTimeframeStructure
{
    public MarketStructureAnalysis Structure1H { get; set; } = new();
    public MarketStructureAnalysis Structure15M { get; set; } = new();
    public MarketStructureAnalysis Structure5M { get; set; } = new();

    /// <summary>Major direction from 1H only — never from 5M.</summary>
    public TrendDirection MajorDirection => Structure1H.AsTrendDirection;

    public string Summary =>
        $"1H {Structure1H.Summary} · 15M {Structure15M.Summary} · 5M {Structure5M.Summary}";
}

public enum MarketRegime
{
    /// <summary>1H RSI(28) &gt; 55 — directional long bias.</summary>
    TrendingBullish,

    /// <summary>1H RSI(28) &lt; 45 — directional short bias.</summary>
    TrendingBearish,

    /// <summary>RSI 45–55 + ADX &lt; 18 — strong chop; prefer liquidity-sweep mean-reversion.</summary>
    StrongChop,

    /// <summary>RSI 45–55 + ADX &gt; 22 — neutral momentum but not auto-chop; wait for structure.</summary>
    DevelopingTrend,

    /// <summary>RSI 45–55 + ADX between weak and developing — soft neutral.</summary>
    SoftNeutral
}
