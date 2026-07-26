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
}
