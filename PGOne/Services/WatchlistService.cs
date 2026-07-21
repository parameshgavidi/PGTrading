using PGOne.Models;

namespace PGOne.Services;

public interface IWatchlistService
{
    List<WatchItem> IndexItems { get; }
    List<WatchItem> Top10WeightItems { get; }
    List<WatchItem> TopWeightageItems { get; }
    bool IsLoading { get; }
    event Action? WatchlistUpdated;
    Task RefreshTopWeightageAsync();
}

public class WatchlistService : IWatchlistService
{
    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;

    public List<WatchItem> IndexItems { get; private set; } = new();
    public List<WatchItem> Top10WeightItems { get; private set; } = new();
    public List<WatchItem> TopWeightageItems { get; private set; } = new();
    public bool IsLoading { get; private set; }
    public event Action? WatchlistUpdated;

    public WatchlistService(IZerodhaService zerodha, ISignalService signal)
    {
        _zerodha = zerodha;
        _signal = signal;
    }

    public async Task RefreshTopWeightageAsync()
    {
        IsLoading = true;
        WatchlistUpdated?.Invoke();

        try
        {
            var indexSymbols = NiftyConstituents.DashboardIndices;
            var top10Symbols = NiftyConstituents.Top10Weightage;
            var allStockSymbols = NiftyConstituents.FullTopWeightageWatchlist;

            var uniqueSymbols = indexSymbols
                .Concat(allStockSymbols)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var instruments = new string[uniqueSymbols.Count];
            for (var i = 0; i < uniqueSymbols.Count; i++)
                instruments[i] = InstrumentMapper.ToZerodhaKey(uniqueSymbols[i]);

            var quotes = await _zerodha.GetInstrumentQuotesAsync(instruments);
            var sparklines = await BuildSparklinesAsync(top10Symbols);

            IndexItems = await BuildWatchItemsAsync(indexSymbols, quotes, sparklines, markFavorites: true);
            Top10WeightItems = await BuildWatchItemsAsync(top10Symbols, quotes, sparklines);
            TopWeightageItems = await BuildWatchItemsAsync(allStockSymbols, quotes, sparklines);
        }
        finally
        {
            IsLoading = false;
            WatchlistUpdated?.Invoke();
        }
    }

    private async Task<Dictionary<string, string>> BuildSparklinesAsync(IReadOnlyList<string> symbols)
    {
        var sparklines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tasks = symbols.Select(async symbol =>
        {
            try
            {
                var instrument = InstrumentMapper.ToZerodhaKey(symbol);
                var candles = await _zerodha.GetHistoricalCandlesAsync(instrument, "5m", 8);
                var closes = candles.Select(c => c.Close).ToList();
                sparklines[symbol] = SparklineHelper.GenerateFromCloses(closes);
            }
            catch
            {
                sparklines[symbol] = string.Empty;
            }
        });

        await Task.WhenAll(tasks);
        return sparklines;
    }

    private async Task<List<WatchItem>> BuildWatchItemsAsync(
        IReadOnlyList<string> symbols,
        Dictionary<string, InstrumentQuote> quotes,
        Dictionary<string, string> sparklines,
        bool markFavorites = false)
    {
        var items = new List<WatchItem>();

        for (var i = 0; i < symbols.Count; i++)
        {
            var symbol = symbols[i];
            var instrument = InstrumentMapper.ToZerodhaKey(symbol);
            quotes.TryGetValue(instrument, out var quote);
            var price = quote?.LastPrice ?? 0m;
            var analysis = price > 0
                ? await _signal.AnalyzeAsync(symbol)
                : new MultiTimeframeAnalysis();

            var changePct = quote?.ChangePercent ?? NiftyWeights.GetDemoChangePercent(symbol);
            var trend = analysis.Trend5M;
            sparklines.TryGetValue(symbol, out var sparkline);
            if (string.IsNullOrEmpty(sparkline))
                sparkline = SparklineHelper.Generate(trend, changePct);

            items.Add(new WatchItem
            {
                Symbol = symbol,
                Name = InstrumentMapper.ToDisplayName(symbol),
                Rank = i + 1,
                LastPrice = price,
                Change = quote?.Change ?? 0m,
                ChangePercent = changePct,
                Trend = trend,
                IsFavorite = markFavorites,
                Weight = NiftyWeights.GetWeight(symbol),
                Sparkline = sparkline
            });
        }

        return items;
    }
}
