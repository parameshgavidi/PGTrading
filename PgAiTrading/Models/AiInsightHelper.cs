namespace PgAiTrading.Models;

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
                Strength = "Reversal Wait",
                ActionHeadline = "WAIT",
                ActionDetail = FormatDetail(
                    analysis.ReversalReason
                    ?? $"5m RSI(28) {analysis.Rsi5M:N0} < 30 + bullish 5m pattern — WAIT, no new entry."),
                ActionKind = "wait"
            };
        }

        if (analysis.ExpectReversal)
        {
            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 58),
                Strength = "Expect Reversal",
                ActionHeadline = "EXPECT REVERSAL",
                ActionDetail = FormatDetail(
                    analysis.ReversalReason
                    ?? $"5m RSI(28) {analysis.Rsi5M:N0} < 30 — expect reversal. If any bullish 5m pattern prints → WAIT."),
                ActionKind = "neutral"
            };
        }

        if (analysis.Regime == MarketRegime.StrongChop && analysis.IsRangebound)
        {
            if (analysis.LiquiditySweep.IsConfirmedSetup && analysis.FrameworkReady)
                return BuildReadyRecommendation(signal, analysis, probability, strength);

            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 48),
                Strength = "Strong Chop",
                ActionHeadline = analysis.LiquiditySweep.Detected ? "WAIT RECLAIM/BOS" : "SWEEP WATCH",
                ActionDetail = analysis.LiquiditySweep.Detected
                    ? FormatDetail(analysis.LiquiditySweep.Summary)
                    : "RSI mid + ADX < 18 — wait liquidity sweep at VA / PDH / PDL, then reclaim + 5M BOS + footprint.",
                ActionKind = "wait"
            };
        }

        if (analysis.Regime == MarketRegime.SoftNeutral && analysis.IsRangebound)
        {
            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 52),
                Strength = "Soft Neutral",
                ActionHeadline = "WAIT",
                ActionDetail = "RSI(28) 45–55 with ADX not developing — stand aside until 1H structure clarifies.",
                ActionKind = "wait"
            };
        }

        if (analysis.Regime == MarketRegime.DevelopingTrend && !analysis.FrameworkReady)
            {
                return new AiInsightRecommendation
                {
                    Probability = Math.Min(probability, 58),
                    Strength = "Developing",
                    ActionHeadline = "WAIT STRUCTURE",
                    // Prefer evaluator status (conflict / VWAP / 15M / BOS) over a generic developing blurb.
                    ActionDetail = FormatDetail(GetDevelopingWaitDetail(analysis)),
                    ActionKind = "wait"
                };
            }

        if (analysis.IsRotationRegime && analysis.Regime != MarketRegime.StrongChop)
        {
            return new AiInsightRecommendation
            {
                Probability = Math.Min(probability, 48),
                Strength = "Rotation",
                ActionHeadline = "SWEEP WATCH",
                ActionDetail = "ADX choppy inside Value Area — prefer liquidity sweeps at extremes, not breakout chase.",
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

    /// <summary>All 5 framework steps aligned with a directional BUY/SELL signal.</summary>
    public static bool IsPerfectEntry(MultiTimeframeAnalysis analysis, Signal signal)
    {
        if (!analysis.FrameworkReady)
            return false;

        var recommendation = BuildRecommendation(signal, analysis);
        return recommendation.ActionKind is "buy" or "sell";
    }

    public static string GetSuggestedAction(Signal signal, MultiTimeframeAnalysis analysis)
        => BuildRecommendation(signal, analysis).ActionHeadline;

    public static IReadOnlyList<AiCheck> BuildChecks(MultiTimeframeAnalysis analysis)
    {
        var tpoPass = analysis.TpoConfirmed;
        var entryPass = analysis.EntryTriggered;
        var footprintPass = analysis.FootprintConfirmed;

        // Trade → market bias → 1H structure. Structure is the reference when ST/VWAP blocked bias.
        var primaryBias = ResolvePrimaryBias(analysis);
        var structureBias = analysis.Structure.MajorDirection;

        var adxState = analysis.Adx >= 25 ? "pass"
            : analysis.Adx >= 18 ? "warn"
            : "fail";

        // Mid RSI is expected in Developing / StrongChop — do not warn those regimes.
        var rsiState = analysis.RsiTrend > 55 || analysis.RsiTrend < 45 ? "pass"
            : analysis.Regime is MarketRegime.DevelopingTrend or MarketRegime.StrongChop ? "pass"
            : analysis.RsiTrend < 30 || analysis.RsiTrend > 70 ? "fail"
            : "warn";

        var st1HState = Get1HStCheckState(analysis, structureBias);
        var vwapState = GetVwapCheckState(analysis, primaryBias, structureBias);
        var st15MState = Get15MStCheckState(analysis);

        var structureState = analysis.Structure.Structure1H.Bias switch
        {
            StructureBias.Bullish or StructureBias.Bearish => "pass",
            StructureBias.Mixed => "warn",
            _ => "fail"
        };

        var structure15State = Get15MStructureCheckState(analysis, primaryBias, structureBias);

        var pocState = GetPocCheckState(analysis, primaryBias, structureBias, tpoPass);
        var pocLabel = tpoPass ? "POC Confirmed" : analysis.Tpo.Summary;

        // Sweep is required only in strong chop; elsewhere optional unless already confirmed.
        var sweepState = analysis.LiquiditySweep.IsConfirmedSetup ? "pass"
            : analysis.LiquiditySweep.Detected && analysis.LiquiditySweep.Reclaimed ? "warn"
            : analysis.Regime == MarketRegime.StrongChop ? "fail"
            : "pass";

        var sweepLabel = analysis.LiquiditySweep.Detected
            ? analysis.LiquiditySweep.Summary
            : analysis.Regime == MarketRegime.StrongChop
                ? "No liquidity sweep"
                : "No liquidity sweep (optional)";

        var footprintState = footprintPass ? "pass"
            : FootprintDisplayHelper.FootprintOpposesBias(analysis.Footprint, primaryBias) ? "fail"
            : "warn";

        _ = st15MState; // retained for callers that still use Get15MStCheckState elsewhere

        var checks = new List<AiCheck>
        {
            new($"1H structure {analysis.Structure.Structure1H.Summary}", structureState),
            new($"{TrendUi.GetIcon(analysis.Trend1H)} 1H ST {TrendUi.GetSuperTrendLabel(analysis.Trend1H)}", st1HState),
            new(analysis.AboveVwap ? "Above VWAP" : "Below VWAP", vwapState),
            new($"15M {analysis.Structure.Structure15M.Summary}", structure15State),
            new(analysis.Adx >= 25 ? "ADX Strong" : analysis.Adx >= 18 ? $"ADX Moderate {analysis.Adx:N0}" : $"ADX Choppy {analysis.Adx:N0}", adxState),
            new($"RSI(28) {analysis.RsiTrend:N0}", rsiState),
            new(pocLabel, pocState),
            new(sweepLabel, sweepState),
            new($"{TrendUi.GetIcon(analysis.Trend5MEntry)} Entry 5M BOS/ST", entryPass ? "pass" : "warn")
        };

        if (footprintPass)
        {
            checks.Add(new(FootprintDisplayHelper.GetDisplayLabel(analysis.Footprint, true), "pass"));
        }
        else if (primaryBias != TrendDirection.Neutral)
        {
            foreach (var (label, state) in FootprintDisplayHelper.GetStep4Checks(analysis.Footprint, primaryBias))
                checks.Add(new(label, state));
        }
        else
        {
            checks.Add(new(
                FootprintDisplayHelper.GetDisplayLabel(analysis.Footprint, false),
                footprintState));
        }

        return checks;
    }

    /// <summary>Trade direction, else market bias, else 1H structure direction.</summary>
    public static TrendDirection ResolvePrimaryBias(MultiTimeframeAnalysis analysis)
    {
        if (analysis.TradeDirection != TrendDirection.Neutral)
            return analysis.TradeDirection;

        if (analysis.MarketBias != TrendDirection.Neutral)
            return analysis.MarketBias;

        return analysis.Structure.MajorDirection;
    }

    private static string Get1HStCheckState(MultiTimeframeAnalysis analysis, TrendDirection structureBias)
    {
        if (analysis.Trend1H == TrendDirection.Neutral)
            return "fail";

        // Hard conflict with 1H structure — never a green pass.
        if (structureBias != TrendDirection.Neutral && analysis.Trend1H != structureBias)
            return "fail";

        if (structureBias == TrendDirection.Neutral)
            return "pass";

        return analysis.Trend1H == structureBias ? "pass" : "warn";
    }

    private static string Get15MStructureCheckState(
        MultiTimeframeAnalysis analysis,
        TrendDirection primaryBias,
        TrendDirection structureBias)
    {
        var reference = primaryBias != TrendDirection.Neutral ? primaryBias : structureBias;
        var s15 = analysis.Structure.Structure15M;

        if (s15.Bias is StructureBias.Mixed or StructureBias.Insufficient)
            return "warn";

        if (reference == TrendDirection.Neutral)
            return "warn";

        var aligned = s15.Confirms(reference)
            || (reference == TrendDirection.Buy && s15.BosBullish)
            || (reference == TrendDirection.Sell && s15.BosBearish);

        if (aligned)
            return "pass";

        // Opposing 15M vs 1H / trade bias.
        if (s15.Bias is StructureBias.Bullish or StructureBias.Bearish
            && !s15.Confirms(reference))
            return "fail";

        return "warn";
    }

    private static string GetPocCheckState(
        MultiTimeframeAnalysis analysis,
        TrendDirection primaryBias,
        TrendDirection structureBias,
        bool tpoPass)
    {
        if (tpoPass)
            return "pass";

        var reference = primaryBias != TrendDirection.Neutral ? primaryBias : structureBias;
        if (reference == TrendDirection.Neutral)
            return analysis.IsRotationRegime ? "fail" : "warn";

        if (analysis.Tpo.Confirms(reference))
            return analysis.TradeDirection != TrendDirection.Neutral ? "pass" : "warn";

        // Location opposes the structure/trade bias (e.g. Bull above POC on a bearish 1H).
        if (analysis.Tpo.Bias != TrendDirection.Neutral && analysis.Tpo.Bias != reference)
            return "fail";

        return analysis.IsRotationRegime ? "fail" : "warn";
    }

    private static string GetVwapCheckState(
        MultiTimeframeAnalysis analysis,
        TrendDirection primaryBias,
        TrendDirection structureBias)
    {
        var reference = primaryBias != TrendDirection.Neutral ? primaryBias : structureBias;

        if (reference == TrendDirection.Buy)
            return analysis.AboveVwap ? "pass" : "fail";

        if (reference == TrendDirection.Sell)
            return analysis.AboveVwap ? "fail" : "pass";

        // No structure/trade bias — fall back to ST vs VWAP hard-oppose rules.
        if (analysis.Trend1H == TrendDirection.Buy && !analysis.AboveVwap)
            return "fail";

        if (analysis.Trend1H == TrendDirection.Sell && analysis.AboveVwap)
            return "fail";

        return "warn";
    }

    private static string GetDevelopingWaitDetail(MultiTimeframeAnalysis analysis)
    {
        var status = analysis.FrameworkStatus?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(status)
            || status.Equals("Ready", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Wait", StringComparison.OrdinalIgnoreCase))
        {
            return "RSI mid but ADX > 22 — wait 1H structure + 15M BOS confirmation (not auto-chop).";
        }

        // Always surface evaluator reasons (structure vs ST, VWAP, 15M, BOS, POC, etc.).
        return status;
    }

    public static string Get15MStCheckState(MultiTimeframeAnalysis analysis)
    {
        var bias = ResolvePrimaryBias(analysis);
        if (bias == TrendDirection.Neutral)
            return "warn";

        if (analysis.Trend15M == bias)
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
        if (!string.IsNullOrWhiteSpace(analysis.FrameworkStatus)
            && !analysis.FrameworkStatus.Equals("Ready", StringComparison.OrdinalIgnoreCase)
            && !analysis.FrameworkStatus.StartsWith("Ready", StringComparison.OrdinalIgnoreCase))
        {
            // Prefer evaluator status — it already encodes structure / regime / sweep / VP / BOS order.
            if (analysis.TradeDirection == TrendDirection.Neutral
                || analysis.FrameworkStatus.Contains("structure", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("15M", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("sweep", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("chop", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("Developing", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("neutral", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("POC", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("VWAP", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("BOS", StringComparison.OrdinalIgnoreCase)
                || analysis.FrameworkStatus.Contains("ADX", StringComparison.OrdinalIgnoreCase))
            {
                return FormatDetail(analysis.FrameworkStatus);
            }
        }

        if (analysis.MarketBias == TrendDirection.Neutral && analysis.Trend1H != TrendDirection.Neutral)
        {
            if (analysis.Trend1H == TrendDirection.Buy && !analysis.AboveVwap)
                return "Step 1 incomplete — bullish structure/ST but price is below session VWAP. Wait for reclaim above VWAP.";

            if (analysis.Trend1H == TrendDirection.Sell && analysis.AboveVwap)
                return "Step 1 incomplete — bearish structure/ST but price is above session VWAP. Wait for rejection below VWAP.";
        }

        if (analysis.TradeDirection == TrendDirection.Neutral && analysis.MarketBias != TrendDirection.Neutral)
            return $"Setup incomplete — wait 15M BOS / volume-profile location for {TrendUi.GetBiasLabel(analysis.MarketBias)}. Regime: {TradeFrameworkEvaluator.RegimeLabel(analysis.Regime)}.";

        // Only treat POC as blocking when trade direction already exists.
        if (!analysis.TpoConfirmed && analysis.TradeDirection != TrendDirection.Neutral)
            return $"POC not confirmed — {analysis.Tpo.Summary}. {NextStepHint(analysis)}";

        if (!analysis.EntryTriggered && analysis.TradeDirection != TrendDirection.Neutral)
            return $"5M BOS not triggered — structure shows {analysis.Structure.Structure5M.Summary}; need {TrendUi.GetBiasLabel(analysis.TradeDirection)} break.";

        if (!analysis.FootprintConfirmed && analysis.TradeDirection != TrendDirection.Neutral)
            return FootprintDisplayHelper.GetStep4BlockingDetail(analysis.Footprint, analysis.TradeDirection);

        return FormatDetail(analysis.FrameworkStatus);
    }

    private static string NextStepHint(MultiTimeframeAnalysis analysis)
    {
        if (!analysis.EntryTriggered)
            return "Watch 5M BOS / CHOCH for entry.";

        if (!analysis.FootprintConfirmed)
            return FootprintDisplayHelper.GetStep4BlockingDetail(
                analysis.Footprint,
                analysis.TradeDirection != TrendDirection.Neutral
                    ? analysis.TradeDirection
                    : analysis.MarketBias);

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
