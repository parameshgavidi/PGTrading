using PgAiTrading.Models.Trading;

namespace PgAiTrading.Models;

public static class OrderPriceHelper
{
    public static decimal GetTickSize(string exchange) =>
        ExchangeCodes.IsDerivatives(exchange) ? 0.05m : 0.01m;

    public static decimal RoundToTick(decimal price, string exchange)
    {
        if (price <= 0)
            return price;

        var tick = GetTickSize(exchange);
        return Math.Round(price / tick, MidpointRounding.AwayFromZero) * tick;
    }

    public static string BuildInstrumentKey(Position position) =>
        ExchangeCodes.InstrumentKey(position.Exchange, position.Symbol);
}
