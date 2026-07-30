namespace PGOne.Models;

public sealed record AiCheck(string Label, string State);

public static class AiInsightHelper
{
    public static AiInsightRecommendation BuildRecommendation(Signal signal, MultiTimeframeAnalysis analysis)
    {
        var primaryBias = analysis.TradeDirection != TrendDirection.Neutral
            ? analysis.TradeDirection
            : analysis.MarketBias;

        var footprintConflict = primaryBias != TrendDirection.Neutral
            && FootprintDisplayHelper.FootprintOpposesBias(analysis.Footprint, primaryBias);

        var probability = analysis.OverallScore;
        var strength = analysis.Strength;

        if (analysis.WaitForReversal)
        {
            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 42),
                Strength = "Reversal Risk",
                ActionHeadline = "WAIT",
                ActionDetail = FormatDetail(
                    analysis.ReversalReason ?? $"5m RSI {analysis.Rsi5M:N0} — avoid new entries until RSI recovers."),
                ActionKind = "wait"
            };
        }

        if (analysis.IsRotationRegime)
        {
            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 48),
                Strength = "Rotation",
                ActionHeadline = "RANGE ONLY",
                ActionDetail = "ADX choppy inside Value Area — fade extremes toward VWAP / Keltner mid, not breakout chase.",
                ActionKind = "neutral"
            };
        }

        if (analysis.IsRangebound)
        {
            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 54),
                Strength = "Range-bound",
                ActionHeadline = "RANGE ONLY",
                ActionDetail = $"1H RSI(28) {analysis.RsiTrend:N0} between 45–55 — prefer straddle / IC fade, not directional chase.",
                ActionKind = "neutral"
            };
        }

        if (footprintConflict)
        {
            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 68),
                Strength = "Flow Conflict",
                ActionHeadline = "WAIT",
                ActionDetail = $"{SummarizeFootprintFlow(analysis.Footprint)} opposes {TrendUi.GetBiasLabel(primaryBias)} setup — wait for aligned futures flow on 5m.",
                ActionKind = "wait"
            };
        }

        if (!analysis.FrameworkReady)
        {
            return new AiInsightRecommendation
            {
                Probability = probability,
                Strength = strength,
                ActionHeadline = "WAIT",
                ActionDetail = FormatBlockingDetail(analysis),
                ActionKind = "wait"
            };
        }

        return BuildReadyRecommendation(signal, analysis, probability, strength);
    }

    public static string GetSuggestedAction(Signal signal, MultiTimeframeAnalysis analysis)
        => BuildRecommendation(signal, analysis).ActionHeadline;

    public static IReadOnlyList<AiCheck> BuildChecks(MultiTimeframeAnalysis analysis)
    {
        var tpoPass = analysis.TpoConfirmed;
        var entryPass = analysis.EntryTriggered;
        var footprintPass = analysis.FootprintConfirmed;

        var primaryBias = analysis.TradeDirection != TrendDirection.Neutral
            ? analysis.TradeDirection
            : analysis.MarketBias;

        var adxState = analysis.Adx >= 25 ? "pass"
            : analysis.Adx >= 18 ? "warn"
            : "fail";

        var rsiState = analysis.RsiTrend > 55 || analysis.RsiTrend < 45 ? "pass"
            : analysis.RsiTrend < 30 || analysis.RsiTrend > 70 ? "fail"
            : "warn";

        var st1HState = analysis.Trend1H == TrendDirection.Neutral ? "fail" : "pass";
        var vwapState = GetVwapCheckState(analysis);
        var st15MState = Get15MStCheckState(analysis);

        var footprintState = footprintPass ? "pass"
            : FootprintDisplayHelper.FootprintOpposesBias(analysis.Footprint, primaryBias) ? "fail"
            : "warn";

        return
        [
            new($"{TrendUi.GetIcon(analysis.Trend1H)} 1H ST {TrendUi.GetSuperTrendLabel(analysis.Trend1H)}", st1HState),
            new(analysis.AboveVwap ? "Above VWAP" : "Below VWAP", vwapState),
            new($"{TrendUi.GetIcon(analysis.Trend15M)} 15M ST {TrendUi.GetSuperTrendLabel(analysis.Trend15M)}", st15MState),
            new(analysis.Adx >= 25 ? "ADX Strong" : analysis.Adx >= 18 ? $"ADX Moderate {analysis.Adx:N0}" : $"ADX Choppy {analysis.Adx:N0}", adxState),
            new($"RSI(28) {analysis.RsiTrend:N0}", rsiState),
            new(tpoPass ? "POC Confirmed" : analysis.Tpo.Summary, tpoPass ? "pass" : analysis.IsRotationRegime ? "fail" : "warn"),
            new($"{TrendUi.GetIcon(analysis.Trend5MEntry)} Entry ST {TrendUi.GetSuperTrendLabel(analysis.Trend5MEntry)}", entryPass ? "pass" : "warn"),
            new(FootprintDisplayHelper.GetDisplayLabel(analysis.Footprint, footprintPass), footprintState)
        ];
    }

    private static string GetVwapCheckState(MultiTimeframeAnalysis analysis)
    {
        if (analysis.MarketBias != TrendDirection.Neutral)
            return "pass";

        if (analysis.Trend1H == TrendDirection.Buy && !analysis.AboveVwap)
            return "fail";

        if (analysis.Trend1H == TrendDirection.Sell && analysis.AboveVwap)
            return "fail";

        return "warn";
    }

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

    private static AiInsightRecommendation BuildReadyRecommendation(
        Signal signal,
        MultiTimeframeAnalysis analysis,
        int probability,
        string strength)
    {
        if (signal.Trend == TrendDirection.Buy)
        {
            var headline = analysis.AboveVwap ? "BUY ON DIP" : "BUY ON PULLBACK";
            return new AiInsightRecommendation
            {
                Probability = probability,
                Strength = strength,
                ActionHeadline = headline,
                ActionDetail = BuildReadyDetail(analysis, signal, "Long MIS / CE debit spread", "5m ST (7,2.5) reversal"),
                ActionKind = "buy"
            };
        }

        if (signal.Trend == TrendDirection.Sell)
        {
            var headline = !analysis.AboveVwap ? "SELL ON RALLY" : "SELL ON BOUNCE";
            return new AiInsightRecommendation
            {
                Probability = probability,
                Strength = strength,
                ActionHeadline = headline,
                ActionDetail = BuildReadyDetail(analysis, signal, "Short MIS / sell ATM CE", "5m ST (7,2.5) reversal"),
                ActionKind = "sell"
            };
        }

        return new AiInsightRecommendation
        {
            Probability = probability,
            Strength = strength,
            ActionHeadline = "HOLD",
            ActionDetail = "Framework aligned but no directional bias — stand aside.",
            ActionKind = "neutral"
        };
    }

    private static string BuildReadyDetail(
        MultiTimeframeAnalysis analysis,
        Signal signal,
        string entryStyle,
        string stopRule)
    {
        var target = !string.IsNullOrWhiteSpace(signal.Target) && signal.Target != "-"
            ? signal.Target
            : analysis.VolumeProfile.TargetSummary(signal.Trend);

        return $"All 5 steps aligned · {entryStyle} · Target {target} · Stop {stopRule}";
    }

    private static string FormatBlockingDetail(MultiTimeframeAnalysis analysis)
    {
        if (analysis.MarketBias == TrendDirection.Neutral && analysis.Trend1H != TrendDirection.Neutral)
        {
            if (analysis.Trend1H == TrendDirection.Buy && !analysis.AboveVwap)
                return "Step 1 incomplete — 1H SuperTrend is bullish but price is below session VWAP. Wait for reclaim above VWAP.";

            if (analysis.Trend1H == TrendDirection.Sell && analysis.AboveVwap)
                return "Step 1 incomplete — 1H SuperTrend is bearish but price is above session VWAP. Wait for rejection below VWAP.";
        }

        if (!analysis.TpoConfirmed && analysis.MarketBias != TrendDirection.Neutral)
            return $"POC not confirmed — {analysis.Tpo.Summary}. {NextStepHint(analysis)}";

        if (!analysis.EntryTriggered && analysis.TradeDirection != TrendDirection.Neutral)
            return $"Entry ST (7,2.5) not triggered — 5m shows {TrendUi.GetSuperTrendLabel(analysis.Trend5MEntry)} vs need {TrendUi.GetBiasLabel(analysis.TradeDirection)}.";

        if (analysis.TradeDirection == TrendDirection.Neutral && analysis.MarketBias != TrendDirection.Neutral)
            return $"Step 2 incomplete — ADX {analysis.Adx:N0}, RSI(28) {analysis.RsiTrend:N0}, or POC blocking {TrendUi.GetBiasLabel(analysis.MarketBias)} direction.";

        return FormatDetail(analysis.FrameworkStatus);
    }

    private static string NextStepHint(MultiTimeframeAnalysis analysis)
    {
        if (!analysis.EntryTriggered)
            return "Watch 5m entry SuperTrend (7,2.5).";

        if (!analysis.FootprintConfirmed)
            return "Need confirming footprint delta + imbalances.";

        return "Review checklist above.";
    }

    private static string SummarizeFootprintFlow(FootprintAnalysis fp)
    {
        if (fp.VolumeSource == "futures" && !string.IsNullOrEmpty(fp.FuturesSymbol))
            return $"{FootprintDisplayHelper.GetFlowBiasLabel(fp)} futures flow ({FootprintDisplayHelper.GetShortDeltaLabel(fp)}, {fp.FuturesSymbol})";

        return FootprintDisplayHelper.GetInsightLabel(fp);
    }

    private static string FormatDetail(string text) =>
        string.IsNullOrWhiteSpace(text) ? "Conditions not met for entry." : text.Trim();
}
