using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class Cpr1mViewModel : INotifyPropertyChanged
{
    private readonly IMarketDataService _marketData;
    private readonly IIntradayCprService _intradayCpr;
    private readonly ISignalService _signal;
    private System.Timers.Timer? _refreshTimer;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Instrument { get; private set; } = "NSE:NIFTY 50";
    public string DisplayName { get; private set; } = "NIFTY";
    public List<Candle> ChartCandles { get; private set; } = new();
    public IReadOnlyList<IntradayCprSegment> CprSegments { get; private set; } = Array.Empty<IntradayCprSegment>();
    public MultiTimeframeAnalysis Analysis { get; private set; } = new();
    public int ChartVersion { get; private set; }
    public int OverlayVersion { get; private set; }

    public bool ShowPocOverlay { get; private set; } = true;
    public bool ShowPivotOverlay { get; private set; } = false;
    public bool ShowCamarillaOverlay { get; private set; } = false;
    public bool ShowKeltnerOverlay { get; private set; } = false;
    public bool ShowIntradayCprOverlay { get; private set; } = true;
    public bool ShowSuperTrendOverlay { get; private set; } = false;
    public bool ShowEma20Overlay { get; private set; } = false;
    public bool ShowVwapOverlay { get; private set; } = false;

    public bool SupportsIntradayCprOverlay => true;
    public bool Supports5mStudyToggles => false;

    public decimal LastPrice { get; private set; }
    public decimal DayChangePercent { get; private set; }
    public decimal CurrentPivot { get; private set; }
    public decimal CurrentTc { get; private set; }
    public decimal CurrentBc { get; private set; }
    public bool AboveCpr { get; private set; }
    public string CprPositionLabel => AboveCpr ? "Above CPR" : "Below CPR";
    public string CprPositionClass => AboveCpr ? "above-cpr" : "below-cpr";

    public bool IsChartFromZerodha { get; private set; }
    public string? ChartDataMessage { get; private set; }
    public bool IsMarketOpen => _marketData.IsMarketOpen;
    public string MarketStatus => IsMarketOpen ? "Market Open" : "Market Closed";

    public Cpr1mViewModel(
        IMarketDataService marketData,
        IIntradayCprService intradayCpr,
        ISignalService signal)
    {
        _marketData = marketData;
        _intradayCpr = intradayCpr;
        _signal = signal;
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
        StartAutoRefresh();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var sessionDate = GetChartSessionDate();
            var candles1m = await _marketData.GetCandlesResultAsync(Instrument, "1m", 450);
            var candles15m = await _marketData.GetCandlesResultAsync(Instrument, "15m", 80);

            ChartCandles = candles1m.Candles
                .Where(c => c.Timestamp.Date == sessionDate)
                .ToList();
            if (ChartCandles.Count == 0)
                ChartCandles = candles1m.Candles;
            IsChartFromZerodha = candles1m.IsFromZerodha;
            ChartDataMessage = candles1m.IsFromZerodha
                ? $"Zerodha 1m candles ({ChartCandles.Count} bars) · CPR updates every 15m"
                : candles1m.Error ?? "Demo 1m candle data";

            CprSegments = _intradayCpr.BuildSegments(candles15m.Candles, sessionDate);
            ChartVersion++;

            Analysis = await _signal.AnalyzeAsync(DisplayName);

            var quote = await _marketData.GetQuoteAsync(Instrument);
            LastPrice = quote?.LastPrice ?? ChartCandles.LastOrDefault()?.Close ?? 0m;
            DayChangePercent = quote?.ChangePercent ?? 0m;

            UpdateIntradayCprState();
        }
        catch (Exception ex)
        {
            ChartDataMessage = $"1m CPR load failed: {ex.Message}";
        }

        Notify();
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

    private static DateTime GetChartSessionDate()
    {
        var now = MarketHours.GetIstNow();
        if (!MarketHours.IsOpen(now) && now.TimeOfDay < MarketHours.OpenTime)
            return now.Date.AddDays(now.DayOfWeek == DayOfWeek.Monday ? -3 : -1);

        return now.Date;
    }

    private void UpdateIntradayCprState()
    {
        if (CprSegments.Count == 0)
        {
            CurrentTc = 0;
            CurrentPivot = 0;
            CurrentBc = 0;
            AboveCpr = false;
            return;
        }

        var activeTime = ChartCandles.Count > 0
            ? ChartCandles[^1].Timestamp
            : MarketHours.GetIstNow();

        var active = _intradayCpr.GetActiveSegment(CprSegments, activeTime);
        if (active is null)
            return;

        CurrentPivot = active.Pivot;
        CurrentTc = active.Tc;
        CurrentBc = active.Bc;
        AboveCpr = LastPrice > 0 && LastPrice >= active.Pivot;
    }

    private void StartAutoRefresh()
    {
        if (_refreshTimer is not null)
            return;

        _refreshTimer = new System.Timers.Timer(60_000);
        _refreshTimer.Elapsed += async (_, _) =>
        {
            if (!MarketHours.IsOpen())
                return;

            await RefreshAsync();
        };
        _refreshTimer.Start();
    }

    private void Notify([CallerMemberName] string? property = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        if (property != null)
            return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartCandles)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CprSegments)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Analysis)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartVersion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayVersion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPocOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPivotOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCamarillaOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowKeltnerOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowIntradayCprOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowSuperTrendOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowEma20Overlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowVwapOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SupportsIntradayCprOverlay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Supports5mStudyToggles)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastPrice)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DayChangePercent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPivot)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTc)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentBc)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AboveCpr)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CprPositionLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CprPositionClass)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChartFromZerodha)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartDataMessage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarketOpen)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarketStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }
}
