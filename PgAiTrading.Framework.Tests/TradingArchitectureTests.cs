using PgAiTrading.Models;
using PgAiTrading.Models.Trading;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class BrokerProductMapperTests
{
    [Theory]
    [InlineData(ProductTypes.Mis, ExchangeCodes.Nse, ProductTypes.Mis)]
    [InlineData(ProductTypes.Mis, ExchangeCodes.Nfo, ProductTypes.Mis)]
    [InlineData(ProductTypes.Cnc, ExchangeCodes.Nse, ProductTypes.Cnc)]
    [InlineData(ProductTypes.Cnc, ExchangeCodes.Nfo, ProductTypes.Nrml)]
    [InlineData("cnc", "nfo", ProductTypes.Nrml)]
    [InlineData("mis", "NSE", ProductTypes.Mis)]
    public void Maps_ui_product_to_broker_product(string ui, string exchange, string expected) =>
        Assert.Equal(expected, BrokerProductMapper.ToBrokerProduct(ui, exchange));

    [Fact]
    public void Describe_cnc_on_nfo_shows_nrml() =>
        Assert.Equal("CNC (NRML)", BrokerProductMapper.DescribeUiProduct(ProductTypes.Cnc, ExchangeCodes.Nfo));

    [Fact]
    public void Describe_cnc_on_nse_is_cnc() =>
        Assert.Equal("CNC", BrokerProductMapper.DescribeUiProduct(ProductTypes.Cnc, ExchangeCodes.Nse));
}

public class LimitPriceCalculatorTests
{
    [Fact]
    public void AtLtp_rounds_equity_to_paisa()
    {
        var price = LimitPriceCalculator.Compute(ExchangeCodes.Nse, OrderSides.Buy, 100.124m, LimitPricingMode.AtLtp);
        Assert.Equal(100.12m, price);
    }

    [Fact]
    public void Aggressive_buy_offsets_up_by_nfo_tick()
    {
        var price = LimitPriceCalculator.Compute(ExchangeCodes.Nfo, OrderSides.Buy, 140.80m, LimitPricingMode.AggressiveOffset);
        Assert.Equal(140.85m, price);
    }

    [Fact]
    public void Aggressive_sell_offsets_down_by_nfo_tick()
    {
        var price = LimitPriceCalculator.Compute(ExchangeCodes.Nfo, OrderSides.Sell, 140.80m, LimitPricingMode.AggressiveOffset);
        Assert.Equal(140.75m, price);
    }

    [Fact]
    public void RawLtp_preserves_exact_price_without_rounding()
    {
        var price = LimitPriceCalculator.Compute(ExchangeCodes.Nse, OrderSides.Buy, 100.124m, LimitPricingMode.RawLtp);
        Assert.Equal(100.124m, price);
    }
}

public class OrderPriceHelperTradingTests
{
    [Fact]
    public void BuildInstrumentKey_uses_exchange_codes()
    {
        var key = OrderPriceHelper.BuildInstrumentKey(new Position
        {
            Exchange = ExchangeCodes.Nse,
            Symbol = "RELIANCE"
        });
        Assert.Equal("NSE:RELIANCE", key);
    }
}
