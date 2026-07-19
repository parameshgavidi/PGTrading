namespace PGOne.Models;

public class Position
{
    public string Instrument { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal PnL { get; set; }
    public TrendDirection Side { get; set; }
}
