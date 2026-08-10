namespace PgAiTrading.Models.Trading;

/// <summary>
/// Maps UI product selection to the product code Zerodha expects on the wire.
/// Equity CNC stays CNC; F&amp;O "CNC" (overnight) is sent as NRML.
/// </summary>
public static class BrokerProductMapper
{
    public static string ToBrokerProduct(string uiProduct, string exchange)
    {
        var product = ProductTypes.NormalizeUi(uiProduct);
        var ex = (exchange ?? string.Empty).Trim().ToUpperInvariant();

        if (product == ProductTypes.Mis)
            return ProductTypes.Mis;

        // UI "CNC" on derivatives = overnight carry → NRML
        if (ExchangeCodes.IsDerivatives(ex))
            return ProductTypes.Nrml;

        return ProductTypes.Cnc;
    }

    public static string DescribeUiProduct(string uiProduct, string exchange)
    {
        var product = ProductTypes.NormalizeUi(uiProduct);
        if (product == ProductTypes.Mis)
            return "MIS";

        return ExchangeCodes.IsDerivatives(exchange) ? "CNC (NRML)" : "CNC";
    }

    public static string ProductHint(string uiProduct, string exchange)
    {
        var product = ProductTypes.NormalizeUi(uiProduct);
        if (product == ProductTypes.Mis)
            return "MIS intraday (auto square-off)";

        return ExchangeCodes.IsDerivatives(exchange)
            ? "CNC → NRML on NFO (carry overnight)"
            : "CNC delivery (equity hold)";
    }
}
