namespace PGOneTrade.Models;

public static class NiftyConstituents
{
    /// <summary>Top Nifty 50 stocks by approximate index weight (highest first).</summary>
    public static IReadOnlyList<string> TopWeightage { get; } =
    [
        "HDFCBANK", "RELIANCE", "ICICIBANK", "INFY", "ITC", "LT", "TCS",
        "AXISBANK", "KOTAKBANK", "SBIN", "BHARTIARTL", "BAJFINANCE",
        "HINDUNILVR", "ASIANPAINT", "MARUTI", "HCLTECH", "SUNPHARMA",
        "TITAN", "NTPC", "M&M", "ULTRACEMCO", "WIPRO", "POWERGRID",
        "BAJAJFINSV", "NESTLEIND", "ADANIENT", "JSWSTEEL", "TATAMOTORS",
        "ONGC", "COALINDIA", "TECHM", "TATASTEEL", "INDUSINDBK", "DIVISLAB",
        "CIPLA", "DRREDDY", "APOLLOHOSP", "EICHERMOT", "GRASIM", "HEROMOTOCO",
        "BRITANNIA", "HINDALCO", "BPCL", "SBILIFE", "ADANIPORTS", "TRENT",
        "SHRIRAMFIN", "BEL", "JIOFIN", "ETERNAL"
    ];

    /// <summary>Top 10 Nifty 50 stocks by approximate index weight.</summary>
    public static IReadOnlyList<string> Top10Weightage { get; } =
    [
        "HDFCBANK", "RELIANCE", "ICICIBANK", "INFY", "ITC", "LT", "TCS",
        "AXISBANK", "KOTAKBANK", "SBIN"
    ];

    /// <summary>Key indices shown on the dashboard watchlist.</summary>
    public static IReadOnlyList<string> DashboardIndices { get; } =
        ["NIFTY", "BANKNIFTY", "SENSEX"];

    /// <summary>Dashboard watchlist: indices plus top 10 weighted stocks.</summary>
    public static IReadOnlyList<string> DashboardWatchlist { get; } =
        DashboardIndices
            .Concat(Top10Weightage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Full top-weight stock list for the Watchlist page.</summary>
    public static IReadOnlyList<string> FullTopWeightageWatchlist { get; } = TopWeightage;

    /// <summary>Liquid NSE equities used for intraday framework scan (Nifty-heavy universe).</summary>
    public static IReadOnlyList<string> ScanUniverse { get; } =
    [
        "RELIANCE", "TCS", "HDFCBANK", "ICICIBANK", "INFY", "HINDUNILVR", "ITC",
        "SBIN", "BHARTIARTL", "KOTAKBANK", "LT", "AXISBANK", "BAJFINANCE",
        "ASIANPAINT", "MARUTI", "HCLTECH", "SUNPHARMA", "TITAN", "NTPC", "M&M",
        "ULTRACEMCO", "WIPRO", "POWERGRID", "BAJAJFINSV", "NESTLEIND", "ADANIENT",
        "JSWSTEEL", "TATAMOTORS", "ONGC", "COALINDIA", "TECHM", "TATASTEEL",
        "INDUSINDBK", "DIVISLAB", "CIPLA", "DRREDDY", "APOLLOHOSP", "EICHERMOT",
        "GRASIM", "HEROMOTOCO", "BRITANNIA", "HINDALCO", "BPCL", "SBILIFE",
        "ADANIPORTS", "TRENT", "SHRIRAMFIN", "BEL", "JIOFIN", "ETERNAL",
        "VEDL", "DLF", "GAIL", "IOC", "PIDILITIND", "SIEMENS", "AMBUJACEM",
        "HAL", "BANKBARODA", "PNB", "CANBK", "UNIONBANK", "IDFCFIRSTB",
        "FEDERALBNK", "AUBANK", "BANDHANBNK", "CHOLAFIN", "MUTHOOTFIN",
        "PFC", "RECLTD", "IRFC", "LODHA", "VBL", "DABUR", "GODREJCP",
        "MARICO", "COLPAL", "TORNTPHARM", "LUPIN", "AUROPHARMA", "BIOCON",
        "ALKEM", "ZYDUSLIFE", "MAXHEALTH", "POLYCAB", "HAVELLS", "VOLTAS",
        "CROMPTON", "ABB", "BOSCHLTD", "MOTHERSON", "BHARATFORG", "TVSMOTOR",
        "ASHOKLEY", "ESCORTS", "EXIDEIND", "MRF", "PAGEIND", "PIIND", "SRF",
        "UPL", "DEEPAKNTR", "ATGL", "IGL", "MGL", "PETRONET", "CONCOR",
        "IRCTC", "INDIGO", "NAUKRI", "PAYTM", "ZOMATO", "POLICYBZR"
    ];
}
