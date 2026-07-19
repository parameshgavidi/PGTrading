namespace PGOne.Models;

public class StrategyConfig
{
    public int SuperTrend1HPeriod { get; set; } = 10;
    public double SuperTrend1HMultiplier { get; set; } = 3.0;
    public int SuperTrend15MPeriod { get; set; } = 10;
    public double SuperTrend15MMultiplier { get; set; } = 3.0;
    public int SuperTrend5MPeriod { get; set; } = 7;
    public double SuperTrend5MMultiplier { get; set; } = 2.5;
    public int RsiLength { get; set; } = 14;
    public int AdxLength { get; set; } = 14;
    public int MinimumAdx { get; set; } = 25;
    public EntryMode EntryMode { get; set; } = EntryMode.Normal;
}

public enum EntryMode
{
    Conservative,
    Normal,
    Aggressive
}
