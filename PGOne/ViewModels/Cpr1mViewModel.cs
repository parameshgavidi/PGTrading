using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class Cpr1mViewModel : INotifyPropertyChanged
{
    private readonly IMarketDataService _marketData;
    private readonly IIntradayCprService _intradayCpr;
    private System.Timers.Timer? _refreshTimer;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Instrument { get; private set; } = "NSE:NIFTY 50";
    public string DisplayName { get; private set; } = "NIFTY";
    public List<Candle> ChartCandles { get; private set; } = new();
    public IReadOnlyList<IntradayCprSegment> CprSegments { get; private set; } = Array.Empty<IntradayCprSegment>();
    public int ChartVersion { get; private set; }
    public int OverlayVersion { get; private set; }
    public bool ShowKeltnerOverlay { get; private set; } = true;

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

    public Cpr1mViewModel(IMarketDataService marketData, IIntradayCprService intradayCpr)
    {
        _marketData = marketData;
        _intradayCpr = intradayCpr;
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
            var sessionDate = MarketHours.GetIstNow().Date;
            if (!MarketHours.IsOpen() && MarketHours.GetIstNow().TimeOfDay < MarketHours.OpenTime)
                sessionDate = sessionDate.AddDays(sessionDate.DayOfWeek == DayOfWeek.Monday ? -3 : -1);

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

            var quote = await _marketData.GetQuoteAsync(Instrument);
            LastPrice = quote?.LastPrice ?? ChartCandles.LastOrDefault()?.Close ?? 0m;
            DayChangePercent = quote?.ChangePercent ?? 0m;

            var activeTime = ChartCandles.Count > 0
                ? ChartCandles[^1].Timestamp
                : MarketHours.GetIstNow();

            var active = _intradayCpr.GetActiveSegment(CprSegments, activeTime);
            if (active is not null)
            {
                CurrentPivot = active.Pivot;
                CurrentTc = active.Tc;
                CurrentBc = active.Bc;
                AboveCpr = LastPrice > 0 && LastPrice >= active.Pivot;
            }
        }
        catch (Exception ex)
        {
            ChartDataMessage = $"1m CPR load failed: {ex.Message}";
        }

        Notify();
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChartVersion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayVersion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowKeltnerOverlay)));
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
