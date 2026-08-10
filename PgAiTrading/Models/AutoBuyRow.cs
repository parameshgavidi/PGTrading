namespace PgAiTrading.Models;

public sealed class AutoBuyRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
  /// <summary>1m, 5m, or 15m — SuperTrend ST(7, 2.5) evaluated on this interval.</summary>
    public string Timeframe { get; set; } = "5m";
  /// <summary>Order quantity in shares (NSE equity CNC).</summary>
    public int Lots { get; set; } = 1;
    /// <summary>Max capital to deploy in this stock (₹). 0 = no limit.</summary>
    public decimal MaxDeployAmount { get; set; }
    public bool AutomationEnabled { get; set; }
    public string Status { get; set; } = "Idle";
    public string? Detail { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    /// <summary>Current deployed value (holdings + CNC) — not persisted.</summary>
    public decimal DeployedAmount { get; set; }
}

public static class AutoBuyTimeframes
{
    public static readonly string[] All = ["1m", "5m", "15m"];

    public static bool IsValid(string? timeframe) =>
        timeframe is "1m" or "5m" or "15m";

    public static string Normalize(string? timeframe) =>
        IsValid(timeframe) ? timeframe! : "5m";
}
