using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

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
            Footprint = new FootprintAnalysis { NegativeDelta = true, FuturesSymbol = "NIFTY26AUGFUT" },
            FootprintConfirmed = false
        };

        var footprintCheck = AiInsightHelper.BuildChecks(analysis)
            .First(c => c.Label.StartsWith("5m delta", StringComparison.Ordinal));
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
            FuturesSymbol = "NIFTY26AUGFUT"
        };
        var analysis = new MultiTimeframeAnalysis
        {
            MarketBias = TrendDirection.Buy,
            TradeDirection = TrendDirection.Buy,
            Trend1H = TrendDirection.Buy,
            Trend15M = TrendDirection.Buy,
            Trend5MEntry = TrendDirection.Buy,
            TpoConfirmed = true,
            EntryTriggered = true,
            FootprintConfirmed = false,
            FrameworkReady = false,
            Footprint = footprint,
            OverallScore = 82,
            Strength = "Strong Setup"
        };

        var recommendation = AiInsightHelper.BuildRecommendation(new Signal(), analysis);
        Assert.Equal("WAIT", recommendation.ActionHeadline);
        Assert.Contains("stacked", recommendation.ActionDetail, StringComparison.OrdinalIgnoreCase);

        var checks = AiInsightHelper.BuildChecks(analysis);
        Assert.Equal(10, checks.Count);
        Assert.Equal("pass", checks[8].State);
        Assert.Equal("warn", checks[9].State);
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
        Assert.Equal("pass", checks[0].State);
        Assert.Equal("fail", checks[1].State);

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
        Assert.Equal("pass", checks[0].State);
        Assert.Equal("fail", checks[1].State);
    }
}
