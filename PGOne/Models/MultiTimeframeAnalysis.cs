namespace PGOne.Models;

public class MultiTimeframeAnalysis
{
    public TrendDirection Trend1H { get; set; }
    public TrendDirection Trend15M { get; set; }
    public TrendDirection Trend5M { get; set; }
    public decimal Rsi { get; set; }
    public decimal Adx { get; set; }
    public string Cpr { get; set; } = "Neutral";
    public int OverallScore { get; set; }
    public string Strength { get; set; } = "Moderate";
}
