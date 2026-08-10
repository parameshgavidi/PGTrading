namespace PGOneTrade.Models;

public class StockScanRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public decimal LastPrice { get; set; }
    public int Quantity { get; set; }
    public decimal OrderValue { get; set; }
    public bool FrameworkSatisfied { get; set; }
    public string FrameworkStatus { get; set; } = string.Empty;
    public int FrameworkScore { get; set; }
    public string? OrderMessage { get; set; }
}

public class LongTermExitRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal PnL { get; set; }
    public decimal SuperTrendLevel { get; set; }
    public decimal LastClosedPrice { get; set; }
    public bool IsExitSignal { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public static class ScanNotional
{
    public const decimal Intraday = 5000m;
    public const decimal LongTerm = 10000m;
}
