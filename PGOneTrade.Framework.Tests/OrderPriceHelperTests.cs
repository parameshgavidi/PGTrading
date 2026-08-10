using PGOneTrade.Models;
using Xunit;

namespace PGOneTrade.Framework.Tests;

public class OrderPriceHelperTests
{
  [Fact]
  public void Nfo_prices_round_to_0_05_tick()
  {
    Assert.Equal(123.45m, OrderPriceHelper.RoundToTick(123.47m, "NFO"));
  }

  [Fact]
  public void Nse_prices_round_to_0_01_tick()
  {
    Assert.Equal(100.12m, OrderPriceHelper.RoundToTick(100.123m, "NSE"));
  }
}
