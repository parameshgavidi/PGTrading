namespace PgAiTrading.Models;

/// <summary>A live market headline ranked for dashboard importance, with sentiment scores.</summary>
public sealed class LiveNewsHeadline
{
    public string Headline { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Label { get; set; } = "neutral";
    public double Score { get; set; }
    public double PositiveScore { get; set; }
    public double NegativeScore { get; set; }
    public double NeutralScore { get; set; }
    public string? Link { get; set; }

    /// <summary>How market-moving / important this item is (higher = more important).</summary>
    public double ImportanceScore { get; set; }

    /// <summary>NSE symbols mentioned in the headline (top affected names).</summary>
    public List<string> RelatedSymbols { get; set; } = new();
}
