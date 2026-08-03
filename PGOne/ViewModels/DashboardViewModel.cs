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
    private readonly ITargetPnLMonitorService _targetPnL;
    private readonly ISettingsService _settings;
    private readonly IIntradayCprService _intradayCpr;

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
    public int OverlayVersion { get; private set; }
    public bool ShowPocOverlay { get; private set; } = true;
    public bool ShowPivotOverlay { get; private set; } = false;
    public bool ShowCamarillaOverlay { get; private set; } = false;
    public bool ShowKeltnerOverlay { get; private set; } = false;
    public bool ShowIntradayCprOverlay { get; private set; } = false;
    public bool ShowSuperTrendOverlay { get; private set; } = false;
    public bool ShowSuperTrend725Overlay { get; private set; } = false;
    public bool ShowEma20Overlay { get; private set; } = false;
    public bool ShowVwapOverlay { get; private set; } = false;
    public bool Supports5mStudyToggles => SelectedTimeframe == "5m";
    public bool SupportsIntradayStOverlays => SelectedTimeframe is not "1W";
    public bool SupportsSt725Overlays => SelectedTimeframe is "1m" or "5m" or "15m";
    public IReadOnlyList<IntradayCprSegment> CprSegments { get; private set; } = Array.Empty<IntradayCprSegment>();
    public decimal CurrentIntradayTc { get; private set; }
    public decimal CurrentIntradayPivot { get; private set; }
    public decimal CurrentIntradayBc { get; private set; }
    public bool AboveCpr { get; private set; }
    public string CprPositionLabel => AboveCpr ? "Above CPR" : "Below CPR";
    public string CprPositionClass => AboveCpr ? "above-cpr" : "below-cpr";
    public bool Is1mCprChart => SelectedTimeframe == "1m";
    /// <summary>15m-pivot intraday CPR bands on 1m chart only.</summary>
    public bool SupportsIntradayCprOverlay => SelectedTimeframe == "1m";
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

    public decimal AggregatePnL => _targetPnL.AggregatePnL;
    public int TargetPnLOpenCount => _targetPnL.OpenPositionCount;
    public bool IsTargetPnLLoading => _targetPnL.IsLoading;
    public string? TargetPnLStatusMessage => _targetPnL.StatusMessage;
    public bool IsTargetPnLMonitoring => _targetPnL.IsMonitoring;
    public decimal TargetProfitAmount => _settings.Settings.TargetProfitAmount;
    public decimal TargetLossAmount => _settings.Settings.TargetLossAmount;
    public TargetPnLTrigger TargetPnLLastTrigger => _targetPnL.LastTrigger;
    public DateTime? RiskControlsLastUpdated { get; private set; }

    public bool IsDashboardReady { get; private set; }
    public string? StartupError { get; private set; }

    public DashboardViewModel(
        IMarketDataService marketData,
        ISignalService signal,
        IWatchlistService watchlist,
        IZerodhaService zerodha,
        ITrailingStopLossService trailingStop,
        ITargetPnLMonitorService targetPnL,
        ISettingsService settings,
        IIntradayCprService intradayCpr)
    {
        _marketData = marketData;
        _signal = signal;
        _watchlist = watchlist;
        _zerodha = zerodha;
        _trailingStop = trailingStop;
        _targetPnL = targetPnL;
        _settings = settings;
        _intradayCpr = intradayCpr;

        _marketData.PriceUpdated += OnPriceUpdated;
        _watchlist.WatchlistUpdated += OnWatchlistUpdated;
        _trailingStop.Updated += OnTrailingStopUpdated;
        _targetPnL.Updated += OnTargetPnLUpdated;
    }

    public async Task InitializeAsync()
    {
        if (IsDashboardReady)
            return;

        try
        {
            StartupError = null;
            await LoadChartAsync();
            await UpdatePriceAsync();
            Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
            CurrentSignal = await _signal.GenerateSignalAsync(SelectedSymbol);
            OverlayVersion++;
            UpdateSelectedTrend();
            await _watchlist.RefreshTopWeightageAsync();
            SyncWatchlists();
            await _settings.LoadAsync();
            await RefreshRiskControlsAsync();
            _marketData.StartStreaming(SelectedInstrument);
        }
        catch (Exception ex)
        {
            StartupError = ex.Message;
            ChartDataMessage ??= "Startup error — using partial data. Check Zerodha connection.";
        }
        finally
        {
            IsDashboardReady = true;
            Notify();
        }
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
        OverlayVersion++;
        UpdateSelectedTrend();
        _marketData.StartStreaming(SelectedInstrument);
        Notify();
    }

    public async Task ChangeTimeframeAsync(string timeframe)
    {
        if (SelectedTimeframe == timeframe)
            return;

        SelectedTimeframe = timeframe;
        await LoadChartAsync();
        OverlayVersion++;
        await UpdatePriceAsync();
        UpdateSelectedTrend();
        Notify(nameof(SelectedTimeframe));
        Notify(nameof(Is1mCprChart));
        Notify(nameof(SupportsIntradayCprOverlay));
        Notify(nameof(Supports5mStudyToggles));
        Notify(nameof(SupportsIntradayStOverlays));
        Notify(nameof(SupportsSt725Overlays));
        Notify(nameof(OverlayVersion));
        Notify();
    }

    /// <summary>1m CPR page: load 1m chart and enable 1m CPR overlay.</summary>
    public async Task Ensure1mCprPageAsync()
    {
        if (!IsDashboardReady)
            await InitializeAsync();

        if (SelectedTimeframe != "1m")
            await ChangeTimeframeAsync("1m");

        SetShowIntradayCprOverlay(true);
    }

    public async Task RefreshAsync()
    {
        await LoadChartAsync();
        await UpdatePriceAsync();
        Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
        CurrentSignal = await _signal.GenerateSignalAsync(SelectedSymbol);
        OverlayVersion++;
        UpdateSelectedTrend();
        await _watchlist.RefreshTopWeightageAsync();
        SyncWatchlists();
        await RefreshRiskControlsAsync();
        Notify();
    }

    public async Task RefreshRiskControlsAsync()
    {
        await RefreshPositionsAsync();

        if (!_trailingStop.IsMonitoring)
            await _trailingStop.RefreshAsync();

        TrailingStopItems = _trailingStop.Items.ToList();

        if (!_targetPnL.IsMonitoring)
            await _targetPnL.RefreshAsync();

        RiskControlsLastUpdated = DateTime.Now;
        NotifyTargetPnL();
        Notify(nameof(TrailingStopItems));
        Notify(nameof(IsTrailingStopLoading));
        Notify(nameof(TrailingStopStatusMessage));
        Notify(nameof(IsTrailingStopMonitoring));
        Notify(nameof(TrailingStopTriggeredCount));
        Notify(nameof(TrailingStopMonitoringCount));
        Notify(nameof(RiskControlsLastUpdated));
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
        => await RefreshRiskControlsAsync();

    public async Task SetTrailingStopMonitoringAsync(bool enabled)
    {
        await _trailingStop.SetMonitoringAsync(enabled);
        await RefreshRiskControlsAsync();
    }

    public async Task RefreshTargetPnLAsync()
        => await RefreshRiskControlsAsync();

    public async Task SetTargetPnLMonitoringAsync(bool enabled)
    {
        await _targetPnL.SetMonitoringAsync(enabled);
        await RefreshRiskControlsAsync();
    }

    public async Task SaveTargetPnLAmountsAsync(decimal profitAmount, decimal lossAmount)
    {
        await _settings.LoadAsync();
        _settings.Settings.TargetProfitAmount = Math.Max(0, profitAmount);
        _settings.Settings.TargetLossAmount = Math.Max(0, lossAmount);
        await _settings.SaveSettingsAsync();
        Notify(nameof(TargetProfitAmount));
        Notify(nameof(TargetLossAmount));
    }

    private void OnTargetPnLUpdated() => NotifyTargetPnL();

    private void NotifyTargetPnL()
    {
        Notify(nameof(AggregatePnL));
        Notify(nameof(TargetPnLOpenCount));
        Notify(nameof(IsTargetPnLLoading));
        Notify(nameof(TargetPnLStatusMessage));
        Notify(nameof(IsTargetPnLMonitoring));
        Notify(nameof(TargetPnLLastTrigger));
    }

    public void SetShowPocOverlay(bool show)
    {
        if (ShowPocOverlay == show)
            return;

        ShowPocOverlay = show;
        OverlayVersion++;
        Notify(nameof(ShowPocOverlay));
        Notify(nameof(OverlayVersion));
    }

    public void SetShowPivotOverlay(bool show)
    {
        if (ShowPivotOverlay == show)
            return;

        ShowPivotOverlay = show;
        OverlayVersion++;
        Notify(nameof(ShowPivotOverlay));
        Notify(nameof(OverlayVersion));
    }

    public void SetShowCamarillaOverlay(bool show)
    {
        if (ShowCamarillaOverlay == show)
            return;

        ShowCamarillaOverlay = show;
        OverlayVersion++;
        Notify(nameof(ShowCamarillaOverlay));
        Notify(nameof(OverlayVersion));
    }

    public void SetShowKeltnerOverlay(bool show)
    {
        if (ShowKeltnerOverlay == show)
            return;

        ShowKeltnerOverlay = show;
        OverlayVersion++;
        Notify(nameof(ShowKeltnerOverlay));
        Notify(nameof(OverlayVersion));
    }

    public void SetShowIntradayCprOverlay(bool show)
    {
        if (ShowIntradayCprOverlay == show)
            return;

        ShowIntradayCprOverlay = show;
        OverlayVersion++;
        Notify(nameof(ShowIntradayCprOverlay));
        Notify(nameof(OverlayVersion));
        Notify(nameof(CprSegments));
        Notify(nameof(AboveCpr));
        Notify(nameof(CprPositionLabel));
        Notify(nameof(CprPositionClass));
    }

    public void SetShowSuperTrendOverlay(bool show)
    {
        if (ShowSuperTrendOverlay == show)
            return;

        ShowSuperTrendOverlay = show;
        OverlayVersion++;
        Notify(nameof(ShowSuperTrendOverlay));
        Notify(nameof(OverlayVersion));
    }

    public void SetShowSuperTrend725Overlay(bool show)
    {
        if (ShowSuperTrend725Overlay == show)
            return;

        ShowSuperTrend725Overlay = show;
        OverlayVersion++;
        Notify(nameof(ShowSuperTrend725Overlay));
        Notify(nameof(OverlayVersion));
    }

    public void SetShowEma20Overlay(bool show)
    {
        if (ShowEma20Overlay == show)
            return;

        ShowEma20Overlay = show;
        OverlayVersion++;
        Notify(nameof(ShowEma20Overlay));
        Notify(nameof(OverlayVersion));
    }

    public void SetShowVwapOverlay(bool show)
    {
        if (ShowVwapOverlay == show)
            return;

        ShowVwapOverlay = show;
        OverlayVersion++;
        Notify(nameof(ShowVwapOverlay));
        Notify(nameof(OverlayVersion));
    }

    private async Task LoadChartAsync()
    {
        try
        {
            var count = GetCandleCount(SelectedTimeframe);
            var result = await _marketData.GetCandlesResultAsync(SelectedInstrument, SelectedTimeframe, count);

            if (SelectedTimeframe == "1m")
            {
                var sessionDate = GetChartSessionDate();
                var candles15m = await _marketData.GetCandlesResultAsync(SelectedInstrument, "15m", 80);
                CprSegments = _intradayCpr.BuildSegments(candles15m.Candles, sessionDate);

                var sessionCandles = result.Candles
                    .Where(c => c.Timestamp.Date == sessionDate)
                    .ToList();
                ChartCandles = sessionCandles.Count > 0 ? sessionCandles : result.Candles;
            }
            else
            {
                CprSegments = Array.Empty<IntradayCprSegment>();
                ChartCandles = result.Candles;
            }

            _marketData.ApplyChartIndicators(ChartCandles, SelectedTimeframe);

            var overlayDiag = BuildOverlayDiagnostic();
            ChartVersion++;
            Notify(nameof(ChartCandles));
            Notify(nameof(ChartVersion));
            Notify(nameof(CprSegments));
            IsChartFromZerodha = result.IsFromZerodha;
            ChartDataMessage = result.IsFromZerodha
                ? SelectedTimeframe switch
                {
                    "1m" => $"Zerodha 1m candles ({ChartCandles.Count} bars)",
                    "15m" => $"Zerodha 15m candles ({ChartCandles.Count} bars)",
                    _ => $"Zerodha {SelectedTimeframe} candles ({ChartCandles.Count} bars)"
                }
                : result.Error ?? "Demo candle data";
            if (!string.IsNullOrEmpty(overlayDiag))
                ChartDataMessage += overlayDiag;
            LastCandleSummary = BuildLastCandleSummary();
            UpdateIntradayCprState();
        }
        catch (Exception ex)
        {
            ChartDataMessage = $"Chart load failed: {ex.Message}";
            CprSegments = Array.Empty<IntradayCprSegment>();
            CurrentIntradayTc = 0;
            CurrentIntradayPivot = 0;
            CurrentIntradayBc = 0;
            AboveCpr = false;
        }
    }

    private string? BuildOverlayDiagnostic()
    {
        if (ChartCandles.Count == 0 || SelectedTimeframe != "5m")
            return null;

        var st725 = ChartCandles.Count(c => c.SuperTrendEntry.HasValue);
        var ema = ChartCandles.Count(c => c.Ema20.HasValue);
        var vwap = ChartCandles.Count(c => c.Vwap.HasValue);
        return $" · ST7 {st725}/{ChartCandles.Count} EMA {ema}/{ChartCandles.Count} VWAP {vwap}/{ChartCandles.Count}";
    }

    private static DateTime GetChartSessionDate()
    {
        var now = MarketHours.GetIstNow();
        if (!MarketHours.IsOpen(now) && now.TimeOfDay < MarketHours.OpenTime)
            return now.Date.AddDays(now.DayOfWeek == DayOfWeek.Monday ? -3 : -1);

        return now.Date;
    }

    private void UpdateIntradayCprState()
    {
        try
        {
            if (!SupportsIntradayCprOverlay || CprSegments.Count == 0)
            {
                CurrentIntradayTc = 0;
                CurrentIntradayPivot = 0;
                CurrentIntradayBc = 0;
                AboveCpr = false;
                return;
            }

            var activeTime = ChartCandles.Count > 0
                ? ChartCandles[^1].Timestamp
                : MarketHours.GetIstNow();

            var active = _intradayCpr.GetActiveSegment(CprSegments, activeTime);
            if (active is null)
                return;

            CurrentIntradayTc = active.Tc;
            CurrentIntradayPivot = active.Pivot;
            CurrentIntradayBc = active.Bc;

            var lastClose = ChartCandles.Count > 0 ? ChartCandles[^1].Close : 0m;
            var price = NiftyPrice > 0 ? NiftyPrice : lastClose;
            AboveCpr = price > 0 && price >= active.Pivot;
        }
        catch
        {
            CurrentIntradayTc = 0;
            CurrentIntradayPivot = 0;
            CurrentIntradayBc = 0;
            AboveCpr = false;
        }
    }

    private static int GetCandleCount(string timeframe) => timeframe switch
    {
        "1m" => 450,
        "5m" => 108,
        "15m" => 75,
        "1H" => 60,
        "1D" => 90,
        "1W" => 104,
        _ => 60
    };

    private string? BuildLastCandleSummary()
    {
        if (ChartCandles.Count == 0)
            return null;

        var last = ChartCandles[^1];
        if (SelectedTimeframe is "1D" or "1W")
            return $"{last.Timestamp:dd MMM yyyy}  O {last.Open:N2}  H {last.High:N2}  L {last.Low:N2}  C {last.Close:N2}";

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
        if (SelectedTimeframe == "1m")
        {
            UpdateIntradayCprState();
            Notify(nameof(AboveCpr));
            Notify(nameof(CprPositionLabel));
            Notify(nameof(CprPositionClass));
        }
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
            UpdateIntradayCprState();
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
            "1D" or "1W" => Analysis.Trend1H,
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayVersion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPocOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPivotOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCamarillaOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowKeltnerOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowIntradayCprOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowSuperTrendOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowSuperTrend725Overlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowEma20Overlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowVwapOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Supports5mStudyToggles)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SupportsIntradayStOverlays)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CprSegments)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIntradayTc)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIntradayPivot)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIntradayBc)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AboveCpr)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CprPositionLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CprPositionClass)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Is1mCprChart)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SupportsIntradayCprOverlay)));
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AggregatePnL)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetPnLOpenCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTargetPnLLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetPnLStatusMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTargetPnLMonitoring)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetProfitAmount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetLossAmount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetPnLLastTrigger)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RiskControlsLastUpdated)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDashboardReady)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartupError)));
    }
}
