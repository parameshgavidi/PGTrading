namespace PGOne.Models;

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
    public bool ShowPivotOverlay { get; set; } = true;
    public bool ShowCamarillaOverlay { get; set; } = true;
    public bool ShowKeltnerOverlay { get; set; } = true;
    public bool ShowIntradayCprOverlay { get; set; } = true;
    public IReadOnlyList<IntradayCprSegment> CprSegments { get; set; } = Array.Empty<IntradayCprSegment>();
    public bool AboveCpr { get; set; }
    public string? ChartDataMessage { get; set; }
    public string? LastCandleSummary { get; set; }
    public bool IsChartFromZerodha { get; set; }
    public TrendDirection ChartTrend { get; set; } = TrendDirection.Neutral;

    public bool SupportsIntradayCprOverlay => SelectedTimeframe is "1m" or "15m";
    public string CprPositionLabel => AboveCpr ? "Above CPR" : "Below CPR";
    public string CprPositionClass => AboveCpr ? "above-cpr" : "below-cpr";
}
