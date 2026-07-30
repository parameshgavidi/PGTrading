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

        var footprintCheck = AiInsightHelper.BuildChecks(analysis)[^1];
        Assert.Equal("fail", footprintCheck.State);
    }
}
