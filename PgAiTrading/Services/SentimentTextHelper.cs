using System.Text.RegularExpressions;

namespace PgAiTrading.Services;

/// <summary>Prepares news text for FinBERT — strips site chrome and prioritizes headlines.</summary>
public static class SentimentTextHelper
{
    private const int MaxAnalysisChars = 512;
    private const int MaxBodySnippetChars = 280;

    private static readonly string[] BoilerplatePhrases =
    [
        "subscribe sign in", "sign in subscribe", "view market dashboard", "home markets market news",
        "stock markets", "stock market news", "mint premium", "read more", "also read", "trending now",
        "follow us on", "cookie policy", "privacy policy", "terms of use", "all rights reserved",
        "download app", "get app", "newsletter", "advertisement", "skip to content", "skip to main",
        "market dashboard", "home markets", "markets market news", "ipo mint", "view all", "see all",
        "moneycontrol", "livemint", "economictimes", "economic times", "google news"
    ];

    private static readonly string[] PositiveTerms =
    [
        "surge", "rally", "gain", "gains", "profit", "growth", "upgrade", "beat", "strong", "bullish",
        "rise", "rises", "rose", "rising", "jump", "outperform", "record high", "boost", "positive", "expand",
        "momentum", "recovery", "upside", "buy", "accumulate", "undervalued", "dividend", "outlook positive",
        "raises target", "target raised", "beats estimate", "top pick", "breakout", "soars", "climbs"
    ];

    private static readonly string[] NegativeTerms =
    [
        "crash", "fall", "falls", "fell", "drop", "drops", "loss", "decline", "downgrade", "miss", "weak",
        "bearish", "plunge", "cut", "warning", "slump", "tumble", "negative", "fraud", "concern", "selloff",
        "underperform", "downside", "sell", "reduce", "overvalued", "cuts target", "target cut",
        "misses estimate", "profit warning", "defaults", "investigation", "penalty", "slips", "tanks"
    ];

    public static string PrepareAnalysisText(string title, string? bodySnippet = null)
    {
        var cleanedTitle = CleanBoilerplate(title);
        if (string.IsNullOrWhiteSpace(cleanedTitle))
            cleanedTitle = title.Trim();

        if (string.IsNullOrWhiteSpace(bodySnippet))
            return Truncate(cleanedTitle, MaxAnalysisChars);

        var cleanedBody = CleanBoilerplate(bodySnippet);
        var lead = ExtractLeadSnippet(cleanedBody, MaxBodySnippetChars);

        if (string.IsNullOrWhiteSpace(lead) || lead.Length < 24)
            return Truncate(cleanedTitle, MaxAnalysisChars);

        // Headline carries most signal; body adds context without drowning FinBERT in nav text.
        var combined = $"{cleanedTitle}. {lead}";
        return Truncate(combined, MaxAnalysisChars);
    }

    public static string ExtractReasonSnippet(string? articleText, string title)
    {
        var cleanedTitle = CleanBoilerplate(title);
        if (!string.IsNullOrWhiteSpace(cleanedTitle) && cleanedTitle.Length >= 12)
            return Truncate(cleanedTitle, 160);

        if (string.IsNullOrWhiteSpace(articleText))
            return Truncate(title.Trim(), 160);

        var lead = ExtractLeadSnippet(CleanBoilerplate(articleText), 160);
        return string.IsNullOrWhiteSpace(lead) ? Truncate(title.Trim(), 160) : lead;
    }

    public static string ExtractArticleSnippetFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var metaDescription = ExtractMetaContent(html, "description")
            ?? ExtractMetaContent(html, "og:description");

        if (!string.IsNullOrWhiteSpace(metaDescription))
            return CleanBoilerplate(metaDescription);

        var articleHtml = Regex.Match(
            html,
            @"<article[^>]*>(.*?)</article>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (articleHtml.Success)
        {
            var articleText = StripHtmlTags(articleHtml.Groups[1].Value);
            var lead = ExtractLeadSnippet(CleanBoilerplate(articleText), MaxBodySnippetChars);
            if (!string.IsNullOrWhiteSpace(lead))
                return lead;
        }

        var paragraphMatch = Regex.Match(
            html,
            @"<p[^>]*>(.*?)</p>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (paragraphMatch.Success)
        {
            var paragraph = StripHtmlTags(paragraphMatch.Groups[1].Value);
            var lead = ExtractLeadSnippet(CleanBoilerplate(paragraph), MaxBodySnippetChars);
            if (!string.IsNullOrWhiteSpace(lead) && lead.Length >= 40)
                return lead;
        }

        return string.Empty;
    }

    public static SentimentScoreVector ScoreWithKeywords(string text)
    {
        var lower = text.ToLowerInvariant();
        var positiveHits = PositiveTerms.Count(term => lower.Contains(term, StringComparison.Ordinal));
        var negativeHits = NegativeTerms.Count(term => lower.Contains(term, StringComparison.Ordinal));

        if (positiveHits == 0 && negativeHits == 0)
            return new SentimentScoreVector(0.28, 0.28, 0.44);

        if (positiveHits > negativeHits)
        {
            var boost = Math.Min(0.35, (positiveHits - negativeHits) * 0.09);
            return new SentimentScoreVector(0.52 + boost, 0.18, 0.30 - boost * 0.3);
        }

        if (negativeHits > positiveHits)
        {
            var boost = Math.Min(0.35, (negativeHits - positiveHits) * 0.09);
            return new SentimentScoreVector(0.18, 0.52 + boost, 0.30 - boost * 0.3);
        }

        return new SentimentScoreVector(0.35, 0.35, 0.30);
    }

    public static string CleanBoilerplate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = Regex.Replace(text, @"\s+", " ").Trim();

        foreach (var phrase in BoilerplatePhrases)
        {
            normalized = Regex.Replace(
                normalized,
                Regex.Escape(phrase),
                " ",
                RegexOptions.IgnoreCase);
        }

        // Drop pipe-separated nav crumbs (common in RSS titles).
        if (normalized.Contains('|'))
        {
            var parts = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
                normalized = parts[0];
        }

        normalized = Regex.Replace(normalized, @"\b(subscribe|sign in|read more|also read)\b", " ", RegexOptions.IgnoreCase);
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static string? ExtractMetaContent(string html, string nameOrProperty)
    {
        var pattern = nameOrProperty.StartsWith("og:", StringComparison.Ordinal)
            ? $@"<meta[^>]+property=[""']{Regex.Escape(nameOrProperty)}[""'][^>]+content=[""']([^""']+)[""']"
            : $@"<meta[^>]+name=[""']{Regex.Escape(nameOrProperty)}[""'][^>]+content=[""']([^""']+)[""']";

        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            pattern = nameOrProperty.StartsWith("og:", StringComparison.Ordinal)
                ? $@"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']{Regex.Escape(nameOrProperty)}[""']"
                : $@"<meta[^>]+content=[""']([^""']+)[""'][^>]+name=[""']{Regex.Escape(nameOrProperty)}[""']";
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        return match.Success ? WebUtilityHtmlDecode(match.Groups[1].Value.Trim()) : null;
    }

    private static string WebUtilityHtmlDecode(string value) =>
        System.Net.WebUtility.HtmlDecode(value);

    private static string StripHtmlTags(string html)
    {
        var withoutScripts = Regex.Replace(html, "<script[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withoutStyles = Regex.Replace(withoutScripts, "<style[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var text = Regex.Replace(withoutStyles, "<[^>]+>", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string ExtractLeadSnippet(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        if (normalized.Length <= maxChars)
            return normalized;

        var sentenceEnd = normalized.IndexOf('.', Math.Min(60, normalized.Length / 3));
        if (sentenceEnd >= 40 && sentenceEnd < maxChars)
            return normalized[..(sentenceEnd + 1)].Trim();

        return normalized[..maxChars].Trim() + "...";
    }

    private static string Truncate(string text, int maxChars) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim()[..Math.Min(text.Trim().Length, maxChars)];
}

public readonly record struct SentimentScoreVector(double Positive, double Negative, double Neutral)
{
    public (string Label, double Score) TopLabel()
    {
        if (Positive >= Negative && Positive >= Neutral)
            return ("positive", Positive);

        if (Negative >= Positive && Negative >= Neutral)
            return ("negative", Negative);

        return ("neutral", Neutral);
    }

    public SentimentScoreVector Normalize()
    {
        var total = Positive + Negative + Neutral;
        if (total <= 0)
            return new SentimentScoreVector(0.33, 0.33, 0.34);

        return new SentimentScoreVector(Positive / total, Negative / total, Neutral / total);
    }
}
