namespace PGOneTrade.Models;

public class Position
{
    public string Exchange { get; set; } = "NSE";
    public string Symbol { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal PnL { get; set; }
    public TrendDirection Side { get; set; }
    public bool IsClosed { get; set; }
    public int DayBuyQuantity { get; set; }
    public int DaySellQuantity { get; set; }

    /// <summary>Backward-compatible alias for <see cref="Exchange"/>.</summary>
    public string Instrument
    {
        get => Exchange;
        set => Exchange = value;
    }
}
