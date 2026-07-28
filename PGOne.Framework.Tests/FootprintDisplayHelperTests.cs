using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

public class FootprintDisplayHelperTests
{
  [Fact]
  public void Display_label_not_confirmed_shows_fut_flow_for_bullish_delta()
  {
    var fp = new FootprintAnalysis
    {
      Delta = 143_500m,
      PositiveDelta = true,
      VolumeSource = "futures",
      FuturesSymbol = "NIFTY26JULFUT",
      Summary = "Fut Δ +143.5K, NIFTY26JULFUT"
    };

    var label = FootprintDisplayHelper.GetDisplayLabel(fp, footprintConfirmed: false);

    Assert.Equal("Bullish fut flow · buy > sell (Δ +143.5K) · NIFTY26JULFUT", label);
  }

  [Fact]
  public void Display_label_confirmed_shows_footprint_ok()
  {
    var fp = new FootprintAnalysis
    {
      Delta = 143_500m,
      PositiveDelta = true,
      StackedBuyImbalance = true,
      VolumeSource = "futures",
      FuturesSymbol = "NIFTY26JULFUT",
      Summary = "technical"
    };

    var label = FootprintDisplayHelper.GetDisplayLabel(fp, footprintConfirmed: true);

    Assert.Equal("Footprint OK · Bullish · Δ +143.5K · NIFTY26JULFUT", label);
  }

  [Fact]
  public void Display_label_never_shows_buy_confirmed_without_framework_pass()
  {
    var fp = new FootprintAnalysis
    {
      Delta = 151_600m,
      PositiveDelta = true,
      StackedBuyImbalance = true,
      VolumeSource = "futures",
      FuturesSymbol = "NIFTY26JULFUT",
      Summary = "Fut Δ +151.6K, NIFTY26JULFUT"
    };

    var label = FootprintDisplayHelper.GetDisplayLabel(fp, footprintConfirmed: false);

    Assert.DoesNotContain("confirmed", label, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Bullish fut flow", label);
  }

  [Fact]
  public void Display_class_follows_flow_bias_not_framework_pass()
  {
    var fp = new FootprintAnalysis
    {
      PositiveDelta = true,
      Delta = 100_000m
    };

    Assert.Equal("buy", FootprintDisplayHelper.GetDisplayClass(fp));
  }
}
