namespace PGOneTrade.Models;

public enum SentimentPrediction
{
    Bullish,
    Bearish,
    Neutral
}

public class NewsSentimentItem
{
    public string Headline { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double Score { get; set; }
    public double PositiveScore { get; set; }
    public double NegativeScore { get; set; }
    public double NeutralScore { get; set; }
    public string? Link { get; set; }
}

public class StockSentimentResult
{
    public string Symbol { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public SentimentPrediction Prediction { get; set; } = SentimentPrediction.Neutral;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }
    public int NeutralCount { get; set; }
    public int NewsCount { get; set; }
    public List<string> Sources { get; set; } = new();
    public List<NewsSentimentItem> Headlines { get; set; } = new();
    public string? Error { get; set; }
    public string? Warning { get; set; }
    public string? OrderMessage { get; set; }
}
