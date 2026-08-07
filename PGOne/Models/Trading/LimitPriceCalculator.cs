using PGOne.Models.Trading;

using PGOne.Models;

namespace PGOne.Models.Trading;

/// <summary>Pure pricing helpers shared by order execution (unit-testable).</summary>
public static class LimitPriceCalculator
{
    public static decimal Compute(string exchange, string side, decimal ltp, LimitPricingMode mode)
    {
        if (ltp <= 0)
            return 0m;

        var normalizedSide = OrderSides.Normalize(side);

        if (mode == LimitPricingMode.RawLtp)
            return ltp;

        if (mode == LimitPricingMode.AtLtp)
            return OrderPriceHelper.RoundToTick(ltp, exchange);

        var tick = OrderPriceHelper.GetTickSize(exchange);
        var raw = normalizedSide == OrderSides.Sell ? ltp - tick : ltp + tick;
        if (raw <= 0)
            raw = ltp;

        var rounded = OrderPriceHelper.RoundToTick(raw, exchange);
        return rounded > 0 ? rounded : OrderPriceHelper.RoundToTick(ltp, exchange);
    }
}
