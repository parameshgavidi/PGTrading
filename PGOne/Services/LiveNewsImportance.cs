using System.Text.RegularExpressions;
using PGOne.Models;

namespace PGOne.Services;

/// <summary>
/// Ranks raw feed headlines by market impact so the dashboard can show the top affected stories.
/// Pure logic — unit tested without HTTP.
/// </summary>
public static class LiveNewsImportance
{
    private static readonly string[] MarketKeywords =
    [
        "nifty", "sensex", "bank nifty", "rbi", "repo rate", "fii", "dii", "fed",
        "crude", "oil", "gdp", "inflation", "budget", "sebi", "ipo", "buyback",
        "earnings", "results", "crash", "surge", "rally", "selloff", "circuit",
        "rupee", "dollar", "war", "geopolit", "rate cut", "rate hike"
    ];

    private static readonly Dictionary<string, double> SourceWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MoneyControl"] = 1.25,
        ["Economic Times"] = 1.2,
        ["LiveMint"] = 1.15,
        ["Google News"] = 1.0
    };

    public static double Score(
        string title,
        string source,
        IReadOnlyList<string> relatedSymbols)
    {
        if (string.IsNullOrWhiteSpace(title))
            return 0;

        var normalized = title.ToLowerInvariant();
        var score = 1.0;

        // More unique NSE names mentioned ⇒ more "top affected" stocks.
        score += Math.Min(relatedSymbols.Count, 5) * 2.5;

        var keywordHits = 0;
        foreach (var keyword in MarketKeywords)
        {
            if (normalized.Contains(keyword, StringComparison.Ordinal))
                keywordHits++;
        }

        score += Math.Min(keywordHits, 4) * 1.4;

        if (SourceWeight.TryGetValue(source, out var weight))
            score *= weight;

        // Prefer shorter, punchy market titles over long SEO blurbs.
        if (title.Length is >= 40 and <= 120)
            score += 0.5;

        // Mild boost for clear directional verbs.
        if (Regex.IsMatch(normalized, @"\b(surge|soar|jump|rally|plunge|crash|slump|fall|tumble)\b"))
            score += 1.0;

        return score;
    }

    public static string NormalizeTitleKey(string title)
    {
        var cleaned = SentimentTextHelper.CleanBoilerplate(title).ToLowerInvariant();
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }
}
