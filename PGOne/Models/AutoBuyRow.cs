namespace PGOne.Models;

public sealed class AutoBuyRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
  /// <summary>1m, 5m, or 15m — SuperTrend ST(7, 2.5) evaluated on this interval.</summary>
    public string Timeframe { get; set; } = "5m";
  /// <summary>Order quantity (shares for NSE equity MIS).</summary>
    public int Lots { get; set; } = 1;
    public bool AutomationEnabled { get; set; }
    public string Status { get; set; } = "Idle";
    public string? Detail { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
}

public static class AutoBuyTimeframes
{
    public static readonly string[] All = ["1m", "5m", "15m"];

    public static bool IsValid(string? timeframe) =>
        timeframe is "1m" or "5m" or "15m";
}
