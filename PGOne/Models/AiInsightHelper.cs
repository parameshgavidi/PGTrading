namespace PGOne.Models;

public sealed record AiCheck(string Label, string State);

public static class AiInsightHelper
{
    public static IReadOnlyList<AiCheck> BuildChecks(MultiTimeframeAnalysis analysis)
    {
        var marketBiasPass = analysis.MarketBias != TrendDirection.Neutral;
        var tradeDirPass = analysis.TradeDirection != TrendDirection.Neutral;
        var entryPass = analysis.EntryTriggered;
        var footprintPass = analysis.FootprintConfirmed;

        var adxState = analysis.Adx >= 25 ? "pass"
            : analysis.Adx >= 20 ? "warn"
            : "fail";

        var rsiState = analysis.Rsi >= 55 || analysis.Rsi <= 45 ? "pass"
            : analysis.Rsi < 30 || analysis.Rsi > 70 ? "fail"
            : "warn";

        return
        [
            new($"1H ST {TrendUi.GetSuperTrendLabel(analysis.Trend1H)}", marketBiasPass ? "pass" : analysis.Trend1H == TrendDirection.Neutral ? "fail" : "warn"),
            new(analysis.AboveVwap ? "Above VWAP" : "Below VWAP", analysis.MarketBias != TrendDirection.Neutral ? "pass" : "warn"),
            new($"15M ST {TrendUi.GetSuperTrendLabel(analysis.Trend15M)}", tradeDirPass ? "pass" : analysis.Trend15M == TrendDirection.Neutral ? "warn" : "fail"),
            new(analysis.Adx >= 25 ? "ADX Rising" : $"ADX {analysis.Adx:N0}", adxState),
            new($"RSI {analysis.Rsi:N0}", rsiState),
            new($"Entry ST {TrendUi.GetSuperTrendLabel(analysis.Trend5MEntry)}", entryPass ? "pass" : "warn"),
            new(footprintPass ? "Footprint OK" : analysis.Footprint.Summary, footprintPass ? "pass" : "warn")
        ];
    }

    public static string GetSuggestedAction(Signal signal, MultiTimeframeAnalysis analysis)
    {
        if (analysis.WaitForReversal)
            return "WAIT";

        if (!analysis.FrameworkReady)
            return analysis.FrameworkStatus.StartsWith("Wait", StringComparison.OrdinalIgnoreCase)
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
