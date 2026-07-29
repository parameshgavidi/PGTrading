using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

public class TradeFrameworkEvaluatorTests
{
  private static StrategyConfig Config => new();

  [Fact]
  public void MarketBias_requires_1h_st_and_vwap_aligned()
  {
    Assert.Equal(TrendDirection.Buy, TradeFrameworkEvaluator.GetMarketBias(TrendDirection.Buy, true));
    Assert.Equal(TrendDirection.Sell, TradeFrameworkEvaluator.GetMarketBias(TrendDirection.Sell, false));
    Assert.Equal(TrendDirection.Neutral, TradeFrameworkEvaluator.GetMarketBias(TrendDirection.Buy, false));
    Assert.Equal(TrendDirection.Neutral, TradeFrameworkEvaluator.GetMarketBias(TrendDirection.Sell, true));
  }

  [Fact]
  public void Rsi_55_is_range_not_long()
  {
    Assert.False(TradeFrameworkEvaluator.RsiConfirmsLong(55m, Config));
    Assert.True(TradeFrameworkEvaluator.IsRangebound(55m, Config));
  }

  [Fact]
  public void Rsi_45_is_range_not_short()
  {
    Assert.False(TradeFrameworkEvaluator.RsiConfirmsShort(45m, Config));
    Assert.True(TradeFrameworkEvaluator.IsRangebound(45m, Config));
  }

  [Fact]
  public void Rsi_56_confirms_long_44_confirms_short()
  {
    Assert.True(TradeFrameworkEvaluator.RsiConfirmsLong(56m, Config));
    Assert.True(TradeFrameworkEvaluator.RsiConfirmsShort(44m, Config));
  }

  [Fact]
  public void Framework_not_ready_when_rangebound()
  {
    var footprint = new FootprintAnalysis
    {
      PositiveDelta = true,
      StackedBuyImbalance = true
    };

    Assert.False(TradeFrameworkEvaluator.IsFrameworkReady(
      TrendDirection.Buy,
      TrendDirection.Buy,
      footprint,
      waitForReversal: false,
      isRotationRegime: false,
      isRangebound: true));
  }

  [Fact]
  public void Adx_choppy_blocks_trade_direction()
  {
    var profile = new VolumeProfileLevels { HasData = true, Poc = 100, Vah = 110, Val = 90 };
    var tpo = new TpoConfirmationAnalysis { BuyConfirmed = true };

    var direction = TradeFrameworkEvaluator.GetTradeDirection(
      TrendDirection.Buy,
      TrendDirection.Buy,
      adx1H: 17m,
      rsi1H: 60m,
      price: 105m,
      profile,
      tpo,
      Config);

    Assert.Equal(TrendDirection.Neutral, direction);
  }

  [Fact]
  public void Poc_above_is_bull_below_is_bear()
  {
    var profile = new VolumeProfileLevels { HasData = true, Poc = 100, Vah = 110, Val = 90 };
    Assert.True(profile.ConfirmsBuy(101m));
    Assert.False(profile.ConfirmsSell(101m));
    Assert.True(profile.ConfirmsSell(99m));
    Assert.False(profile.ConfirmsBuy(99m));
  }

  [Fact]
  public void Rotation_when_adx_low_inside_va()
  {
    var profile = new VolumeProfileLevels { HasData = true, Poc = 100, Vah = 110, Val = 90 };
    Assert.True(TradeFrameworkEvaluator.IsRotationRegime(17m, 100m, profile, Config));
    Assert.False(TradeFrameworkEvaluator.IsRotationRegime(20m, 100m, profile, Config));
  }

  [Fact]
  public void Rangebound_score_reflects_partial_alignment_not_flat_45()
  {
    var footprint = new FootprintAnalysis { PositiveDelta = true };
    var tpo = new TpoConfirmationAnalysis { BuyConfirmed = true };

    var score = TradeFrameworkEvaluator.CalculateScore(
      marketBias: TrendDirection.Buy,
      tradeDirection: TrendDirection.Neutral,
      trend1H: TrendDirection.Buy,
      trend15M: TrendDirection.Buy,
      trend5MEntry: TrendDirection.Buy,
      strength1H: TrendStrength.Weak,
      aboveVwap: true,
      footprint,
      tpo,
      isRotationRegime: false,
      isRangebound: true,
      frameworkReady: false);

    Assert.True(score > 45);
    Assert.True(score <= 54);
    Assert.Equal("Range-bound", TradeFrameworkEvaluator.GetScoreStrengthLabel(score, true, false, false));
  }

  [Fact]
  public void Rotation_regime_caps_score_at_48()
  {
    var footprint = new FootprintAnalysis();
    var tpo = new TpoConfirmationAnalysis();

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
      isRotationRegime: true,
      isRangebound: false,
      frameworkReady: false);

    Assert.True(score <= 48);
    Assert.Equal("Rotation", TradeFrameworkEvaluator.GetScoreStrengthLabel(score, false, true, false));
  }

  [Fact]
  public void Framework_playbook_groups_have_rules()
  {
    Assert.All(FrameworkPlaybook.Groups, group =>
    {
      Assert.NotNull(group.Rules);
      Assert.NotEmpty(group.Rules);
    });
  }

  [Fact]
  public void Tpo_bias_reflects_buy_sell_confirmed()
  {
    var bull = new TpoConfirmationAnalysis { BuyConfirmed = true };
    var bear = new TpoConfirmationAnalysis { SellConfirmed = true };
    var neutral = new TpoConfirmationAnalysis();

    Assert.Equal(TrendDirection.Buy, bull.Bias);
    Assert.Equal(TrendDirection.Sell, bear.Bias);
    Assert.Equal(TrendDirection.Neutral, neutral.Bias);
  }

  [Fact]
  public void Value_area_bias_above_poc_and_vah_is_bullish()
  {
    var profile = new VolumeProfileLevels
    {
      HasData = true,
      Poc = 100,
      Vah = 110,
      Val = 90,
      PrevDayPoc = 95,
      PrevDayVah = 105,
      PrevDayVal = 85
    };

    Assert.Equal(TrendDirection.Buy, profile.GetSessionValueAreaBias(111m));
    Assert.Equal(TrendDirection.Sell, profile.GetSessionValueAreaBias(89m));
    Assert.Equal(TrendDirection.Neutral, profile.GetSessionValueAreaBias(100m));

    Assert.Equal(TrendDirection.Buy, profile.GetPrevDayValueAreaBias(106m));
    Assert.Equal(TrendDirection.Sell, profile.GetPrevDayValueAreaBias(84m));
    Assert.Equal(TrendDirection.Neutral, profile.GetPrevDayValueAreaBias(100m));
  }

  [Fact]
  public void Footprint_volume_proxy_uses_range_when_volume_zero()
  {
    var bullish = new Candle { High = 110, Low = 100, Close = 109, Open = 105, Volume = 0 };
    var bearish = new Candle { High = 110, Low = 100, Close = 101, Open = 105, Volume = 0 };

    var (bullBuy, bullSell) = FootprintVolumeEstimator.EstimateBidAskVolume(bullish);
    var (bearBuy, bearSell) = FootprintVolumeEstimator.EstimateBidAskVolume(bearish);

    Assert.True(bullBuy > bullSell);
    Assert.True(bearSell > bearBuy);
  }

  [Fact]
  public void Footprint_volume_zero_without_range_is_flat()
  {
    var flat = new Candle { High = 100, Low = 100, Close = 100, Open = 100, Volume = 0 };
    var (buy, sell) = FootprintVolumeEstimator.EstimateBidAskVolume(flat);
    Assert.Equal(0, buy);
    Assert.Equal(0, sell);
  }

  [Fact]
  public void Ai_15m_st_bullish_aligned_passes_when_trade_direction_pending()
  {
    var analysis = new MultiTimeframeAnalysis
    {
      MarketBias = TrendDirection.Buy,
      Trend15M = TrendDirection.Buy,
      TradeDirection = TrendDirection.Neutral
    };

    Assert.Equal("pass", AiInsightHelper.Get15MStCheckState(analysis));
  }

  [Fact]
  public void Ai_15m_st_opposing_market_bias_fails()
  {
    var analysis = new MultiTimeframeAnalysis
    {
      MarketBias = TrendDirection.Buy,
      Trend15M = TrendDirection.Sell
    };

    Assert.Equal("fail", AiInsightHelper.Get15MStCheckState(analysis));
  }

  [Fact]
  public void Ai_rsi_55_is_warn_not_pass()
  {
    var analysis = new MultiTimeframeAnalysis { RsiTrend = 55m };
    var rsiCheck = AiInsightHelper.BuildChecks(analysis).First(c => c.Label.StartsWith("RSI"));
    Assert.Equal("warn", rsiCheck.State);
  }

  [Fact]
  public void Camarilla_levels_from_prev_day()
  {
    var prev = new Candle { High = 23850m, Low = 23650m, Close = 23773.30m };
    var cam = CamarillaCalculator.FromPreviousDay(prev);

    Assert.True(cam.HasData);
    Assert.True(cam.H4 > cam.H3);
    Assert.True(cam.H3 > cam.H2);
    Assert.True(cam.H2 > cam.Pivot);
    Assert.True(cam.Pivot > cam.L2);
    Assert.True(cam.L2 > cam.L3);
    Assert.True(cam.L3 > cam.L4);
  }

  [Fact]
  public void Camarilla_bias_above_pp_and_h2()
  {
    var cam = new CamarillaLevels { HasData = true, Pivot = 23758m, H2 = 23810m, L2 = 23637m };
    Assert.Equal(TrendDirection.Buy, cam.GetBias(23923m));
    Assert.Equal(TrendDirection.Sell, cam.GetBias(23600m));
  }
}
