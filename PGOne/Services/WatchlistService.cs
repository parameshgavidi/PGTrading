using PGOne.Models;

namespace PGOne.Services;

public interface IWatchlistService
{
    List<WatchItem> TopWeightageItems { get; }
    bool IsLoading { get; }
    event Action? WatchlistUpdated;
    Task RefreshTopWeightageAsync();
}

public class WatchlistService : IWatchlistService
{
    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;

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
            var symbols = NiftyConstituents.TopWeightage;
            var instruments = symbols.Select(s => $"NSE:{s}").ToArray();
            var quotes = await _zerodha.GetQuotesAsync(instruments);
            var items = new List<WatchItem>();

            for (var i = 0; i < symbols.Count; i++)
            {
                var symbol = symbols[i];
                var instrument = $"NSE:{symbol}";
                var price = quotes.GetValueOrDefault(instrument, 0m);
                var analysis = price > 0
                    ? await _signal.AnalyzeAsync(symbol)
                    : new MultiTimeframeAnalysis();

                items.Add(new WatchItem
                {
                    Symbol = symbol,
                    Name = symbol,
                    Rank = i + 1,
                    LastPrice = price,
                    Change = 0m,
                    ChangePercent = 0m,
                    Trend = analysis.Trend5M,
                    IsFavorite = i < 5
                });
            }

            TopWeightageItems = items;
        }
        finally
        {
            IsLoading = false;
            WatchlistUpdated?.Invoke();
        }
    }
}
