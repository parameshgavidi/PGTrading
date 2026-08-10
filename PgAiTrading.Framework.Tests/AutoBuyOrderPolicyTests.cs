using PgAiTrading.Models;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class AutoBuyOrderPolicyTests
{
    [Fact]
    public void EntrySideOrThrow_rejects_sell()
    {
        Assert.Throws<InvalidOperationException>(() => AutoBuyOrderPolicy.EntrySideOrThrow("SELL"));
        Assert.Equal("BUY", AutoBuyOrderPolicy.EntrySideOrThrow("BUY"));
    }

    [Fact]
    public void DetectBearishFlip_is_not_used_for_orders()
    {
        Assert.Equal("BUY", AutoBuyDefaults.EntrySide);
        Assert.False(AutoBuyOrderPolicy.IsAllowedEntrySide("SELL"));
    }
}
