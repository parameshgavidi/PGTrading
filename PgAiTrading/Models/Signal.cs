namespace PgAiTrading.Models;

public enum TrendDirection
{
    Neutral,
    Buy,
    Sell
}

public class Signal
{
    public string Instrument { get; set; } = "NIFTY";
    public TrendDirection Trend { get; set; }
    public string Entry { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string StopLoss { get; set; } = string.Empty;
    public decimal? StopLossLevel { get; set; }
    public int Strike { get; set; }
    public string OptionType { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public List<string> Reasons { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}
