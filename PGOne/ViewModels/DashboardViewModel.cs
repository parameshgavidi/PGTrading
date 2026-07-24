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
    private readonly IZerodhaService _zerodha;
    private readonly ITrailingStopLossService _trailingStop;

    public event PropertyChangedEventHandler? PropertyChanged;

    public decimal NiftyPrice { get; private set; }
    public decimal NiftyChange { get; private set; }
    public decimal NiftyChangePercent { get; private set; }
    private decimal _priceReference;
    public TrendDirection NiftyTrend { get; private set; } = TrendDirection.Neutral;
    public MultiTimeframeAnalysis Analysis { get; private set; } = new();
    public Signal CurrentSignal { get; private set; } = new();
    public List<WatchItem> IndexWatchlist { get; private set; } = new();
    public List<WatchItem> Top10Watchlist { get; private set; } = new();
    public List<Candle> ChartCandles { get; private set; } = new();
    public List<Position> Positions { get; private set; } = new();
    public List<TrailingStopRow> TrailingStopItems { get; private set; } = new();

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

    public bool IsConnected => _zerodha.IsConnected;
    public bool IsPositionsLoading { get; private set; }
    public bool IsTrailingStopLoading => _trailingStop.IsLoading;
    public string? TrailingStopStatusMessage => _trailingStop.StatusMessage;
    public bool IsTrailingStopMonitoring => _trailingStop.IsMonitoring;
    public int TrailingStopTriggeredCount => TrailingStopItems.Count(i => i.IsTriggered && !i.ExitPlaced);
    public int TrailingStopMonitoringCount => TrailingStopItems.Count;

    public DashboardViewModel(
        IMarketDataService marketData,
        ISignalService signal,
        IWatchlistService watchlist,
        IZerodhaService zerodha,
        ITrailingStopLossService trailingStop)
    {
        _marketData = marketData;
        _signal = signal;
        _watchlist = watchlist;
        _zerodha = zerodha;
        _trailingStop = trailingStop;

        _marketData.PriceUpdated += OnPriceUpdated;
        _watchlist.WatchlistUpdated += OnWatchlistUpdated;
        _trailingStop.Updated += OnTrailingStopUpdated;
    }

    public async Task InitializeAsync()
    {
        await LoadChartAsync();
        await UpdatePriceAsync();
        Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
        CurrentSignal = await _signal.GenerateSignalAsync(SelectedSymbol);
        UpdateSelectedTrend();
        await _watchlist.RefreshTopWeightageAsync();
        SyncWatchlists();
        await RefreshPositionsAsync();
        await _trailingStop.RefreshAsync();
        TrailingStopItems = _trailingStop.Items.ToList();
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
        await _watchlist.RefreshTopWeightageAsync();
        SyncWatchlists();
        await RefreshPositionsAsync();
        await _trailingStop.RefreshAsync();
        TrailingStopItems = _trailingStop.Items.ToList();
        Notify();
    }

    public async Task RefreshPositionsAsync()
    {
        IsPositionsLoading = true;
        Notify(nameof(IsPositionsLoading));

        try
        {
            Positions = IsConnected
                ? await _zerodha.GetPositionsAsync()
                : new List<Position>();
        }
        finally
        {
            IsPositionsLoading = false;
            Notify(nameof(Positions));
            Notify(nameof(IsPositionsLoading));
            Notify(nameof(IsConnected));
        }
    }

    public async Task RefreshTrailingStopAsync()
    {
        await _trailingStop.RefreshAsync();
        TrailingStopItems = _trailingStop.Items.ToList();
        Notify(nameof(TrailingStopItems));
        Notify(nameof(IsTrailingStopLoading));
        Notify(nameof(TrailingStopStatusMessage));
        Notify(nameof(IsTrailingStopMonitoring));
        Notify(nameof(TrailingStopTriggeredCount));
        Notify(nameof(TrailingStopMonitoringCount));
    }

    public async Task SetTrailingStopMonitoringAsync(bool enabled)
        => await _trailingStop.SetMonitoringAsync(enabled);

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
        if (_priceReference > 0)
        {
            NiftyChange = price - _priceReference;
            NiftyChangePercent = Math.Round(NiftyChange / _priceReference * 100, 2);
        }

        Notify(nameof(NiftyPrice));
        Notify(nameof(NiftyChange));
        Notify(nameof(NiftyChangePercent));
    }

    private void OnWatchlistUpdated()
    {
        SyncWatchlists();
        Notify();
    }

    private void SyncWatchlists()
    {
        IndexWatchlist = _watchlist.IndexItems;
        Top10Watchlist = _watchlist.Top10WeightItems;
    }

    private void OnTrailingStopUpdated()
    {
        TrailingStopItems = _trailingStop.Items.ToList();
        Notify();
    }

    private async Task UpdatePriceAsync()
    {
        var quote = await _marketData.GetQuoteAsync(SelectedInstrument);
        if (quote is { LastPrice: > 0 })
        {
            NiftyPrice = quote.LastPrice;
            _priceReference = quote.PreviousClose > 0 ? quote.PreviousClose : quote.Open;
            NiftyChange = quote.Change;
            NiftyChangePercent = quote.ChangePercent;
            return;
        }

        _priceReference = 0;
        UpdatePriceFromCandles();
    }

    private void UpdatePriceFromCandles()
    {
        if (ChartCandles.Count == 0)
            return;

        var last = ChartCandles[^1];
        NiftyPrice = last.Close;

        var reference = GetPreviousCloseFromCandles();
        if (reference > 0)
        {
            _priceReference = reference;
            NiftyChange = last.Close - reference;
            NiftyChangePercent = Math.Round(NiftyChange / reference * 100, 2);
        }
    }

    private decimal GetPreviousCloseFromCandles()
    {
        if (ChartCandles.Count == 0)
            return 0;

        var sessionClose = MarketHours.GetLastSessionClose(MarketHours.GetIstNow());
        Candle? priorSessionCandle = null;
        foreach (var candle in ChartCandles)
        {
            if (candle.Timestamp <= sessionClose)
                priorSessionCandle = candle;
            else
                break;
        }

        return priorSessionCandle?.Close ?? ChartCandles[0].Open;
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
        if (property != null)
            return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSymbol)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDisplayName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedInstrument)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Analysis)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSignal)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IndexWatchlist)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Top10Watchlist)));
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Positions)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPositionsLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTrailingStopLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopStatusMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTrailingStopMonitoring)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopTriggeredCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrailingStopMonitoringCount)));
    }
}
