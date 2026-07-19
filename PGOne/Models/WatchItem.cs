namespace PGOne.Models;

public class WatchItem
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public TrendDirection Trend { get; set; }
    public bool IsFavorite { get; set; }
}
