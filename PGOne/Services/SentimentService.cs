using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PGOne.Models;

namespace PGOne.Services;

public interface ISentimentService
{
    bool IsScanning { get; }
    string? ProgressMessage { get; }
    IReadOnlyList<StockSentimentResult> Results { get; }
    event Action? Updated;
    Task ScanNewsFeedsAsync(CancellationToken cancellationToken = default);
    Task ScanSymbolsAsync(IReadOnlyList<string>? symbols = null, CancellationToken cancellationToken = default);
}

public class SentimentService : ISentimentService
{
    private static readonly (string Name, string Url)[] NewsFeeds =
    [
        ("MoneyControl", "https://www.moneycontrol.com/rss/MCtopnews.xml"),
        ("Economic Times", "https://economictimes.indiatimes.com/rssfeedsdefault.cms"),
        ("LiveMint", "https://www.livemint.com/rss/markets"),
        ("Google News", "https://news.google.com/rss/search?q=nifty%20stocks+when:1d&hl=en-IN&gl=IN&ceid=IN:en")
    ];

    private const string FinBertModel = "ProsusAI/finbert";
    private const string HuggingFaceApiUrl = $"https://router.huggingface.co/hf-inference/models/{FinBertModel}";
    private const int EntriesPerFeed = 10;
    private const int HeadlinesPerStock = 5;
    private const int MaxArticleChars = 900;
    private const int MaxRetries = 3;

    private readonly ISettingsService _settings;
    private readonly INseSymbolResolver _nseSymbols;
    private readonly HttpClient _http;
    private readonly List<StockSentimentResult> _results = new();

    public bool IsScanning { get; private set; }
    public string? ProgressMessage { get; private set; }
    public IReadOnlyList<StockSentimentResult> Results => _results;
    public event Action? Updated;

    public SentimentService(ISettingsService settings, INseSymbolResolver nseSymbols)
    {
        _settings = settings;
        _nseSymbols = nseSymbols;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public Task ScanNewsFeedsAsync(CancellationToken cancellationToken = default) =>
        RunScanAsync(ScanMode.NewsFeeds, null, cancellationToken);

    public Task ScanSymbolsAsync(IReadOnlyList<string>? symbols = null, CancellationToken cancellationToken = default) =>
        RunScanAsync(ScanMode.Symbols, symbols ?? NiftyConstituents.TopWeightage, cancellationToken);

    private async Task RunScanAsync(
        ScanMode mode,
        IReadOnlyList<string>? symbols,
        CancellationToken cancellationToken)
    {
        if (IsScanning)
            return;

        await _settings.LoadAsync();
        await _nseSymbols.EnsureLoadedAsync(cancellationToken);

        IsScanning = true;
        _results.Clear();
        ProgressMessage = mode == ScanMode.NewsFeeds
            ? $"Fetching news from MoneyControl, ET, LiveMint, Google News ({_nseSymbols.SymbolCount:N0} NSE symbols loaded)..."
            : "Starting NSE symbol sentiment scan...";
        NotifyUpdated();

        try
        {
            if (mode == ScanMode.NewsFeeds)
                await ScanFromNewsFeedsAsync(cancellationToken);
            else
                await ScanFromSymbolsAsync(symbols!, cancellationToken);

            SortResults();
            ProgressMessage = $"Sentiment scan complete — {_results.Count} stocks analyzed.";
        }
        catch (OperationCanceledException)
        {
            ProgressMessage = "Sentiment scan cancelled.";
        }
        catch (Exception ex)
        {
            ProgressMessage = $"Sentiment scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            NotifyUpdated();
        }
    }

    private async Task ScanFromNewsFeedsAsync(CancellationToken cancellationToken)
    {
        var mentions = await FetchAndExtractStockMentionsAsync(cancellationToken);
        if (mentions.Count == 0)
        {
            ProgressMessage = "No stock mentions found in recent news feeds.";
            return;
        }

        var grouped = mentions
            .GroupBy(m => m.Symbol, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var completed = 0;
        foreach (var group in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProgressMessage = $"Analyzing {group.Key} ({completed + 1}/{grouped.Count}) from {group.Select(m => m.Source).Distinct().Count()} sources...";
            NotifyUpdated();

            var result = await AnalyzeMentionsAsync(group.Key, group.ToList(), cancellationToken);
            _results.Add(result);
            completed++;

            if (completed < grouped.Count)
                await Task.Delay(350, cancellationToken);
        }
    }

    private async Task ScanFromSymbolsAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken)
    {
        var completed = 0;
        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProgressMessage = $"Analyzing {symbol} ({completed + 1}/{symbols.Count})...";
            NotifyUpdated();

            var mentions = await FetchSymbolMentionsAsync(symbol, cancellationToken);
            var result = mentions.Count > 0
                ? await AnalyzeMentionsAsync(symbol, mentions, cancellationToken)
                : await AnalyzeSymbolHeadlinesAsync(symbol, cancellationToken);

            _results.Add(result);
            completed++;

            if (completed < symbols.Count)
                await Task.Delay(350, cancellationToken);
        }
    }

    private async Task<List<StockNewsMention>> FetchAndExtractStockMentionsAsync(CancellationToken cancellationToken)
    {
        var mentions = new List<StockNewsMention>();

        foreach (var (sourceName, feedUrl) in NewsFeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProgressMessage = $"Reading {sourceName} feed...";
            NotifyUpdated();

            var entries = await FetchFeedEntriesAsync(feedUrl, cancellationToken);
            foreach (var entry in entries.Take(EntriesPerFeed))
            {
                var articleText = await DownloadArticleTextAsync(entry.Link, cancellationToken);
                var combinedText = $"{entry.Title}\n{articleText}";
                var symbols = _nseSymbols.ResolveSymbolsInText(combinedText).ToList();
                if (symbols.Count == 0)
                    continue;

                foreach (var symbol in symbols)
                {
                    mentions.Add(new StockNewsMention
                    {
                        Symbol = symbol,
                        Title = entry.Title,
                        Text = string.IsNullOrWhiteSpace(articleText) ? entry.Title : articleText,
                        Source = sourceName,
                        Link = entry.Link,
                        FeedUrl = feedUrl
                    });
                }
            }
        }

        return mentions;
    }

    private async Task<List<StockNewsMention>> FetchSymbolMentionsAsync(string symbol, CancellationToken cancellationToken)
    {
        var mentions = new List<StockNewsMention>();
        var query = Uri.EscapeDataString($"{symbol} NSE stock");
        var googleFeed = $"https://news.google.com/rss/search?q={query}+when:7d&hl=en-IN&gl=IN&ceid=IN:en";
        var entries = await FetchFeedEntriesAsync(googleFeed, cancellationToken);

        foreach (var entry in entries.Take(HeadlinesPerStock))
        {
            var articleText = await DownloadArticleTextAsync(entry.Link, cancellationToken);
            mentions.Add(new StockNewsMention
            {
                Symbol = symbol,
                Title = entry.Title,
                Text = string.IsNullOrWhiteSpace(articleText) ? entry.Title : articleText,
                Source = "Google News",
                Link = entry.Link,
                FeedUrl = googleFeed
            });
        }

        return mentions;
    }

    private async Task<StockSentimentResult> AnalyzeSymbolHeadlinesAsync(string symbol, CancellationToken cancellationToken)
    {
        var result = new StockSentimentResult { Symbol = symbol };
        var headlines = await FetchGoogleHeadlinesAsync(symbol, cancellationToken);
        result.NewsCount = headlines.Count;

        if (headlines.Count == 0)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Error = "No recent news found.";
            return result;
        }

        var mentions = headlines.Select(h => new StockNewsMention
        {
            Symbol = symbol,
            Title = h,
            Text = h,
            Source = "Google News"
        }).ToList();

        return await AnalyzeMentionsAsync(symbol, mentions, cancellationToken);
    }

    private async Task<StockSentimentResult> AnalyzeMentionsAsync(
        string symbol,
        IReadOnlyList<StockNewsMention> mentions,
        CancellationToken cancellationToken)
    {
        var result = new StockSentimentResult
        {
            Symbol = symbol,
            NewsCount = mentions.Count,
            Sources = mentions.Select(m => m.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };

        var uniqueTexts = mentions
            .Select(m => TruncateForAnalysis(m.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct()
            .ToList();

        if (uniqueTexts.Count == 0)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Error = "No readable article text found.";
            return result;
        }

        var predictions = await AnalyzeTextsAsync(uniqueTexts, cancellationToken);

        foreach (var mention in mentions)
        {
            var text = TruncateForAnalysis(mention.Text);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var predictionIndex = uniqueTexts.IndexOf(text);
            if (predictionIndex < 0 || predictionIndex >= predictions.Count || predictions[predictionIndex] is null)
                continue;

            var prediction = predictions[predictionIndex]!.Value;
            var reason = BuildReason(prediction.Label, mention.Title, mention.Source, text);

            result.Headlines.Add(new NewsSentimentItem
            {
                Headline = mention.Title,
                Source = mention.Source,
                Label = prediction.Label,
                Score = prediction.Score,
                Reason = reason,
                Link = mention.Link
            });

            switch (prediction.Label.ToLowerInvariant())
            {
                case "positive":
                    result.PositiveCount++;
                    break;
                case "negative":
                    result.NegativeCount++;
                    break;
                default:
                    result.NeutralCount++;
                    break;
            }
        }

        if (result.Headlines.Count == 0)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Error = "FinBERT analysis unavailable. Add a free Hugging Face token in Settings.";
            return result;
        }

        ApplyAggregatePrediction(result);
        return result;
    }

    private static void ApplyAggregatePrediction(StockSentimentResult result)
    {
        var positiveScore = result.Headlines
            .Where(h => h.Label.Equals("positive", StringComparison.OrdinalIgnoreCase))
            .Sum(h => h.Score);

        var negativeScore = result.Headlines
            .Where(h => h.Label.Equals("negative", StringComparison.OrdinalIgnoreCase))
            .Sum(h => h.Score);

        var neutralScore = result.Headlines
            .Where(h => h.Label.Equals("neutral", StringComparison.OrdinalIgnoreCase))
            .Sum(h => h.Score);

        var total = positiveScore + negativeScore + neutralScore;
        if (total <= 0)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Confidence = 0;
            result.Reason = "Insufficient sentiment signal from news coverage.";
            return;
        }

        NewsSentimentItem? bestItem;
        if (positiveScore >= negativeScore && positiveScore >= neutralScore)
        {
            result.Prediction = SentimentPrediction.Bullish;
            result.Confidence = positiveScore / total;
            bestItem = result.Headlines
                .Where(h => h.Label.Equals("positive", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.Score)
                .FirstOrDefault();
        }
        else if (negativeScore >= positiveScore && negativeScore >= neutralScore)
        {
            result.Prediction = SentimentPrediction.Bearish;
            result.Confidence = negativeScore / total;
            bestItem = result.Headlines
                .Where(h => h.Label.Equals("negative", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.Score)
                .FirstOrDefault();
        }
        else
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Confidence = neutralScore / total;
            bestItem = result.Headlines
                .Where(h => h.Label.Equals("neutral", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.Score)
                .FirstOrDefault();
        }

        result.Reason = bestItem?.Reason
            ?? $"News from {string.Join(", ", result.Sources)} indicates {result.Prediction.ToString().ToLowerInvariant()} tone.";
    }

    private static string BuildReason(string label, string title, string source, string articleText)
    {
        var snippet = ExtractReasonSnippet(articleText, title);
        var sentiment = label.ToLowerInvariant() switch
        {
            "positive" => "Bullish",
            "negative" => "Bearish",
            _ => "Neutral"
        };

        return $"[{source}] {sentiment} tone — {snippet}";
    }

    private static string ExtractReasonSnippet(string articleText, string fallbackTitle)
    {
        var source = string.IsNullOrWhiteSpace(articleText) ? fallbackTitle : articleText;
        var normalized = Regex.Replace(source, @"\s+", " ").Trim();
        if (normalized.Length <= 140)
            return normalized;

        var sentenceEnd = normalized.IndexOf('.', 80);
        if (sentenceEnd is > 80 and < 180)
            return normalized[..(sentenceEnd + 1)].Trim();

        return normalized[..140].Trim() + "...";
    }

    private static string TruncateForAnalysis(string text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim()[..Math.Min(text.Trim().Length, MaxArticleChars)];

    private async Task<List<FeedEntry>> FetchFeedEntriesAsync(string feedUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(feedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = XDocument.Parse(xml);

            return document.Descendants("item")
                .Select(item => new FeedEntry
                {
                    Title = item.Element("title")?.Value?.Trim() ?? string.Empty,
                    Link = item.Element("link")?.Value?.Trim() ?? string.Empty
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<string> DownloadArticleTextAsync(string? link, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(link))
            return string.Empty;

        try
        {
            using var response = await _http.GetAsync(link, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return StripHtml(html);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<List<string>> FetchGoogleHeadlinesAsync(string symbol, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"{symbol} NSE stock");
        var url = $"https://news.google.com/rss/search?q={query}+when:7d&hl=en-IN&gl=IN&ceid=IN:en";
        var entries = await FetchFeedEntriesAsync(url, cancellationToken);
        return entries.Select(e => e.Title).Take(HeadlinesPerStock).ToList();
    }

    private static string StripHtml(string html)
    {
        var withoutScripts = Regex.Replace(html, "<script[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withoutStyles = Regex.Replace(withoutScripts, "<style[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var text = Regex.Replace(withoutStyles, "<[^>]+>", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private async Task<List<(string Label, double Score)?>> AnalyzeTextsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, HuggingFaceApiUrl);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    inputs = texts,
                    parameters = new
                    {
                        function_to_apply = "softmax",
                        top_k = 3
                    }
                }),
                Encoding.UTF8,
                "application/json");

            var token = _settings.Settings.HuggingFaceApiToken?.Trim();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, cancellationToken);

            if ((int)response.StatusCode == 503)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * (attempt + 1)), cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                return texts.Select(_ => ((string Label, double Score)?)null).ToList();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseFinBertBatchResponse(json, texts.Count);
        }

        return texts.Select(_ => ((string Label, double Score)?)null).ToList();
    }

    private static List<(string Label, double Score)?> ParseFinBertBatchResponse(string json, int expectedCount)
    {
        var results = new List<(string Label, double Score)?>(expectedCount);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in root.EnumerateArray())
            results.Add(ParseFinBertItem(item));

        while (results.Count < expectedCount)
            results.Add(null);

        return results;
    }

    private static (string Label, double Score)? ParseFinBertItem(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
        {
            var best = item.EnumerateArray()
                .OrderByDescending(entry => entry.TryGetProperty("score", out var score) ? score.GetDouble() : 0)
                .First();

            if (best.TryGetProperty("label", out var label) && best.TryGetProperty("score", out var bestScore))
                return (label.GetString() ?? "neutral", bestScore.GetDouble());
        }

        if (item.TryGetProperty("label", out var singleLabel) && item.TryGetProperty("score", out var singleScore))
            return (singleLabel.GetString() ?? "neutral", singleScore.GetDouble());

        return null;
    }

    private void SortResults()
    {
        _results.Sort((a, b) =>
        {
            var predictionCompare = b.Confidence.CompareTo(a.Confidence);
            return predictionCompare != 0
                ? predictionCompare
                : string.Compare(a.Symbol, b.Symbol, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void NotifyUpdated() => Updated?.Invoke();

    private enum ScanMode
    {
        NewsFeeds,
        Symbols
    }

    private sealed class FeedEntry
    {
        public string Title { get; init; } = string.Empty;
        public string Link { get; init; } = string.Empty;
    }

    private sealed class StockNewsMention
    {
        public string Symbol { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string? Link { get; init; }
        public string FeedUrl { get; init; } = string.Empty;
    }
}
