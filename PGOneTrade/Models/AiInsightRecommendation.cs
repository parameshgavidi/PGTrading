namespace PGOneTrade.Models;

/// <summary>Structured AI panel output — probability aligned with actionable readiness.</summary>
public sealed class AiInsightRecommendation
{
    public int Probability { get; init; }
    public string Strength { get; init; } = "Weak";
    public string ActionHeadline { get; init; } = "HOLD";
    public string ActionDetail { get; init; } = string.Empty;
    public string ActionKind { get; init; } = "neutral";
}
