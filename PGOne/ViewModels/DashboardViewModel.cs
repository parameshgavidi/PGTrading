using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IMarketDataService _marketData;
    private readonly ISignalService _signal;
    private readonly IWatchlistService _watchlist;

    public event PropertyChangedEventHandler? PropertyChanged;

    public decimal NiftyPrice { get; private set; }
    public decimal NiftyChange { get; private set; }
    public decimal NiftyChangePercent { get; private set; }
    public TrendDirection NiftyTrend { get; private set; } = TrendDirection.Neutral;
    public MultiTimeframeAnalysis Analysis { get; private set; } = new();
    public Signal CurrentSignal { get; private set; } = new();
    public List<WatchItem> Watchlist { get; private set; } = new();
    public List<Candle> ChartCandles { get; private set; } = new();
    public string SelectedTimeframe { get; set; } = "5m";
    public int ChartVersion { get; private set; }
    public bool IsMarketOpen => _marketData.IsMarketOpen;
    public string MarketStatus => IsMarketOpen ? "Market Open" : "Market Closed";

    public DashboardViewModel(IMarketDataService marketData, ISignalService signal, IWatchlistService watchlist)
    {
        _marketData = marketData;
        _signal = signal;
        _watchlist = watchlist;
        _marketData.PriceUpdated += OnPriceUpdated;
        _watchlist.WatchlistUpdated += () => { Watchlist = _watchlist.Items; Notify(); };
    }

    public async Task InitializeAsync()
    {
        ChartCandles = await _marketData.GetCandlesAsync("NSE:NIFTY 50", SelectedTimeframe, 60);
        ChartVersion++;
        UpdatePriceFromCandles();
        Analysis = await _signal.AnalyzeAsync("NIFTY");
        CurrentSignal = await _signal.GenerateSignalAsync("NIFTY");
        NiftyTrend = Analysis.Trend5M;
        Watchlist = _watchlist.Items;
        Notify();
        _marketData.StartStreaming();
    }

    public async Task ChangeTimeframeAsync(string timeframe)
    {
        SelectedTimeframe = timeframe;
        ChartCandles = await _marketData.GetCandlesAsync("NSE:NIFTY 50", timeframe, 60);
        UpdatePriceFromCandles();
        Notify();
    }

    public async Task RefreshAsync()
    {
        ChartCandles = await _marketData.GetCandlesAsync("NSE:NIFTY 50", SelectedTimeframe, 60);
        ChartVersion++;
        UpdatePriceFromCandles();
        Analysis = await _signal.AnalyzeAsync("NIFTY");
        CurrentSignal = await _signal.GenerateSignalAsync("NIFTY");
        NiftyTrend = Analysis.Trend5M;
        Notify();
    }

    private void OnPriceUpdated(string instrument, decimal price)
    {
        if (instrument != "NSE:NIFTY 50")
            return;

        NiftyPrice = price;
        Notify(nameof(NiftyPrice));
    }

    private void UpdatePriceFromCandles()
    {
        if (ChartCandles.Count == 0)
            return;

        var last = ChartCandles[^1];
        NiftyPrice = last.Close;

        if (ChartCandles.Count > 1)
        {
            var prev = ChartCandles[^2].Close;
            NiftyChange = last.Close - prev;
            NiftyChangePercent = prev == 0 ? 0 : Math.Round(NiftyChange / prev * 100, 2);
        }
    }

    private void Notify([CallerMemberName] string? property = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        if (property != null) return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Analysis)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSignal)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Watchlist)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartCandles)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartVersion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarketOpen)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarketStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NiftyChange)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NiftyChangePercent)));
    }
}
