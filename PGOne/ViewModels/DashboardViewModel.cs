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
    public string SelectedSymbol { get; private set; } = "NIFTY";
    public string SelectedInstrument { get; private set; } = "NSE:NIFTY 50";
    public string SelectedDisplayName => InstrumentMapper.ToDisplayName(SelectedSymbol);
    public int ChartVersion { get; private set; }
    public bool IsChartFromZerodha { get; private set; }
    public string? ChartDataMessage { get; private set; }
    public string? LastCandleSummary { get; private set; }
    public bool IsMarketOpen => _marketData.IsMarketOpen;
    public string MarketStatus => IsMarketOpen ? "Market Open" : "Market Closed";

    public DashboardViewModel(IMarketDataService marketData, ISignalService signal, IWatchlistService watchlist)
    {
        _marketData = marketData;
        _signal = signal;
        _watchlist = watchlist;
        _marketData.PriceUpdated += OnPriceUpdated;
        _watchlist.WatchlistUpdated += () => { Watchlist = _watchlist.TopWeightageItems; Notify(); };
    }

    public async Task InitializeAsync()
    {
        await LoadChartAsync();
        await UpdatePriceAsync();
        Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
        CurrentSignal = await _signal.GenerateSignalAsync(SelectedSymbol);
        UpdateSelectedTrend();
        await _watchlist.RefreshTopWeightageAsync();
        Watchlist = _watchlist.TopWeightageItems;
        Notify();
        _marketData.StartStreaming(SelectedInstrument);
    }

    public async Task SelectInstrumentAsync(string symbol)
    {
        if (string.Equals(SelectedSymbol, symbol, StringComparison.OrdinalIgnoreCase))
            return;

        SelectedSymbol = symbol.ToUpper();
        SelectedInstrument = InstrumentMapper.ToZerodhaKey(SelectedSymbol);
        await LoadChartAsync();
        await UpdatePriceAsync();
        Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
        CurrentSignal = await _signal.GenerateSignalAsync(SelectedSymbol);
        UpdateSelectedTrend();
        _marketData.StartStreaming(SelectedInstrument);
        Notify();
    }

    public async Task ChangeTimeframeAsync(string timeframe)
    {
        SelectedTimeframe = timeframe;
        await LoadChartAsync();
        await UpdatePriceAsync();
        UpdateSelectedTrend();
        Notify();
    }

    public async Task RefreshAsync()
    {
        await LoadChartAsync();
        await UpdatePriceAsync();
        Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
        CurrentSignal = await _signal.GenerateSignalAsync(SelectedSymbol);
        UpdateSelectedTrend();
        Notify();
    }

    private async Task LoadChartAsync()
    {
        var count = GetCandleCount(SelectedTimeframe);
        var result = await _marketData.GetCandlesResultAsync(SelectedInstrument, SelectedTimeframe, count);
        ChartCandles = result.Candles;
        ChartVersion++;
        IsChartFromZerodha = result.IsFromZerodha;
        ChartDataMessage = result.IsFromZerodha
            ? $"Zerodha {SelectedTimeframe} candles ({ChartCandles.Count} bars)"
            : result.Error ?? "Demo candle data";
        LastCandleSummary = BuildLastCandleSummary();
    }

    // Show a sensible window per timeframe (an NSE session is ~6h15m):
    // 5m ≈ 1.5 sessions, 15m ≈ 3 sessions, 1H ≈ 8 sessions, 1D ≈ 3 months.
    private static int GetCandleCount(string timeframe) => timeframe switch
    {
        "5m" => 108,
        "15m" => 75,
        "1H" => 60,
        "1D" => 90,
        _ => 60
    };

    private string? BuildLastCandleSummary()
    {
        if (ChartCandles.Count == 0)
            return null;

        var last = ChartCandles[^1];
        return $"{last.Timestamp:dd MMM HH:mm}  O {last.Open:N2}  H {last.High:N2}  L {last.Low:N2}  C {last.Close:N2}";
    }

    private void OnPriceUpdated(string instrument, decimal price)
    {
        if (instrument != SelectedInstrument)
            return;

        NiftyPrice = price;
        Notify(nameof(NiftyPrice));
    }

    private async Task UpdatePriceAsync()
    {
        var quote = await _marketData.GetQuoteAsync(SelectedInstrument);
        if (quote is { LastPrice: > 0 })
        {
            NiftyPrice = quote.LastPrice;
            var reference = IsMarketOpen ? quote.Open : quote.PreviousClose;
            if (reference > 0)
            {
                NiftyChange = quote.LastPrice - reference;
                NiftyChangePercent = Math.Round(NiftyChange / reference * 100, 2);
            }
            return;
        }

        UpdatePriceFromCandles();
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

    private void UpdateSelectedTrend()
    {
        NiftyTrend = SelectedTimeframe switch
        {
            "1H" => Analysis.Trend1H,
            "15m" => Analysis.Trend15M,
            "1D" => Analysis.Trend1H,
            _ => Analysis.Trend5M
        };
    }

    private void Notify([CallerMemberName] string? property = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        if (property != null) return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSymbol)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDisplayName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedInstrument)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Analysis)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSignal)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Watchlist)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartCandles)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartVersion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarketOpen)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarketStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NiftyChange)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NiftyChangePercent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NiftyTrend)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChartFromZerodha)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartDataMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastCandleSummary)));
    }
}
