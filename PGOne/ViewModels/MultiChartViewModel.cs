using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class MultiChartViewModel : INotifyPropertyChanged
{
    private readonly IMarketDataService _marketData;
    private readonly ISignalService _signal;
    private readonly IIntradayCprService _intradayCpr;

    private decimal _priceReference;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChartPanelModel Panel5m { get; } = new()
    {
        Title = "5 min",
        CanvasId = "multiChart5m",
        SelectedTimeframe = "5m"
    };

    public ChartPanelModel Panel15m { get; } = new()
    {
        Title = "15 min",
        CanvasId = "multiChart15m",
        SelectedTimeframe = "15m"
    };

    public decimal LivePrice { get; private set; }
    public decimal LiveChange { get; private set; }
    public decimal LiveChangePercent { get; private set; }
    public MultiTimeframeAnalysis Analysis { get; private set; } = new();

    public string SelectedSymbol { get; private set; } = "NIFTY";
    public string SelectedInstrument { get; private set; } = "NSE:NIFTY 50";
    public string SelectedDisplayName => InstrumentMapper.ToDisplayName(SelectedSymbol);
    public bool IsMarketOpen => _marketData.IsMarketOpen;
    public string MarketStatus => IsMarketOpen ? "Market Open" : "Market Closed";
    public bool IsReady { get; private set; }
    public string? StartupError { get; private set; }

    public MultiChartViewModel(
        IMarketDataService marketData,
        ISignalService signal,
        IIntradayCprService intradayCpr)
    {
        _marketData = marketData;
        _signal = signal;
        _intradayCpr = intradayCpr;

        _marketData.PriceUpdated += OnPriceUpdated;
    }

    public async Task InitializeAsync()
    {
        if (IsReady)
            return;

        try
        {
            StartupError = null;
            await UpdatePriceAsync();
            Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
            await LoadAllChartsAsync();
            _marketData.StartStreaming(SelectedInstrument);
        }
        catch (Exception ex)
        {
            StartupError = ex.Message;
        }
        finally
        {
            IsReady = true;
            Notify();
        }
    }

    public async Task SelectInstrumentAsync(string symbol)
    {
        if (string.Equals(SelectedSymbol, symbol, StringComparison.OrdinalIgnoreCase))
            return;

        SelectedSymbol = symbol.ToUpper();
        SelectedInstrument = InstrumentMapper.ToZerodhaKey(SelectedSymbol);
        await UpdatePriceAsync();
        Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
        await LoadAllChartsAsync();
        _marketData.StartStreaming(SelectedInstrument);
        Notify();
    }

    public async Task ChangePanelTimeframeAsync(ChartPanelModel panel, string timeframe)
    {
        if (panel.SelectedTimeframe == timeframe)
            return;

        panel.SelectedTimeframe = timeframe;
        await ChartPanelLoader.LoadAsync(panel, SelectedInstrument, _marketData, _intradayCpr, LivePrice);
        panel.ChartTrend = ChartPanelLoader.GetTrendForTimeframe(Analysis, panel.SelectedTimeframe);
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowPoc(ChartPanelModel panel, bool show)
    {
        if (panel.ShowPocOverlay == show)
            return;

        panel.ShowPocOverlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowPivot(ChartPanelModel panel, bool show)
    {
        if (panel.ShowPivotOverlay == show)
            return;

        panel.ShowPivotOverlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowCamarilla(ChartPanelModel panel, bool show)
    {
        if (panel.ShowCamarillaOverlay == show)
            return;

        panel.ShowCamarillaOverlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowKeltner(ChartPanelModel panel, bool show)
    {
        if (panel.ShowKeltnerOverlay == show)
            return;

        panel.ShowKeltnerOverlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowIntradayCpr(ChartPanelModel panel, bool show)
    {
        if (panel.ShowIntradayCprOverlay == show)
            return;

        panel.ShowIntradayCprOverlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowSuperTrend(ChartPanelModel panel, bool show)
    {
        if (panel.ShowSuperTrendOverlay == show)
            return;

        panel.ShowSuperTrendOverlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowSuperTrend725(ChartPanelModel panel, bool show)
    {
        if (panel.ShowSuperTrend725Overlay == show)
            return;

        panel.ShowSuperTrend725Overlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowEma9(ChartPanelModel panel, bool show)
    {
        if (panel.ShowEma9Overlay == show)
            return;

        panel.ShowEma9Overlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowEma20(ChartPanelModel panel, bool show)
    {
        if (panel.ShowEma20Overlay == show)
            return;

        panel.ShowEma20Overlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowEma50(ChartPanelModel panel, bool show)
    {
        if (panel.ShowEma50Overlay == show)
            return;

        panel.ShowEma50Overlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowEma200(ChartPanelModel panel, bool show)
    {
        if (panel.ShowEma200Overlay == show)
            return;

        panel.ShowEma200Overlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public void SetPanelShowVwap(ChartPanelModel panel, bool show)
    {
        if (panel.ShowVwapOverlay == show)
            return;

        panel.ShowVwapOverlay = show;
        panel.OverlayVersion++;
        NotifyPanels();
    }

    public async Task RefreshAsync()
    {
        await UpdatePriceAsync();
        Analysis = await _signal.AnalyzeAsync(SelectedSymbol);
        await LoadAllChartsAsync();
        Notify();
    }

    private async Task LoadAllChartsAsync()
    {
        await ChartPanelLoader.LoadAsync(Panel5m, SelectedInstrument, _marketData, _intradayCpr, LivePrice);
        Panel5m.ChartTrend = ChartPanelLoader.GetTrendForTimeframe(Analysis, Panel5m.SelectedTimeframe);

        await ChartPanelLoader.LoadAsync(Panel15m, SelectedInstrument, _marketData, _intradayCpr, LivePrice);
        Panel15m.ChartTrend = ChartPanelLoader.GetTrendForTimeframe(Analysis, Panel15m.SelectedTimeframe);

        NotifyPanels();
    }

    private async Task UpdatePriceAsync()
    {
        var quote = await _marketData.GetQuoteAsync(SelectedInstrument);
        if (quote is { LastPrice: > 0 })
        {
            LivePrice = quote.LastPrice;
            _priceReference = quote.PreviousClose > 0 ? quote.PreviousClose : quote.Open;
            LiveChange = quote.Change;
            LiveChangePercent = quote.ChangePercent;
            ChartPanelLoader.UpdateIntradayCprState(Panel5m, _intradayCpr, LivePrice);
            ChartPanelLoader.UpdateIntradayCprState(Panel15m, _intradayCpr, LivePrice);
            return;
        }

        _priceReference = 0;
    }

    private void OnPriceUpdated(string instrument, decimal price)
    {
        if (instrument != SelectedInstrument)
            return;

        LivePrice = price;
        if (_priceReference > 0)
        {
            LiveChange = price - _priceReference;
            LiveChangePercent = Math.Round(LiveChange / _priceReference * 100, 2);
        }

        ChartPanelLoader.UpdateIntradayCprState(Panel5m, _intradayCpr, LivePrice);
        ChartPanelLoader.UpdateIntradayCprState(Panel15m, _intradayCpr, LivePrice);

        Notify(nameof(LivePrice));
        Notify(nameof(LiveChange));
        Notify(nameof(LiveChangePercent));
        NotifyPanels();
    }

    private void NotifyPanels()
    {
        Notify(nameof(Panel5m));
        Notify(nameof(Panel15m));
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarketOpen)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarketStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReady)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartupError)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Panel5m)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Panel15m)));
    }
}
