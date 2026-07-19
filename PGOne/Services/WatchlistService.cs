using PGOne.Models;

namespace PGOne.Services;

public interface IWatchlistService
{
    List<WatchItem> Items { get; }
    Task RefreshAsync();
    event Action? WatchlistUpdated;
}

public class WatchlistService : IWatchlistService
{
    private readonly IZerodhaService _zerodha;
    private readonly ISignalService _signal;

    public List<WatchItem> Items { get; private set; } = new();
    public event Action? WatchlistUpdated;

    private static readonly string[] DefaultSymbols =
    {
        "NSE:NIFTY 50", "NSE:NIFTY BANK", "NSE:RELIANCE",
        "NSE:INFY", "NSE:TCS", "NSE:SBIN", "NSE:HDFCBANK"
    };

    public WatchlistService(IZerodhaService zerodha, ISignalService signal)
    {
        _zerodha = zerodha;
        _signal = signal;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var quotes = await _zerodha.GetQuotesAsync(DefaultSymbols);
        var items = new List<WatchItem>();

        foreach (var (symbol, price) in quotes)
        {
            var name = symbol.Replace("NSE:", "").Replace("NIFTY 50", "NIFTY").Replace("NIFTY BANK", "BANKNIFTY");
            var analysis = await _signal.AnalyzeAsync(name);

            items.Add(new WatchItem
            {
                Symbol = name,
                Name = name,
                LastPrice = price,
                Change = price * 0.0033m,
                ChangePercent = 0.33m,
                Trend = analysis.Trend5M,
                IsFavorite = name == "NIFTY"
            });
        }

        Items = items;
        WatchlistUpdated?.Invoke();
    }
}
