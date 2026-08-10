namespace PGOneTrade.Models;

public class Holding
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal DayChangePercent { get; set; }
    public decimal PnL { get; set; }
}

public class HoldingRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal DayChangePercent { get; set; }
    public decimal OverallChangePercent { get; set; }
    public bool FrameworkSatisfied { get; set; }
    public string FrameworkStatus { get; set; } = string.Empty;
    public string? StopLossRecommendation { get; set; }
    public int FrameworkScore { get; set; }
    public bool IsClosed { get; set; }
    public decimal PnL { get; set; }
}
