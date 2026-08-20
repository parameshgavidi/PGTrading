using PgAiTrading.Models;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class AiInsightHelperTests
{
    [Fact]
    public void Footprint_conflict_caps_score_and_structured_wait()
    {
        var footprint = new FootprintAnalysis
        {
            NegativeDelta = true,
            VolumeSource = "futures",
            FuturesSymbol = "NIFTY26AUGFUT",
            Delta = -51200m
        };
        var tpo = new TpoConfirmationAnalysis { BuyConfirmed = true };

        var score = TradeFrameworkEvaluator.CalculateScore(
            TrendDirection.Buy,
            TrendDirection.Buy,
            TrendDirection.Buy,
            TrendDirection.Buy,
            TrendDirection.Buy,
            TrendStrength.Strong,
            aboveVwap: true,
            footprint,
            tpo,
            isRotationRegime: false,
            isRangebound: false,
            frameworkReady: false);

        Assert.True(score <= 68);

        var strength = TradeFrameworkEvaluator.GetScoreStrengthLabel(
            score, false, false, false, footprintConflict: true);

        Assert.Equal("Flow Conflict", strength);

        var analysis = new MultiTimeframeAnalysis
        {
            MarketBias = TrendDirection.Buy,
            TradeDirection = TrendDirection.Buy,
            Trend1H = TrendDirection.Buy,
            Trend15M = TrendDirection.Buy,
            Trend5MEntry = TrendDirection.Buy,
            AboveVwap = true,
            Adx = 28m,
            RsiTrend = 61m,
            TpoConfirmed = true,
            EntryTriggered = true,
            FootprintConfirmed = false,
            FrameworkReady = false,
            FrameworkStatus = "Wait — footprint not confirmed",
            Footprint = footprint,
            Tpo = tpo,
            OverallScore = score,
            Strength = strength
        };

        var recommendation = AiInsightHelper.BuildRecommendation(new Signal(), analysis);

        Assert.Equal("WAIT", recommendation.ActionHeadline);
        Assert.Equal("wait", recommendation.ActionKind);
        Assert.Contains("opposes", recommendation.ActionDetail, StringComparison.OrdinalIgnoreCase);
        Assert.True(recommendation.Probability <= 68);
    }

    [Fact]
    public void Framework_ready_suggests_structured_buy_with_targets()
    {
        var footprint = new FootprintAnalysis
        {
            PositiveDelta = true,
            StackedBuyImbalance = true,
            VolumeSource = "futures",
            FuturesSymbol = "NIFTY26AUGFUT"
        };
        var tpo = new TpoConfirmationAnalysis { BuyConfirmed = true };
        var profile = new VolumeProfileLevels { HasData = true, Poc = 100, Vah = 110, Val = 90 };

        var analysis = new MultiTimeframeAnalysis
        {
            MarketBias = TrendDirection.Buy,
            TradeDirection = TrendDirection.Buy,
            Trend1H = TrendDirection.Buy,
            Trend15M = TrendDirection.Buy,
            Trend5MEntry = TrendDirection.Buy,
            AboveVwap = true,
            Adx = 28m,
            RsiTrend = 61m,
            TpoConfirmed = true,
            EntryTriggered = true,
            FootprintConfirmed = true,
            FrameworkReady = true,
            FrameworkStatus = "Ready",
            Footprint = footprint,
            Tpo = tpo,
            VolumeProfile = profile,
            OverallScore = 88,
            Strength = "Ready"
        };

        var signal = new Signal
        {
            Trend = TrendDirection.Buy,
            Target = "VAH / POC ladder"
        };

        var recommendation = AiInsightHelper.BuildRecommendation(signal, analysis);

        Assert.Equal("BUY ON DIP", recommendation.ActionHeadline);
        Assert.Equal("buy", recommendation.ActionKind);
        Assert.Contains("All 5 steps aligned", recommendation.ActionDetail);
        Assert.Contains("VAH", recommendation.ActionDetail);
    }

    [Fact]
    public void Footprint_opposing_check_is_fail_not_warn()
    {
        var analysis = new MultiTimeframeAnalysis
        {
            MarketBias = TrendDirection.Buy,
            TradeDirection = TrendDirection.Buy,
            Footprint = new FootprintAnalysis
            {
                NegativeDelta = true,
                FuturesSymbol = "NIFTY26AUGFUT",
                VolumeSource = "futures",
                Summary = "Δ− stacked sell"
            },
            FootprintConfirmed = false
        };

        var footprintCheck = AiInsightHelper.BuildChecks(analysis)
            .First(c => c.Label.Contains("delta", StringComparison.OrdinalIgnoreCase)
                        || c.Label.Contains("Footprint", StringComparison.OrdinalIgnoreCase)
                        || c.Label.Contains("stacked", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("fail", footprintCheck.State);
    }

    [Fact]
    public void Footprint_missing_stacked_shows_step4_detail_in_wait()
    {
        var footprint = new FootprintAnalysis
        {
            Delta = 67_800m,
            PositiveDelta = true,
            VolumeSource = "futures",
            FuturesSymbol = "NIFTY26AUGFUT",
            Summary = "Δ+ no stacked buy"
        };
        var analysis = new MultiTimeframeAnalysis
        {
            MarketBias = TrendDirection.Buy,
            TradeDirection = TrendDirection.Buy,
            Trend1H = TrendDirection.Buy,
            Trend15M = TrendDirection.Buy,
            Trend5MEntry = TrendDirection.Buy,
            Regime = MarketRegime.TrendingBullish,
            Structure = new MultiTimeframeStructure
            {
                Structure1H = new MarketStructureAnalysis { Bias = StructureBias.Bullish, Summary = "Bullish (HH+HL)" }
            },
            TpoConfirmed = true,
            EntryTriggered = true,
            FootprintConfirmed = false,
            FrameworkReady = false,
            Footprint = footprint,
            OverallScore = 82,
            Strength = "Strong Setup",
            FrameworkStatus = "Wait — footprint not confirmed"
        };

        var recommendation = AiInsightHelper.BuildRecommendation(new Signal(), analysis);
        Assert.Equal("WAIT", recommendation.ActionHeadline);

        var checks = AiInsightHelper.BuildChecks(analysis);
        Assert.True(checks.Count >= 9);
        Assert.Contains(checks, c => c.Label.Contains("delta", StringComparison.OrdinalIgnoreCase)
                                     || c.Label.Contains("stacked", StringComparison.OrdinalIgnoreCase)
                                     || c.Label.Contains("Footprint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Step1_bullish_st_below_vwap_marks_st_pass_and_vwap_fail()
    {
        var analysis = new MultiTimeframeAnalysis
        {
            Trend1H = TrendDirection.Buy,
            MarketBias = TrendDirection.Neutral,
            AboveVwap = false,
            FrameworkReady = false,
            FrameworkStatus = "Step 1 — 1H ST bullish but price below session VWAP"
        };

        var checks = AiInsightHelper.BuildChecks(analysis);
        // [0]=1H structure, [1]=1H ST, [2]=VWAP
        Assert.Equal("pass", checks[1].State);
        Assert.Equal("fail", checks[2].State);

        var recommendation = AiInsightHelper.BuildRecommendation(new Signal(), analysis);
        Assert.Equal("WAIT", recommendation.ActionHeadline);
        Assert.Contains("below session VWAP", recommendation.ActionDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step1_bearish_st_above_vwap_marks_vwap_fail()
    {
        var analysis = new MultiTimeframeAnalysis
        {
            Trend1H = TrendDirection.Sell,
            MarketBias = TrendDirection.Neutral,
            AboveVwap = true,
            FrameworkReady = false,
            FrameworkStatus = "Step 1 — 1H ST bearish but price above session VWAP"
        };

        var checks = AiInsightHelper.BuildChecks(analysis);
        Assert.Equal("pass", checks[1].State);
        Assert.Equal("fail", checks[2].State);
    }

    [Fact]
    public void Structure_vs_st_conflict_marks_st_fail_not_pass()
    {
        var analysis = new MultiTimeframeAnalysis
        {
            Trend1H = TrendDirection.Buy,
            MarketBias = TrendDirection.Neutral,
            AboveVwap = true,
            Regime = MarketRegime.DevelopingTrend,
            Structure = new MultiTimeframeStructure
            {
                Structure1H = new MarketStructureAnalysis
                {
                    Bias = StructureBias.Bearish,
                    Summary = "Bearish (LH+LL)",
                    HasLowerHigh = true,
                    HasLowerLow = true
                },
                Structure15M = new MarketStructureAnalysis
                {
                    Bias = StructureBias.Bullish,
                    Summary = "Bullish (HH+HL)",
                    HasHigherHigh = true,
                    HasHigherLow = true
                }
            },
            Tpo = new TpoConfirmationAnalysis { BuyConfirmed = true, Summary = "Bull — above POC" },
            FrameworkReady = false,
            FrameworkStatus = "Wait — 1H structure vs SuperTrend conflict",
            Adx = 28m,
            RsiTrend = 47m
        };

        var checks = AiInsightHelper.BuildChecks(analysis);
        Assert.Equal("pass", checks[0].State); // 1H structure directional
        Assert.Equal("fail", checks[1].State); // ST opposes structure
        Assert.Equal("fail", checks[2].State); // Above VWAP vs bearish structure
        Assert.Equal("fail", checks[3].State); // 15M bullish vs 1H bearish
        Assert.Equal("pass", checks[4].State); // ADX strong
        Assert.Equal("pass", checks[5].State); // RSI mid expected in Developing
        Assert.Equal("fail", checks[6].State); // Bull POC vs bearish structure

        var recommendation = AiInsightHelper.BuildRecommendation(new Signal(), analysis);
        Assert.Equal("WAIT STRUCTURE", recommendation.ActionHeadline);
        Assert.Contains("structure vs SuperTrend", recommendation.ActionDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Perfect_aligned_long_marks_core_checks_pass()
    {
        var analysis = new MultiTimeframeAnalysis
        {
            Trend1H = TrendDirection.Buy,
            Trend15M = TrendDirection.Buy,
            Trend5MEntry = TrendDirection.Buy,
            MarketBias = TrendDirection.Buy,
            TradeDirection = TrendDirection.Buy,
            AboveVwap = true,
            Regime = MarketRegime.TrendingBullish,
            Adx = 28m,
            RsiTrend = 61m,
            Structure = new MultiTimeframeStructure
            {
                Structure1H = new MarketStructureAnalysis
                {
                    Bias = StructureBias.Bullish,
                    Summary = "Bullish (HH+HL)",
                    HasHigherHigh = true,
                    HasHigherLow = true
                },
                Structure15M = new MarketStructureAnalysis
                {
                    Bias = StructureBias.Bullish,
                    Summary = "Bullish (HH+HL)",
                    HasHigherHigh = true,
                    HasHigherLow = true,
                    BosBullish = true,
                    LatestEvent = StructureEvent.BosBullish
                },
                Structure5M = new MarketStructureAnalysis
                {
                    Bias = StructureBias.Bullish,
                    BosBullish = true,
                    LatestEvent = StructureEvent.BosBullish
                }
            },
            TpoConfirmed = true,
            Tpo = new TpoConfirmationAnalysis { BuyConfirmed = true, Summary = "Bull — above POC" },
            EntryTriggered = true,
            FootprintConfirmed = true,
            Footprint = new FootprintAnalysis
            {
                PositiveDelta = true,
                StackedBuyImbalance = true,
                VolumeSource = "futures",
                FuturesSymbol = "NIFTY26AUGFUT"
            },
            FrameworkReady = true,
            FrameworkStatus = "Ready",
            OverallScore = 88,
            Strength = "Ready"
        };

        var checks = AiInsightHelper.BuildChecks(analysis);
        Assert.All(checks.Take(7), c => Assert.Equal("pass", c.State));
        Assert.Equal("pass", checks[8].State); // entry
        Assert.Contains(checks, c => c.State == "pass" && (
            c.Label.Contains("Footprint", StringComparison.OrdinalIgnoreCase)
            || c.Label.Contains("Bullish", StringComparison.OrdinalIgnoreCase)
            || c.Label.Contains("delta", StringComparison.OrdinalIgnoreCase)));

        var signal = new Signal { Trend = TrendDirection.Buy, Target = "VAH" };
        Assert.True(AiInsightHelper.IsPerfectEntry(analysis, signal));
        var recommendation = AiInsightHelper.BuildRecommendation(signal, analysis);
        Assert.Equal("buy", recommendation.ActionKind);
    }

    [Fact]
    public void IsPerfectEntry_true_when_framework_ready_and_buy_signal()
    {
        var analysis = new MultiTimeframeAnalysis { FrameworkReady = true };
        var signal = new Signal { Trend = TrendDirection.Buy };

        Assert.True(AiInsightHelper.IsPerfectEntry(analysis, signal));

        var recommendation = AiInsightHelper.BuildRecommendation(signal, analysis);
        Assert.Equal("buy", recommendation.ActionKind);
    }

    [Fact]
    public void IsPerfectEntry_false_when_framework_not_ready()
    {
        var analysis = new MultiTimeframeAnalysis { FrameworkReady = false };
        var signal = new Signal { Trend = TrendDirection.Buy };

        Assert.False(AiInsightHelper.IsPerfectEntry(analysis, signal));
    }
}
