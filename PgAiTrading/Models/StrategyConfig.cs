namespace PgAiTrading.Models;

public class StrategyConfig
{
    public int SuperTrend1HPeriod { get; set; } = 10;
    public double SuperTrend1HMultiplier { get; set; } = 3.0;
    public int SuperTrend15MPeriod { get; set; } = 10;
    public double SuperTrend15MMultiplier { get; set; } = 3.0;
    public int SuperTrend5MPeriod { get; set; } = 7;
    public double SuperTrend5MMultiplier { get; set; } = 2.5;

    public int RsiLength { get; set; } = 14;

    // RSI trend thresholds — 1H RSI(28): >55 long, <45 short; mid band combined with ADX for regime.
    public int RsiTrendLength { get; set; } = 28;
    public decimal RsiBullThreshold { get; set; } = 55m;
    public decimal RsiBearThreshold { get; set; } = 45m;
    public decimal RsiReversalThreshold { get; set; } = 30m;

    // ADX(14) on 1H — <18 choppy, 18–25 moderate, >25 strong.
    // ADX > AdxDevelopingThreshold with mid RSI = developing trend (not auto-chop).
    public int AdxLength { get; set; } = 14;
    public decimal AdxWeakThreshold { get; set; } = 18m;
    public decimal AdxDevelopingThreshold { get; set; } = 22m;
    public decimal AdxStrongThreshold { get; set; } = 25m;
    public int MinimumAdx { get; set; } = 20;

    // Keltner Channels (used on 5m when 1H is range-bound).
    public int KeltnerEmaLength { get; set; } = 20;
    public int KeltnerAtrLength { get; set; } = 20;
    public double KeltnerMultiplierInner { get; set; } = 1.5;
    public double KeltnerMultiplierOuter { get; set; } = 2.0;

    public EntryMode EntryMode { get; set; } = EntryMode.Normal;
}

public enum EntryMode
{
    Conservative,
    Normal,
    Aggressive
}
