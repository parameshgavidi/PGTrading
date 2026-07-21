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

            var quotes = await _zerodha.GetQuotesAsync(instruments);

            IndexItems = await BuildWatchItemsAsync(indexSymbols, quotes, markFavorites: true);
            Top10WeightItems = await BuildWatchItemsAsync(top10Symbols, quotes);
            TopWeightageItems = await BuildWatchItemsAsync(allStockSymbols, quotes);
        }
        finally
        {
            IsLoading = false;
            WatchlistUpdated?.Invoke();
        }
    }

    private async Task<List<WatchItem>> BuildWatchItemsAsync(
        IReadOnlyList<string> symbols,
        Dictionary<string, decimal> quotes,
        bool markFavorites = false)
    {
        var items = new List<WatchItem>();

        for (var i = 0; i < symbols.Count; i++)
        {
            var symbol = symbols[i];
            var instrument = InstrumentMapper.ToZerodhaKey(symbol);
            var price = quotes.GetValueOrDefault(instrument, 0m);
            var analysis = price > 0
                ? await _signal.AnalyzeAsync(symbol)
                : new MultiTimeframeAnalysis();

            items.Add(new WatchItem
            {
                Symbol = symbol,
                Name = InstrumentMapper.ToDisplayName(symbol),
                Rank = i + 1,
                LastPrice = price,
                Change = 0m,
                ChangePercent = 0m,
                Trend = analysis.Trend5M,
                IsFavorite = markFavorites
            });
        }

        return items;
    }
}
