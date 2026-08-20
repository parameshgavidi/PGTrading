using PgAiTrading.Models;
using PgAiTrading.Services;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class MarketStructureAndRegimeTests
{
  private static StrategyConfig Config => new();
  private readonly MarketStructureService _structure = new();

  [Fact]
  public void Regime_rsi_mid_adx_low_is_strong_chop()
  {
    Assert.Equal(MarketRegime.StrongChop, TradeFrameworkEvaluator.GetRegime(50m, 15m, Config));
  }

  [Fact]
  public void Regime_rsi_mid_adx_developing_is_not_auto_chop()
  {
    Assert.Equal(MarketRegime.DevelopingTrend, TradeFrameworkEvaluator.GetRegime(50m, 23m, Config));
  }

  [Fact]
  public void Regime_rsi_mid_adx_soft_is_soft_neutral()
  {
    Assert.Equal(MarketRegime.SoftNeutral, TradeFrameworkEvaluator.GetRegime(50m, 20m, Config));
  }

  [Fact]
  public void Entry_requires_5m_bos_not_supertrend_alone()
  {
    var structure5M = new MarketStructureAnalysis
    {
      Bias = StructureBias.Bullish,
      BosBullish = false,
      LatestEvent = StructureEvent.None
    };

    Assert.False(TradeFrameworkEvaluator.EntryTriggered(
      TrendDirection.Buy, structure5M, TrendDirection.Buy));

    structure5M.BosBullish = true;
    structure5M.LatestEvent = StructureEvent.BosBullish;
    Assert.True(TradeFrameworkEvaluator.EntryTriggered(
      TrendDirection.Buy, structure5M, TrendDirection.Neutral));
  }

  [Fact]
  public void Major_direction_comes_from_1h_only()
  {
    var mtf = new MultiTimeframeStructure
    {
      Structure1H = new MarketStructureAnalysis { Bias = StructureBias.Bullish },
      Structure15M = new MarketStructureAnalysis { Bias = StructureBias.Bearish },
      Structure5M = new MarketStructureAnalysis { Bias = StructureBias.Bearish }
    };

    Assert.Equal(TrendDirection.Buy, mtf.MajorDirection);
  }

  [Fact]
  public void Ai_blocking_prefers_framework_status_over_false_poc_wait()
  {
    var analysis = new MultiTimeframeAnalysis
    {
      MarketBias = TrendDirection.Buy,
      TradeDirection = TrendDirection.Neutral,
      Trend1H = TrendDirection.Buy,
      AboveVwap = true,
      Regime = MarketRegime.DevelopingTrend,
      TpoConfirmed = false,
      FrameworkReady = false,
      FrameworkStatus = "Developing — wait 15M BOS with 1H structure",
      OverallScore = 55,
      Strength = "Developing"
    };

    var rec = AiInsightHelper.BuildRecommendation(new Signal(), analysis);
    Assert.Equal("WAIT STRUCTURE", rec.ActionHeadline);
    Assert.Contains("15M", rec.ActionDetail, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("POC not confirmed", rec.ActionDetail, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void Ai_developing_surfaces_structure_vs_st_conflict()
  {
    var analysis = new MultiTimeframeAnalysis
    {
      MarketBias = TrendDirection.Neutral,
      TradeDirection = TrendDirection.Neutral,
      Trend1H = TrendDirection.Buy,
      AboveVwap = true,
      Regime = MarketRegime.DevelopingTrend,
      Structure = new MultiTimeframeStructure
      {
        Structure1H = new MarketStructureAnalysis { Bias = StructureBias.Bearish, Summary = "Bearish (LH+LL)" }
      },
      FrameworkReady = false,
      FrameworkStatus = "Wait — 1H structure vs SuperTrend conflict",
      OverallScore = 54,
      Strength = "Developing"
    };

    var rec = AiInsightHelper.BuildRecommendation(new Signal(), analysis);
    Assert.Equal("WAIT STRUCTURE", rec.ActionHeadline);
    Assert.Contains("structure vs SuperTrend", rec.ActionDetail, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void Detects_bullish_hh_hl_structure()
  {
    // Explicit fractal swings: L1, H1, L2(higher), H2(higher)
    var candles = new List<Candle>();
    void Add(decimal o, decimal h, decimal l, decimal c)
      => candles.Add(new Candle
      {
        Timestamp = DateTime.UtcNow.AddMinutes(candles.Count * 60),
        Open = o, High = h, Low = l, Close = c, Volume = 1000
      });

    // pad left
    Add(100, 101, 99, 100);
    Add(100, 101, 99, 100);
    // swing low ~98
    Add(100, 100.5m, 98, 98.5m);
    Add(98.5m, 99, 97.5m, 98);
    Add(98, 99, 97.8m, 98.5m);
    // rise to swing high ~105
    Add(98.5m, 102, 98.4m, 101);
    Add(101, 105, 100.5m, 104);
    Add(104, 105.5m, 103, 104.5m);
    Add(104.5m, 105, 103.5m, 104);
    // pullback higher low ~100
    Add(104, 104.2m, 101, 101.5m);
    Add(101.5m, 102, 100, 100.5m);
    Add(100.5m, 101.5m, 100.2m, 101);
    // higher high ~110
    Add(101, 106, 100.8m, 105);
    Add(105, 110, 104.5m, 109);
    Add(109, 110.5m, 108, 109.5m);
    Add(109.5m, 110, 108.5m, 109);
    // pad right
    Add(109, 109.5m, 108, 108.5m);
    Add(108.5m, 109, 107.5m, 108);

    var analysis = _structure.Analyze(candles, swingStrength: 2);
    Assert.True(analysis.Swings.Count >= 2, $"swings={analysis.Swings.Count} summary={analysis.Summary}");
  }

  [Fact]
  public void Strong_chop_requires_confirmed_sweep_for_direction()
  {
    var structure = new MultiTimeframeStructure
    {
      Structure1H = new MarketStructureAnalysis { Bias = StructureBias.Mixed },
      Structure15M = new MarketStructureAnalysis { Bias = StructureBias.Mixed },
      Structure5M = new MarketStructureAnalysis { Bias = StructureBias.Mixed }
    };

    var noSweep = new LiquiditySweepAnalysis();
    var dir = TradeFrameworkEvaluator.GetTradeDirection(
      TrendDirection.Neutral,
      structure,
      MarketRegime.StrongChop,
      adx1H: 15m,
      price: 100m,
      new VolumeProfileLevels { HasData = true, Poc = 100, Vah = 105, Val = 95 },
      noSweep,
      Config);

    Assert.Equal(TrendDirection.Neutral, dir);

    var sweep = new LiquiditySweepAnalysis
    {
      Detected = true,
      Side = LiquiditySweepSide.SellSide,
      Reclaimed = true,
      StructureConfirmed = true,
      LevelName = "PDL",
      LevelPrice = 95
    };

    var mr = TradeFrameworkEvaluator.GetTradeDirection(
      TrendDirection.Neutral,
      structure,
      MarketRegime.StrongChop,
      adx1H: 15m,
      price: 100m,
      new VolumeProfileLevels { HasData = true, Poc = 100, Vah = 105, Val = 95 },
      sweep,
      Config);

    Assert.Equal(TrendDirection.Buy, mr);
  }

  [Fact]
  public void Liquidity_sweep_detects_sell_side_reclaim()
  {
    var profile = new VolumeProfileLevels
    {
      HasData = true,
      Pdl = 95
      // only PDL — avoid competing POC/VAH buy-side matches
    };

    var candles = new List<Candle>
    {
      Bar(100, 101, 99, 100),
      Bar(100, 100.5m, 94, 94.5m), // sweep below PDL 95
      Bar(94.5m, 97, 94.2m, 96.5m),
      Bar(96.5m, 98, 96, 97.5m) // reclaim above 95
    };

    var structure15 = new MarketStructureAnalysis();
    var structure5 = new MarketStructureAnalysis
    {
      Bias = StructureBias.Bullish,
      BosBullish = true,
      LatestEvent = StructureEvent.BosBullish,
      LastSwingHigh = 97
    };
    var footprint = new FootprintAnalysis { PositiveDelta = true, AbsorptionAgainstShort = true };

    var sweep = LiquiditySweepEvaluator.Evaluate(candles, profile, structure15, structure5, footprint);
    Assert.True(sweep.Detected);
    Assert.Equal(LiquiditySweepSide.SellSide, sweep.Side);
    Assert.True(sweep.Reclaimed);
    Assert.True(sweep.StructureConfirmed);
    Assert.True(sweep.IsConfirmedSetup);
  }

  [Fact]
  public void Blocking_reason_strong_chop_mentions_sweep()
  {
    var reason = TradeFrameworkEvaluator.GetBlockingReason(
      TrendDirection.Neutral,
      TrendDirection.Neutral,
      TrendDirection.Buy,
      TrendDirection.Buy,
      TrendDirection.Buy,
      adx1H: 15m,
      rsi1H: 50m,
      aboveVwap: true,
      new FootprintAnalysis(),
      new TpoConfirmationAnalysis(),
      waitForReversal: false,
      isRotationRegime: false,
      isRangebound: true,
      Config);

    Assert.Contains("chop", reason, StringComparison.OrdinalIgnoreCase);
  }

  private static Candle Bar(decimal open, decimal high, decimal low, decimal close) => new()
  {
    Timestamp = DateTime.UtcNow,
    Open = open,
    High = high,
    Low = low,
    Close = close,
    Volume = 1000
  };
}
