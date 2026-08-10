namespace PgAiTrading.Services;

public static class InstrumentMapper
{
    public static string ToZerodhaKey(string symbol, string exchange = "NSE")
    {
        if (symbol.Contains(':'))
            return symbol;

        return symbol.ToUpper() switch
        {
            "NIFTY" => "NSE:NIFTY 50",
            "BANKNIFTY" => "NSE:NIFTY BANK",
            "SENSEX" => "BSE:SENSEX",
            "RELIANCE" => "NSE:RELIANCE",
            "INFY" => "NSE:INFY",
            "TCS" => "NSE:TCS",
            "SBIN" => "NSE:SBIN",
            "HDFCBANK" => "NSE:HDFCBANK",
            _ => $"{exchange.ToUpperInvariant()}:{symbol.ToUpperInvariant()}"
        };
    }

    public static string ToDisplayName(string symbol) => symbol.ToUpper() switch
    {
        "NIFTY" => "NIFTY 50",
        "BANKNIFTY" => "NIFTY BANK",
        "SENSEX" => "SENSEX",
        _ => symbol
    };

    public static string ToIndexShortName(string symbol) => symbol.ToUpper() switch
    {
        "NIFTY" => "NIFTY 50",
        "BANKNIFTY" => "BANK",
        "SENSEX" => "SENSEX",
        _ => ToDisplayName(symbol)
    };

    public static string FromZerodhaKey(string instrument) => instrument.ToUpper() switch
    {
        "NSE:NIFTY 50" => "NIFTY",
        "NSE:NIFTY BANK" => "BANKNIFTY",
        "BSE:SENSEX" => "SENSEX",
        _ => instrument.Contains(':') ? instrument.Split(':', 2)[1] : instrument
    };

    public static bool IsIndexSymbol(string instrumentOrSymbol)
    {
        var symbol = instrumentOrSymbol.Contains(':')
            ? FromZerodhaKey(instrumentOrSymbol)
            : instrumentOrSymbol;

        return symbol.ToUpperInvariant() is "NIFTY" or "BANKNIFTY" or "SENSEX";
    }

    public static string ResolveStInstrument(string symbol, string exchange)
    {
        if (!exchange.Equals("NFO", StringComparison.OrdinalIgnoreCase))
            return ToZerodhaKey(symbol, exchange);

        if (symbol.StartsWith("BANKNIFTY", StringComparison.OrdinalIgnoreCase))
            return "NSE:NIFTY BANK";
        if (symbol.StartsWith("FINNIFTY", StringComparison.OrdinalIgnoreCase))
            return "NSE:NIFTY FIN SERVICE";
        if (symbol.StartsWith("MIDCPNIFTY", StringComparison.OrdinalIgnoreCase))
            return "NSE:NIFTY MID SELECT";
        if (symbol.StartsWith("NIFTY", StringComparison.OrdinalIgnoreCase))
            return "NSE:NIFTY 50";

        return ToZerodhaKey(symbol, exchange);
    }
}
