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

    /// <summary>Top live market headlines (importance-ranked) with sentiment labels.</summary>
    IReadOnlyList<LiveNewsHeadline> TopLiveHeadlines { get; }
    bool IsLoadingTopLiveNews { get; }

    event Action? Updated;
    Task ScanNewsFeedsAsync(CancellationToken cancellationToken = default);
    Task ScanSymbolsAsync(IReadOnlyList<string>? symbols = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches live RSS news async, picks the top 5 most important / market-affecting stories,
    /// then runs a separate sentiment-analysis pass for those headlines.
    /// </summary>
    Task RefreshTopLiveNewsAsync(CancellationToken cancellationToken = default);
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
    private const int MaxRetries = 3;
    private const int TopLiveNewsCount = 5;
    private const int LiveNewsCandidatesPerFeed = 12;

    /// <summary>Minimum average positive/negative score to call a stock directional.</summary>
    private const double MinDirectionalScore = 0.40;

    /// <summary>Directional score must beat the opposite side by this margin.</summary>
    private const double MinDirectionalMargin = 0.06;

    private readonly ISettingsService _settings;
    private readonly INseSymbolResolver _nseSymbols;
    private readonly HttpClient _http;
    private readonly List<StockSentimentResult> _results = new();
    private readonly List<LiveNewsHeadline> _topLiveHeadlines = new();
    private readonly object _topLiveGate = new();

    public bool IsScanning { get; private set; }
    public string? ProgressMessage { get; private set; }
    public IReadOnlyList<StockSentimentResult> Results => _results;
    public IReadOnlyList<LiveNewsHeadline> TopLiveHeadlines => _topLiveHeadlines;
    public bool IsLoadingTopLiveNews { get; private set; }
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

    public async Task RefreshTopLiveNewsAsync(CancellationToken cancellationToken = default)
    {
        lock (_topLiveGate)
        {
            if (IsLoadingTopLiveNews)
                return;
            IsLoadingTopLiveNews = true;
        }

        NotifyUpdated();

        try
        {
            await _settings.LoadAsync();
            await _nseSymbols.EnsureLoadedAsync(cancellationToken);

            var candidates = await FetchLiveNewsCandidatesAsync(cancellationToken);
            var top = candidates
                .OrderByDescending(c => c.ImportanceScore)
                .ThenByDescending(c => c.RelatedSymbols.Count)
                .Take(TopLiveNewsCount)
                .ToList();

            // Separate method: sentiment analysis for the top affected / most important live news.
            var analyzed = await AnalyzeTopLiveNewsSentimentAsync(top, cancellationToken);

            _topLiveHeadlines.Clear();
            _topLiveHeadlines.AddRange(analyzed);
        }
        catch (OperationCanceledException)
        {
            // Caller cancelled — keep prior headlines if any.
        }
        catch
        {
            // Network/parse failures: leave previous headlines; dashboard shows empty-state if none.
        }
        finally
        {
            IsLoadingTopLiveNews = false;
            NotifyUpdated();
        }
    }

    /// <summary>
    /// Runs FinBERT (or keyword fallback) on the selected top live headlines.
    /// Kept separate from stock-universe scanning so dashboard news stays fast and focused.
    /// </summary>
    internal async Task<List<LiveNewsHeadline>> AnalyzeTopLiveNewsSentimentAsync(
        IReadOnlyList<LiveNewsCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
            return [];

        var texts = candidates
            .Select(c => SentimentTextHelper.PrepareAnalysisText(c.Title, bodySnippet: null))
            .ToList();

        var analysis = await AnalyzeTextsAsync(texts, cancellationToken);
        var results = new List<LiveNewsHeadline>(candidates.Count);

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var scores = i < analysis.Scores.Count ? analysis.Scores[i] : null;
            var vector = scores ?? SentimentTextHelper.ScoreWithKeywords(texts[i]);
            var (label, score) = vector.TopLabel();

            results.Add(new LiveNewsHeadline
            {
                Headline = SentimentTextHelper.CleanBoilerplate(candidate.Title),
                Source = candidate.Source,
                Link = candidate.Link,
                Label = label,
                Score = score,
                PositiveScore = vector.Positive,
                NegativeScore = vector.Negative,
                NeutralScore = vector.Neutral,
                ImportanceScore = candidate.ImportanceScore,
                RelatedSymbols = candidate.RelatedSymbols.ToList()
            });
        }

        return results;
    }

    private async Task<List<LiveNewsCandidate>> FetchLiveNewsCandidatesAsync(CancellationToken cancellationToken)
    {
        var byTitle = new Dictionary<string, LiveNewsCandidate>(StringComparer.Ordinal);

        foreach (var (sourceName, feedUrl) in NewsFeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = await FetchFeedEntriesAsync(feedUrl, cancellationToken);

            foreach (var entry in entries.Take(LiveNewsCandidatesPerFeed))
            {
                var key = LiveNewsImportance.NormalizeTitleKey(entry.Title);
                if (string.IsNullOrWhiteSpace(key) || key.Length < 12)
                    continue;

                var symbols = _nseSymbols.ResolveSymbolsInText(entry.Title).ToList();
                var importance = LiveNewsImportance.Score(entry.Title, sourceName, symbols);

                if (byTitle.TryGetValue(key, out var existing))
                {
                    if (importance > existing.ImportanceScore)
                    {
                        byTitle[key] = new LiveNewsCandidate
                        {
                            Title = entry.Title,
                            Source = sourceName,
                            Link = entry.Link,
                            RelatedSymbols = symbols,
                            ImportanceScore = importance
                        };
                    }

                    continue;
                }

                byTitle[key] = new LiveNewsCandidate
                {
                    Title = entry.Title,
                    Source = sourceName,
                    Link = entry.Link,
                    RelatedSymbols = symbols,
                    ImportanceScore = importance
                };
            }
        }

        return byTitle.Values.ToList();
    }

    private async Task RunScanAsync(
        ScanMode mode,
        IReadOnlyList<string>? symbols,
        CancellationToken cancellationToken)
    {
        if (IsScanning)
            return;

        await _settings.LoadAsync();
        await _nseSymbols.EnsureLoadedAsync(cancellationToken);

        var hasToken = !string.IsNullOrWhiteSpace(_settings.Settings.HuggingFaceApiToken);
        if (!hasToken)
        {
            ProgressMessage = "No Hugging Face token — using keyword fallback. Add a free token in Settings for FinBERT.";
            NotifyUpdated();
        }

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
                var articleSnippet = await DownloadArticleSnippetAsync(entry.Link, cancellationToken);
                var combinedText = $"{entry.Title}\n{articleSnippet}";
                var symbols = _nseSymbols.ResolveSymbolsInText(combinedText).ToList();
                if (symbols.Count == 0)
                    continue;

                foreach (var symbol in symbols)
                {
                    mentions.Add(new StockNewsMention
                    {
                        Symbol = symbol,
                        Title = entry.Title,
                        BodySnippet = articleSnippet,
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
            var articleSnippet = await DownloadArticleSnippetAsync(entry.Link, cancellationToken);
            mentions.Add(new StockNewsMention
            {
                Symbol = symbol,
                Title = entry.Title,
                BodySnippet = articleSnippet,
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
            BodySnippet = string.Empty,
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

        var analysisTexts = new List<string>();
        var textToMentions = new Dictionary<string, List<StockNewsMention>>(StringComparer.Ordinal);

        foreach (var mention in mentions)
        {
            var text = SentimentTextHelper.PrepareAnalysisText(mention.Title, mention.BodySnippet);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (!textToMentions.TryGetValue(text, out var list))
            {
                list = new List<StockNewsMention>();
                textToMentions[text] = list;
                analysisTexts.Add(text);
            }

            list.Add(mention);
        }

        if (analysisTexts.Count == 0)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Error = "No readable article text found.";
            return result;
        }

        var analysis = await AnalyzeTextsAsync(analysisTexts, cancellationToken);
        if (analysis.UsedKeywordFallback)
        {
            result.Warning = string.IsNullOrWhiteSpace(_settings.Settings.HuggingFaceApiToken)
                ? "Keyword fallback (add a free Hugging Face token in Settings for FinBERT)."
                : "Keyword fallback (check Hugging Face token permissions in Settings).";
        }

        for (var i = 0; i < analysisTexts.Count; i++)
        {
            var text = analysisTexts[i];
            var scores = analysis.Scores[i];
            if (scores is null)
                continue;

            var (label, score) = scores.Value.TopLabel();
            var relatedMentions = textToMentions[text];

            foreach (var mention in relatedMentions)
            {
                var reason = BuildReason(label, mention.Title, mention.Source, mention.BodySnippet, analysis.UsedKeywordFallback);

                result.Headlines.Add(new NewsSentimentItem
                {
                    Headline = SentimentTextHelper.CleanBoilerplate(mention.Title),
                    Source = mention.Source,
                    Label = label,
                    Score = score,
                    PositiveScore = scores.Value.Positive,
                    NegativeScore = scores.Value.Negative,
                    NeutralScore = scores.Value.Neutral,
                    Reason = reason,
                    Link = mention.Link
                });

                switch (label.ToLowerInvariant())
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
        }

        if (result.Headlines.Count == 0)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Error = analysis.Error ?? "Sentiment analysis unavailable.";
            return result;
        }

        ApplyAggregatePrediction(result);
        return result;
    }

    private static void ApplyAggregatePrediction(StockSentimentResult result)
    {
        var count = result.Headlines.Count;
        if (count == 0)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Confidence = 0;
            result.Reason = "Insufficient sentiment signal from news coverage.";
            return;
        }

        var avgPositive = result.Headlines.Average(h => h.PositiveScore);
        var avgNegative = result.Headlines.Average(h => h.NegativeScore);
        var avgNeutral = result.Headlines.Average(h => h.NeutralScore);

        // Headline-count tie-breaker when scores are close.
        var positiveVotes = result.PositiveCount;
        var negativeVotes = result.NegativeCount;

        SentimentPrediction prediction;
        double confidence;
        string labelFilter;

        if (avgPositive >= MinDirectionalScore
            && avgPositive >= avgNegative + MinDirectionalMargin
            && positiveVotes >= negativeVotes)
        {
            prediction = SentimentPrediction.Bullish;
            confidence = avgPositive;
            labelFilter = "positive";
        }
        else if (avgNegative >= MinDirectionalScore
                 && avgNegative >= avgPositive + MinDirectionalMargin
                 && negativeVotes >= positiveVotes)
        {
            prediction = SentimentPrediction.Bearish;
            confidence = avgNegative;
            labelFilter = "negative";
        }
        else if (positiveVotes >= 2
                 && negativeVotes == 0
                 && avgPositive > avgNegative)
        {
            prediction = SentimentPrediction.Bullish;
            confidence = Math.Max(avgPositive, 0.42);
            labelFilter = "positive";
        }
        else if (negativeVotes >= 2
                 && positiveVotes == 0
                 && avgNegative > avgPositive)
        {
            prediction = SentimentPrediction.Bearish;
            confidence = Math.Max(avgNegative, 0.42);
            labelFilter = "negative";
        }
        else
        {
            prediction = SentimentPrediction.Neutral;
            confidence = avgNeutral;
            labelFilter = "neutral";
        }

        result.Prediction = prediction;
        result.Confidence = Math.Clamp(confidence, 0, 1);

        var bestItem = result.Headlines
            .Where(h => h.Label.Equals(labelFilter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Score)
            .FirstOrDefault()
            ?? result.Headlines.OrderByDescending(h => h.Score).First();

        result.Reason = bestItem.Reason;
    }

    private static string BuildReason(string label, string title, string source, string? bodySnippet, bool keywordFallback)
    {
        var snippet = SentimentTextHelper.ExtractReasonSnippet(bodySnippet, title);
        var sentiment = label.ToLowerInvariant() switch
        {
            "positive" => "Bullish",
            "negative" => "Bearish",
            _ => "Neutral"
        };

        var engine = keywordFallback ? "Keyword" : "FinBERT";
        return $"[{source}] {sentiment} ({engine}) — {snippet}";
    }

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

    private async Task<string> DownloadArticleSnippetAsync(string? link, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(link))
            return string.Empty;

        try
        {
            using var response = await _http.GetAsync(link, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return SentimentTextHelper.ExtractArticleSnippetFromHtml(html);
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

    private async Task<TextAnalysisResult> AnalyzeTextsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        var finbert = await TryAnalyzeWithFinBertAsync(texts, cancellationToken);
        if (finbert is not null && finbert.Any(s => s is not null))
            return new TextAnalysisResult(finbert, usedKeywordFallback: false);

        var token = _settings.Settings.HuggingFaceApiToken?.Trim();
        var error = string.IsNullOrEmpty(token)
            ? "Hugging Face token missing."
            : finbert is null
                ? "FinBERT request failed."
                : "FinBERT returned no predictions.";

        return new TextAnalysisResult(
            texts.Select(text => (SentimentScoreVector?)SentimentTextHelper.ScoreWithKeywords(text)).ToList(),
            usedKeywordFallback: true,
            error: error);
    }

    private async Task<List<SentimentScoreVector?>?> TryAnalyzeWithFinBertAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        var token = _settings.Settings.HuggingFaceApiToken?.Trim();
        if (string.IsNullOrEmpty(token))
            return null;

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

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, cancellationToken);

            if ((int)response.StatusCode == 503)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * (attempt + 1)), cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseFinBertBatchResponse(json, texts.Count);
            return parsed.Any(s => s is not null) ? parsed : null;
        }

        return null;
    }

    private static List<SentimentScoreVector?> ParseFinBertBatchResponse(string json, int expectedCount)
    {
        var results = new List<SentimentScoreVector?>(expectedCount);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in root.EnumerateArray())
            results.Add(ParseFinBertScoreVector(item));

        while (results.Count < expectedCount)
            results.Add(null);

        return results;
    }

    private static SentimentScoreVector? ParseFinBertScoreVector(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() == 0)
        {
            if (item.TryGetProperty("label", out var singleLabel) && item.TryGetProperty("score", out var singleScore))
            {
                var label = singleLabel.GetString() ?? "neutral";
                var score = singleScore.GetDouble();
                return LabelToVector(label, score);
            }

            return null;
        }

        double positive = 0, negative = 0, neutral = 0;

        foreach (var entry in item.EnumerateArray())
        {
            if (!entry.TryGetProperty("label", out var labelEl) || !entry.TryGetProperty("score", out var scoreEl))
                continue;

            var label = labelEl.GetString() ?? string.Empty;
            var score = scoreEl.GetDouble();

            if (label.Contains("positive", StringComparison.OrdinalIgnoreCase))
                positive = score;
            else if (label.Contains("negative", StringComparison.OrdinalIgnoreCase))
                negative = score;
            else
                neutral = score;
        }

        if (positive + negative + neutral <= 0)
            return null;

        return new SentimentScoreVector(positive, negative, neutral).Normalize();
    }

    private static SentimentScoreVector LabelToVector(string label, double score)
    {
        if (label.Contains("positive", StringComparison.OrdinalIgnoreCase))
            return new SentimentScoreVector(score, 0.15, 0.15).Normalize();

        if (label.Contains("negative", StringComparison.OrdinalIgnoreCase))
            return new SentimentScoreVector(0.15, score, 0.15).Normalize();

        return new SentimentScoreVector(0.15, 0.15, score).Normalize();
    }

    private void SortResults()
    {
        _results.Sort((a, b) =>
        {
            var predictionRank = GetPredictionRank(b.Prediction).CompareTo(GetPredictionRank(a.Prediction));
            if (predictionRank != 0)
                return predictionRank;

            var confidenceCompare = b.Confidence.CompareTo(a.Confidence);
            return confidenceCompare != 0
                ? confidenceCompare
                : string.Compare(a.Symbol, b.Symbol, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static int GetPredictionRank(SentimentPrediction prediction) => prediction switch
    {
        SentimentPrediction.Bullish => 2,
        SentimentPrediction.Bearish => 2,
        _ => 1
    };

    private void NotifyUpdated() => Updated?.Invoke();

    private enum ScanMode
    {
        NewsFeeds,
        Symbols
    }

    private sealed class TextAnalysisResult
    {
        public TextAnalysisResult(
            List<SentimentScoreVector?> scores,
            bool usedKeywordFallback,
            string? error = null)
        {
            Scores = scores;
            UsedKeywordFallback = usedKeywordFallback;
            Error = error;
        }

        public List<SentimentScoreVector?> Scores { get; }
        public bool UsedKeywordFallback { get; }
        public string? Error { get; }
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
        public string BodySnippet { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string? Link { get; init; }
        public string FeedUrl { get; init; } = string.Empty;
    }

    /// <summary>Feed candidate before sentiment labeling — ranked by <see cref="LiveNewsImportance"/>.</summary>
    internal sealed class LiveNewsCandidate
    {
        public string Title { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string? Link { get; init; }
        public List<string> RelatedSymbols { get; init; } = new();
        public double ImportanceScore { get; init; }
    }
}
