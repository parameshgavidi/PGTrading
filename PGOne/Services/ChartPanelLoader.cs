using PGOne.Models;

namespace PGOne.Services;

public static class ChartPanelLoader
{
    public static async Task LoadAsync(
        ChartPanelModel panel,
        string instrument,
        IMarketDataService marketData,
        IIntradayCprService intradayCpr,
        decimal livePrice = 0m)
    {
        try
        {
            var count = GetCandleCount(panel.SelectedTimeframe);
            var result = await marketData.GetCandlesResultAsync(instrument, panel.SelectedTimeframe, count);

            if (panel.SelectedTimeframe == "1m")
            {
                var sessionDate = GetChartSessionDate();
                var candles15m = await marketData.GetCandlesResultAsync(instrument, "15m", 80);
                panel.CprSegments = intradayCpr.BuildSegments(candles15m.Candles, sessionDate);

                var sessionCandles = result.Candles
                    .Where(c => c.Timestamp.Date == sessionDate)
                    .ToList();
                panel.ChartCandles = sessionCandles.Count > 0 ? sessionCandles : result.Candles;
            }
            else if (panel.SelectedTimeframe == "15m")
            {
                var sessionDate = GetChartSessionDate();
                panel.CprSegments = intradayCpr.BuildSegments(result.Candles, sessionDate);
                panel.ChartCandles = result.Candles;
            }
            else
            {
                panel.CprSegments = Array.Empty<IntradayCprSegment>();
                panel.ChartCandles = result.Candles;
            }

            marketData.ApplyChartIndicators(panel.ChartCandles, panel.SelectedTimeframe);

            panel.ChartVersion++;
            panel.IsChartFromZerodha = result.IsFromZerodha;
            panel.ChartDataMessage = result.IsFromZerodha
                ? panel.SelectedTimeframe switch
                {
                    "1m" => $"Zerodha 1m candles ({panel.ChartCandles.Count} bars) · 1m CPR bands (15m pivot)",
                    "15m" => $"Zerodha 15m candles ({panel.ChartCandles.Count} bars) · 1m CPR bands",
                    _ => $"Zerodha {panel.SelectedTimeframe} candles ({panel.ChartCandles.Count} bars)"
                }
                : result.Error ?? "Demo candle data";
            panel.LastCandleSummary = BuildLastCandleSummary(panel.ChartCandles);
            UpdateIntradayCprState(panel, intradayCpr, livePrice);
        }
        catch (Exception ex)
        {
            panel.ChartDataMessage = $"Chart load failed: {ex.Message}";
            panel.CprSegments = Array.Empty<IntradayCprSegment>();
            panel.AboveCpr = false;
        }
    }

    public static void UpdateIntradayCprState(ChartPanelModel panel, IIntradayCprService intradayCpr, decimal livePrice)
    {
        try
        {
            if (!panel.SupportsIntradayCprOverlay || panel.CprSegments.Count == 0)
            {
                panel.AboveCpr = false;
                return;
            }

            var activeTime = panel.ChartCandles.Count > 0
                ? panel.ChartCandles[^1].Timestamp
                : MarketHours.GetIstNow();

            var active = intradayCpr.GetActiveSegment(panel.CprSegments, activeTime);
            if (active is null)
                return;

            var lastClose = panel.ChartCandles.Count > 0 ? panel.ChartCandles[^1].Close : 0m;
            var price = livePrice > 0 ? livePrice : lastClose;
            panel.AboveCpr = price > 0 && price >= active.Pivot;
        }
        catch
        {
            panel.AboveCpr = false;
        }
    }

    public static TrendDirection GetTrendForTimeframe(MultiTimeframeAnalysis analysis, string timeframe) =>
        timeframe switch
        {
            "1H" => analysis.Trend1H,
            "15m" => analysis.Trend15M,
            "1D" => analysis.Trend1H,
            _ => analysis.Trend5M
        };

    private static DateTime GetChartSessionDate()
    {
        var now = MarketHours.GetIstNow();
        if (!MarketHours.IsOpen(now) && now.TimeOfDay < MarketHours.OpenTime)
            return now.Date.AddDays(now.DayOfWeek == DayOfWeek.Monday ? -3 : -1);

        return now.Date;
    }

    private static int GetCandleCount(string timeframe) => timeframe switch
    {
        "1m" => 450,
        "5m" => 108,
        "15m" => 75,
        "1H" => 60,
        "1D" => 90,
        _ => 60
    };

    private static string? BuildLastCandleSummary(List<Candle> candles)
    {
        if (candles.Count == 0)
            return null;

        var last = candles[^1];
        return $"{last.Timestamp:dd MMM HH:mm}  O {last.Open:N2}  H {last.High:N2}  L {last.Low:N2}  C {last.Close:N2}";
    }
}
