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

    public decimal NiftyPrice { get; private set; } = 25325.40m;
    public decimal NiftyChange { get; private set; } = 82.35m;
    public decimal NiftyChangePercent { get; private set; } = 0.33m;
    public TrendDirection NiftyTrend { get; private set; } = TrendDirection.Buy;
    public MultiTimeframeAnalysis Analysis { get; private set; } = new();
    public Signal CurrentSignal { get; private set; } = new();
    public List<WatchItem> Watchlist { get; private set; } = new();
    public List<Candle> ChartCandles { get; private set; } = new();
    public string SelectedTimeframe { get; set; } = "5m";

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
        NiftyPrice = await _marketData.GetCurrentPriceAsync("NSE:NIFTY 50");
        Analysis = await _signal.AnalyzeAsync("NIFTY");
        CurrentSignal = await _signal.GenerateSignalAsync("NIFTY");
        NiftyTrend = Analysis.Trend5M;
        ChartCandles = await _marketData.GetCandlesAsync("NSE:NIFTY 50", SelectedTimeframe, 60);
        Watchlist = _watchlist.Items;
        Notify();
        _marketData.StartStreaming();
    }

    public async Task ChangeTimeframeAsync(string timeframe)
    {
        SelectedTimeframe = timeframe;
        ChartCandles = await _marketData.GetCandlesAsync("NSE:NIFTY 50", timeframe, 60);
        Notify();
    }

    public async Task RefreshAsync()
    {
        Analysis = await _signal.AnalyzeAsync("NIFTY");
        CurrentSignal = await _signal.GenerateSignalAsync("NIFTY");
        NiftyTrend = Analysis.Trend5M;
        Notify();
    }

    private void OnPriceUpdated(string instrument, decimal price)
    {
        if (instrument == "NSE:NIFTY 50")
        {
            NiftyPrice = price;
            Notify(nameof(NiftyPrice));
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
    }
}
