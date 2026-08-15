namespace PgAiTrading.Models;

/// <summary>State for one chart panel on Multi Chart (timeframe, overlays, candles).</summary>
public class ChartPanelModel
{
    public string Title { get; set; } = "Chart";
    public string CanvasId { get; set; } = "chartPanel";
    public string SelectedTimeframe { get; set; } = "5m";
    public List<Candle> ChartCandles { get; set; } = new();
    public int ChartVersion { get; set; }
    public int OverlayVersion { get; set; }
    public bool ShowPocOverlay { get; set; } = true;
    public bool ShowPivotOverlay { get; set; } = false;
    public bool ShowCamarillaOverlay { get; set; } = false;
    public bool ShowKeltnerOverlay { get; set; } = false;
    public bool ShowIntradayCprOverlay { get; set; } = false;
    public bool ShowSuperTrendOverlay { get; set; } = false;
    public bool ShowSuperTrend725Overlay { get; set; } = false;
    public bool ShowEma9Overlay { get; set; } = false;
    public bool ShowEma20Overlay { get; set; } = false;
    public bool ShowEma50Overlay { get; set; } = false;
    public bool ShowEma200Overlay { get; set; } = false;
    public bool ShowVwapOverlay { get; set; } = false;
    public bool ShowPatternsOverlay { get; set; } = false;
    public IReadOnlyList<IntradayCprSegment> CprSegments { get; set; } = Array.Empty<IntradayCprSegment>();
    public bool AboveCpr { get; set; }
    public string? ChartDataMessage { get; set; }
    public string? LastCandleSummary { get; set; }
    public bool IsChartFromZerodha { get; set; }
    public TrendDirection ChartTrend { get; set; } = TrendDirection.Neutral;

    public bool SupportsIntradayCprOverlay => true;
    /// <summary>SuperTrend overlays stay visible on every chart timeframe.</summary>
    public bool SupportsIntradayStOverlays => true;
    public bool SupportsSt725Overlays => true;
    /// <summary>EMA / VWAP study toggles stay visible on every chart timeframe.</summary>
    public bool Supports5mStudyToggles => true;
    public string CprPositionLabel => AboveCpr ? "Above CPR" : "Below CPR";
    public string CprPositionClass => AboveCpr ? "above-cpr" : "below-cpr";
}
