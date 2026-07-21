using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PGOne.Models;

namespace PGOne.Services;

public interface ISentimentService
{
    bool IsScanning { get; }
    string? ProgressMessage { get; }
    IReadOnlyList<StockSentimentResult> Results { get; }
    event Action? Updated;
    Task ScanAsync(IReadOnlyList<string>? symbols = null, CancellationToken cancellationToken = default);
}

public class SentimentService : ISentimentService
{
    private const string FinBertModel = "ProsusAI/finbert";
    private const string HuggingFaceApiUrl = $"https://api-inference.huggingface.co/models/{FinBertModel}";
    private const int HeadlinesPerStock = 5;
    private const int MaxRetries = 3;

    private readonly ISettingsService _settings;
    private readonly HttpClient _http;
    private readonly List<StockSentimentResult> _results = new();

    public bool IsScanning { get; private set; }
    public string? ProgressMessage { get; private set; }
    public IReadOnlyList<StockSentimentResult> Results => _results;
    public event Action? Updated;

    public SentimentService(ISettingsService settings)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
    }

    public async Task ScanAsync(IReadOnlyList<string>? symbols = null, CancellationToken cancellationToken = default)
    {
        if (IsScanning)
            return;

        await _settings.LoadAsync();

        IsScanning = true;
        _results.Clear();
        ProgressMessage = "Starting sentiment scan...";
        NotifyUpdated();

        symbols ??= NiftyConstituents.TopWeightage;
        var completed = 0;

        try
        {
            foreach (var symbol in symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ProgressMessage = $"Analyzing {symbol} ({completed + 1}/{symbols.Count})...";
                NotifyUpdated();

                var result = await AnalyzeStockAsync(symbol, cancellationToken);
                _results.Add(result);
                completed++;

                ProgressMessage = $"Analyzed {symbol} ({completed}/{symbols.Count})";
                NotifyUpdated();

                // Hugging Face free tier is rate-limited; brief pause between stocks.
                if (completed < symbols.Count)
                    await Task.Delay(350, cancellationToken);
            }

            _results.Sort((a, b) =>
            {
                var predictionCompare = b.Confidence.CompareTo(a.Confidence);
                return predictionCompare != 0
                    ? predictionCompare
                    : string.Compare(a.Symbol, b.Symbol, StringComparison.OrdinalIgnoreCase);
            });

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

    private async Task<StockSentimentResult> AnalyzeStockAsync(string symbol, CancellationToken cancellationToken)
    {
        var result = new StockSentimentResult { Symbol = symbol };

        try
        {
            var headlines = await FetchNewsHeadlinesAsync(symbol, cancellationToken);
            result.NewsCount = headlines.Count;

            if (headlines.Count == 0)
            {
                result.Prediction = SentimentPrediction.Neutral;
                result.Confidence = 0;
                result.Error = "No recent news found.";
                return result;
            }

            var predictions = await AnalyzeTextsAsync(headlines, cancellationToken);
            for (var i = 0; i < headlines.Count; i++)
            {
                if (i >= predictions.Count || predictions[i] is null)
                    continue;

                var prediction = predictions[i]!.Value;
                result.Headlines.Add(new NewsSentimentItem
                {
                    Headline = headlines[i],
                    Label = prediction.Label,
                    Score = prediction.Score
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
                result.Confidence = 0;
                result.Error = "FinBERT analysis unavailable. Add a free Hugging Face token in Settings.";
                return result;
            }

            ApplyAggregatePrediction(result);
        }
        catch (Exception ex)
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Error = ex.Message;
        }

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
            return;
        }

        if (positiveScore >= negativeScore && positiveScore >= neutralScore)
        {
            result.Prediction = SentimentPrediction.Bullish;
            result.Confidence = positiveScore / total;
        }
        else if (negativeScore >= positiveScore && negativeScore >= neutralScore)
        {
            result.Prediction = SentimentPrediction.Bearish;
            result.Confidence = negativeScore / total;
        }
        else
        {
            result.Prediction = SentimentPrediction.Neutral;
            result.Confidence = neutralScore / total;
        }
    }

    private async Task<List<string>> FetchNewsHeadlinesAsync(string symbol, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"{symbol} NSE stock");
        var url = $"https://news.google.com/rss/search?q={query}+when:7d&hl=en-IN&gl=IN&ceid=IN:en";

        using var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = XDocument.Parse(xml);

        return document.Descendants("item")
            .Select(item => item.Element("title")?.Value?.Trim())
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => title!)
            .Take(HeadlinesPerStock)
            .ToList();
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
        {
            results.Add(ParseFinBertItem(item));
        }

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

    private void NotifyUpdated() => Updated?.Invoke();
}
