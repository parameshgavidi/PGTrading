namespace PGOne.Models;

public sealed record AiCheck(string Label, string State);

public static class AiInsightHelper
{
    public static IReadOnlyList<AiCheck> BuildChecks(MultiTimeframeAnalysis analysis)
    {
        var marketBiasPass = analysis.MarketBias != TrendDirection.Neutral;
        var tradeDirPass = analysis.TradeDirection != TrendDirection.Neutral;
        var tpoPass = analysis.TpoConfirmed;
        var entryPass = analysis.EntryTriggered;
        var footprintPass = analysis.FootprintConfirmed;

        var adxState = analysis.Adx >= 25 ? "pass"
            : analysis.Adx >= 18 ? "warn"
            : "fail";

        var rsiState = analysis.RsiTrend > 55 || analysis.RsiTrend < 45 ? "pass"
            : analysis.RsiTrend < 30 || analysis.RsiTrend > 70 ? "fail"
            : "warn";

        var st1HState = marketBiasPass ? "pass"
            : analysis.Trend1H == TrendDirection.Neutral ? "fail"
            : "warn";

        var st15MState = Get15MStCheckState(analysis);

        return
        [
            new($"{TrendUi.GetIcon(analysis.Trend1H)} 1H ST {TrendUi.GetSuperTrendLabel(analysis.Trend1H)}", st1HState),
            new(analysis.AboveVwap ? "Above VWAP" : "Below VWAP", analysis.MarketBias != TrendDirection.Neutral ? "pass" : "warn"),
            new($"{TrendUi.GetIcon(analysis.Trend15M)} 15M ST {TrendUi.GetSuperTrendLabel(analysis.Trend15M)}", st15MState),
            new(analysis.Adx >= 25 ? "ADX Strong" : analysis.Adx >= 18 ? $"ADX Moderate {analysis.Adx:N0}" : $"ADX Choppy {analysis.Adx:N0}", adxState),
            new($"RSI(28) {analysis.RsiTrend:N0}", rsiState),
            new(tpoPass ? "POC Confirmed" : analysis.Tpo.Summary, tpoPass ? "pass" : analysis.IsRotationRegime ? "fail" : "warn"),
            new($"{TrendUi.GetIcon(analysis.Trend5MEntry)} Entry ST {TrendUi.GetSuperTrendLabel(analysis.Trend5MEntry)}", entryPass ? "pass" : "warn"),
            new(FootprintDisplayHelper.GetDisplayLabel(analysis.Footprint, footprintPass),
                footprintPass ? "pass" : "warn")
        ];
    }

    /// <summary>
    /// 15M ST check reflects alignment with market bias (Step 2 partial), not full trade direction.
    /// </summary>
    public static string Get15MStCheckState(MultiTimeframeAnalysis analysis)
    {
        if (analysis.MarketBias == TrendDirection.Neutral)
            return analysis.Trend15M == TrendDirection.Neutral ? "warn" : "warn";

        if (analysis.Trend15M == analysis.MarketBias)
            return "pass";

        if (analysis.Trend15M == TrendDirection.Neutral)
            return "warn";

        return "fail";
    }

    public static string GetSuggestedAction(Signal signal, MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return "WAIT";

        if (analysis.IsRotationRegime)
            return "RANGE TRADE";

        if (!analysis.FrameworkReady)
            return analysis.FrameworkStatus.StartsWith("Wait", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.StartsWith("Rotation", StringComparison.OrdinalIgnoreCase)
                ? "WAIT"
                : analysis.FrameworkStatus;

        return signal.Trend switch
        {
            TrendDirection.Buy when analysis.AboveVwap => "BUY ON DIP",
            TrendDirection.Buy => "BUY ON PULLBACK",
            TrendDirection.Sell when !analysis.AboveVwap => "SELL ON RALLY",
            TrendDirection.Sell => "SELL ON BOUNCE",
            _ when analysis.IsRangebound => "RANGE TRADE",
            _ => "HOLD / NEUTRAL"
        };
    }
}
