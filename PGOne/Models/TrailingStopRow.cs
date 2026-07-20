namespace PGOne.Models;

public class TrailingStopRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public int Quantity { get; set; }
    public TrendDirection Side { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal PnL { get; set; }
    public decimal SuperTrendLevel { get; set; }
    public decimal LastClosedPrice { get; set; }
    public bool IsTriggered { get; set; }
    public bool ExitPlaced { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public static class TrailingStopDefaults
{
    public const int Period = 7;
    public const double Multiplier = 2.5;
    public const string Interval = "5m";
}
