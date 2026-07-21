namespace PGOne.Models;

public enum SentimentPrediction
{
    Bullish,
    Bearish,
    Neutral
}

public class NewsSentimentItem
{
    public string Headline { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class StockSentimentResult
{
    public string Symbol { get; set; } = string.Empty;
    public SentimentPrediction Prediction { get; set; } = SentimentPrediction.Neutral;
    public double Confidence { get; set; }
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }
    public int NeutralCount { get; set; }
    public int NewsCount { get; set; }
    public List<NewsSentimentItem> Headlines { get; set; } = new();
    public string? Error { get; set; }
}
