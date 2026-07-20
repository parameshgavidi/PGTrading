using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PGOne.Models;

namespace PGOne.Services;

public interface IZerodhaService
{
    bool IsConnected { get; }
    string? UserId { get; }
    event Action<bool>? ConnectionChanged;
    string GetLoginUrl();
    Task<(bool Success, string Message)> GenerateSessionAsync(string requestToken);
    Task<decimal> GetLtpAsync(string instrument);
    Task<InstrumentQuote?> GetQuoteAsync(string instrument);
    Task<List<Candle>> GetHistoricalCandlesAsync(string instrument, string interval, int count = 100);
    Task<CandleSeriesResult> GetHistoricalCandlesResultAsync(string instrument, string interval, int count = 100);
    Task<Dictionary<string, decimal>> GetQuotesAsync(string[] instruments);
    Task<List<Position>> GetPositionsAsync(string? product = null);
    Task<List<Position>> GetMisPositionsAsync(bool includeClosed = true);
    Task<List<Holding>> GetHoldingsAsync();
    Task<List<Order>> GetOrdersAsync();
    Task<string?> PlaceOrderAsync(string exchange, string tradingsymbol, string transactionType, int quantity, string orderType, decimal? price = null, string product = "MIS");
    Task<IReadOnlyList<string>> GetNseEquitySymbolsAsync();
    void Disconnect();
}

public class ZerodhaService : IZerodhaService
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.kite.trade";
    private const string LoginUrl = "https://kite.zerodha.com/connect/login";

    private List<string>? _nseEquitySymbols;

    public bool IsConnected { get; private set; }
    public string? UserId { get; private set; }
    public event Action<bool>? ConnectionChanged;

    public ZerodhaService(ISettingsService settings)
    {
        _settings = settings;
        _http = new HttpClient();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _settings.LoadAsync();
        if (!string.IsNullOrEmpty(_settings.Settings.AccessToken))
        {
            IsConnected = true;
            ConnectionChanged?.Invoke(true);
        }
    }

    public string GetLoginUrl()
    {
        var apiKey = _settings.Settings.ApiKey;
        return $"{LoginUrl}?v=3&api_key={apiKey}";
    }

    public async Task<(bool Success, string Message)> GenerateSessionAsync(string requestToken)
    {
        await _settings.LoadAsync();

        var apiKey = _settings.Settings.ApiKey?.Trim() ?? string.Empty;
        var apiSecret = _settings.Settings.ApiSecret?.Trim() ?? string.Empty;
        requestToken = requestToken.Trim();

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            return (false, "API Key and API Secret are required. Enter them above and click Save Settings.");

        if (string.IsNullOrEmpty(requestToken))
            return (false, "Request token is empty. Paste the token from the Zerodha redirect URL.");

        try
        {
            var checksum = ComputeSha256(apiKey + requestToken + apiSecret);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["api_key"] = apiKey,
                ["request_token"] = requestToken,
                ["checksum"] = checksum
            });

            var response = await _http.PostAsync($"{BaseUrl}/session/token", content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = TryReadKiteError(json)
                    ?? $"Zerodha rejected the token (HTTP {(int)response.StatusCode}). Generate a fresh request_token and try again.";
                return (false, error);
            }

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            _settings.Settings.AccessToken = data.GetProperty("access_token").GetString() ?? string.Empty;
            UserId = data.GetProperty("user_id").GetString();
            await _settings.SaveSettingsAsync();

            IsConnected = true;
            ConnectionChanged?.Invoke(true);
            return (true, $"Connected as {UserId}");
        }
        catch (Exception ex)
        {
            return (false, $"Connection error: {ex.Message}");
        }
    }

    private static string? TryReadKiteError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var message))
                return message.GetString();
        }
        catch
        {
            // Ignore malformed error payloads.
        }

        return null;
    }

    public async Task<decimal> GetLtpAsync(string instrument)
    {
        var quote = await GetQuoteAsync(instrument);
        return quote?.LastPrice ?? 0;
    }

    public async Task<InstrumentQuote?> GetQuoteAsync(string instrument)
    {
        if (!IsConnected)
            return null;

        try
        {
            var query = $"i={Uri.EscapeDataString(instrument)}";
            var request = CreateRequest(HttpMethod.Get, $"/quote?{query}");
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.GetProperty("data").TryGetProperty(instrument, out var item))
                return null;

            var ohlc = item.GetProperty("ohlc");
            return new InstrumentQuote
            {
                LastPrice = item.GetProperty("last_price").GetDecimal(),
                Open = ohlc.GetProperty("open").GetDecimal(),
                High = ohlc.GetProperty("high").GetDecimal(),
                Low = ohlc.GetProperty("low").GetDecimal(),
                PreviousClose = ohlc.GetProperty("close").GetDecimal()
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<Candle>> GetHistoricalCandlesAsync(string instrument, string interval, int count = 100)
    {
        var result = await GetHistoricalCandlesResultAsync(instrument, interval, count);
        return result.Candles;
    }

    public async Task<CandleSeriesResult> GetHistoricalCandlesResultAsync(string instrument, string interval, int count = 100)
    {
        if (!IsConnected)
            return new CandleSeriesResult { Error = "Not connected to Zerodha." };

        if (!InstrumentTokens.TryGetValue(instrument, out var token))
            return new CandleSeriesResult { Error = $"Unknown instrument: {instrument}" };

        try
        {
            var kiteInterval = MapKiteInterval(interval);
            var (from, to) = GetHistoricalRange(interval, count);
            var fromParam = Uri.EscapeDataString(from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var toParam = Uri.EscapeDataString(to.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            var request = CreateRequest(HttpMethod.Get,
                $"/instruments/historical/{token}/{kiteInterval}?from={fromParam}&to={toParam}&continuous=0&oi=0");
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = TryReadKiteError(json)
                    ?? $"Zerodha historical API failed (HTTP {(int)response.StatusCode}).";
                return new CandleSeriesResult { Error = error };
            }

            using var doc = JsonDocument.Parse(json);
            var candles = new List<Candle>();

            foreach (var row in doc.RootElement.GetProperty("data").GetProperty("candles").EnumerateArray())
            {
                var timestampText = row[0].GetString();
                if (string.IsNullOrEmpty(timestampText))
                    continue;

                candles.Add(new Candle
                {
                    Timestamp = DateTimeOffset.Parse(timestampText, CultureInfo.InvariantCulture).DateTime,
                    Open = row[1].GetDecimal(),
                    High = row[2].GetDecimal(),
                    Low = row[3].GetDecimal(),
                    Close = row[4].GetDecimal(),
                    Volume = row[5].GetInt64()
                });
            }

            if (candles.Count == 0)
                return new CandleSeriesResult { Error = "Zerodha returned no candles for this range." };

            if (candles.Count > count)
                candles = candles[^count..];

            return new CandleSeriesResult { Candles = candles, IsFromZerodha = true };
        }
        catch (Exception ex)
        {
            return new CandleSeriesResult { Error = $"Failed to load Zerodha candles: {ex.Message}" };
        }
    }

    public async Task<Dictionary<string, decimal>> GetQuotesAsync(string[] instruments)
    {
        if (!IsConnected)
            return GetDemoQuotes(instruments);

        try
        {
            var query = string.Join("&", instruments.Select(i => $"i={Uri.EscapeDataString(i)}"));
            var request = CreateRequest(HttpMethod.Get, $"/quote/ltp?{query}");
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return GetDemoQuotes(instruments);

            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, decimal>();
            var data = doc.RootElement.GetProperty("data");

            foreach (var prop in data.EnumerateObject())
                result[prop.Name] = prop.Value.GetProperty("last_price").GetDecimal();

            return result;
        }
        catch
        {
            return GetDemoQuotes(instruments);
        }
    }

    public async Task<List<Position>> GetPositionsAsync(string? product = null)
    {
        if (!IsConnected)
            return new List<Position>();

        try
        {
            var request = CreateRequest(HttpMethod.Get, "/portfolio/positions");
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new List<Position>();

            using var doc = JsonDocument.Parse(json);
            var net = doc.RootElement.GetProperty("data").GetProperty("net");
            var positions = new List<Position>();

            foreach (var item in net.EnumerateArray())
            {
                var quantity = item.GetProperty("quantity").GetInt32();
                if (quantity == 0)
                    continue;

                var itemProduct = item.GetProperty("product").GetString() ?? "";
                if (product is not null
                    && !string.Equals(itemProduct, product, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                positions.Add(new Position
                {
                    Symbol = item.GetProperty("tradingsymbol").GetString() ?? "",
                    Exchange = item.GetProperty("exchange").GetString() ?? "NSE",
                    Product = itemProduct,
                    Quantity = quantity,
                    AveragePrice = item.GetProperty("average_price").GetDecimal(),
                    LastPrice = item.GetProperty("last_price").GetDecimal(),
                    PnL = item.GetProperty("pnl").GetDecimal(),
                    Side = quantity > 0 ? TrendDirection.Buy : TrendDirection.Sell
                });
            }

            return positions;
        }
        catch
        {
            return new List<Position>();
        }
    }

    public async Task<List<Position>> GetMisPositionsAsync(bool includeClosed = true)
    {
        if (!IsConnected)
            return new List<Position>();

        try
        {
            var request = CreateRequest(HttpMethod.Get, "/portfolio/positions");
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new List<Position>();

            using var doc = JsonDocument.Parse(json);
            var day = doc.RootElement.GetProperty("data").GetProperty("day");
            var positions = new List<Position>();

            foreach (var item in day.EnumerateArray())
            {
                var itemProduct = item.GetProperty("product").GetString() ?? "";
                if (!string.Equals(itemProduct, "MIS", StringComparison.OrdinalIgnoreCase))
                    continue;

                var quantity = item.GetProperty("quantity").GetInt32();
                var dayBuyQuantity = item.TryGetProperty("day_buy_quantity", out var dayBuy)
                    ? dayBuy.GetInt32()
                    : 0;
                var daySellQuantity = item.TryGetProperty("day_sell_quantity", out var daySell)
                    ? daySell.GetInt32()
                    : 0;
                var hasDayActivity = dayBuyQuantity > 0 || daySellQuantity > 0;

                if (quantity == 0 && !hasDayActivity)
                    continue;

                var isClosed = quantity == 0 && hasDayActivity;
                if (!includeClosed && isClosed)
                    continue;

                positions.Add(new Position
                {
                    Symbol = item.GetProperty("tradingsymbol").GetString() ?? "",
                    Exchange = item.GetProperty("exchange").GetString() ?? "NSE",
                    Product = itemProduct,
                    Quantity = quantity,
                    AveragePrice = item.GetProperty("average_price").GetDecimal(),
                    LastPrice = item.GetProperty("last_price").GetDecimal(),
                    PnL = item.GetProperty("pnl").GetDecimal(),
                    Side = quantity > 0
                        ? TrendDirection.Buy
                        : quantity < 0
                            ? TrendDirection.Sell
                            : dayBuyQuantity >= daySellQuantity
                                ? TrendDirection.Buy
                                : TrendDirection.Sell,
                    IsClosed = isClosed,
                    DayBuyQuantity = dayBuyQuantity,
                    DaySellQuantity = daySellQuantity
                });
            }

            return positions
                .OrderBy(p => p.IsClosed)
                .ThenBy(p => p.Symbol)
                .ToList();
        }
        catch
        {
            return new List<Position>();
        }
    }

    public async Task<List<Holding>> GetHoldingsAsync()
    {
        if (!IsConnected)
            return GetDemoHoldings();

        try
        {
            var request = CreateRequest(HttpMethod.Get, "/portfolio/holdings");
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = TryReadKiteError(json)
                    ?? $"Holdings API failed (HTTP {(int)response.StatusCode}).";
                throw new InvalidOperationException(error);
            }

            using var doc = JsonDocument.Parse(json);
            var holdings = new List<Holding>();

            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var quantity = item.GetProperty("quantity").GetInt32();
                var t1Quantity = item.TryGetProperty("t1_quantity", out var t1) ? t1.GetInt32() : 0;
                var collateralQuantity = item.TryGetProperty("collateral_quantity", out var collateral)
                    ? collateral.GetInt32()
                    : 0;
                var effectiveQuantity = quantity + t1Quantity + collateralQuantity;
                if (effectiveQuantity == 0)
                    continue;

                var averagePrice = item.GetProperty("average_price").GetDecimal();
                var lastPrice = item.GetProperty("last_price").GetDecimal();
                var dayChangePercent = item.TryGetProperty("day_change_percentage", out var dayChangePct)
                    ? dayChangePct.GetDecimal()
                    : 0m;

                holdings.Add(new Holding
                {
                    Symbol = item.GetProperty("tradingsymbol").GetString() ?? "",
                    Exchange = item.GetProperty("exchange").GetString() ?? "NSE",
                    Quantity = effectiveQuantity,
                    AveragePrice = averagePrice,
                    LastPrice = lastPrice,
                    DayChangePercent = dayChangePercent,
                    PnL = item.TryGetProperty("pnl", out var pnl) ? pnl.GetDecimal() : 0m
                });
            }

            return holdings;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load holdings: {ex.Message}", ex);
        }
    }

    public async Task<List<Order>> GetOrdersAsync()
    {
        if (!IsConnected)
            return new List<Order>();

        try
        {
            var request = CreateRequest(HttpMethod.Get, "/orders");
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new List<Order>();

            using var doc = JsonDocument.Parse(json);
            var orders = new List<Order>();

            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                orders.Add(new Order
                {
                    OrderId = item.GetProperty("order_id").GetString() ?? "",
                    Symbol = item.GetProperty("tradingsymbol").GetString() ?? "",
                    TransactionType = item.GetProperty("transaction_type").GetString() ?? "",
                    Quantity = item.GetProperty("quantity").GetInt32(),
                    Price = item.GetProperty("price").GetDecimal(),
                    Status = item.GetProperty("status").GetString() ?? "",
                    OrderTime = DateTime.Parse(item.GetProperty("order_timestamp").GetString() ?? DateTime.Now.ToString())
                });
            }

            return orders;
        }
        catch
        {
            return new List<Order>();
        }
    }

    public async Task<string?> PlaceOrderAsync(
        string exchange,
        string tradingsymbol,
        string transactionType,
        int quantity,
        string orderType,
        decimal? price = null,
        string product = "MIS")
    {
        if (!IsConnected)
            return null;

        var formData = new Dictionary<string, string>
        {
            ["exchange"] = exchange,
            ["tradingsymbol"] = tradingsymbol,
            ["transaction_type"] = transactionType,
            ["quantity"] = quantity.ToString(),
            ["order_type"] = orderType,
            ["product"] = product,
            ["validity"] = "DAY"
        };

        if (price.HasValue)
            formData["price"] = price.Value.ToString("F2");

        var request = CreateRequest(HttpMethod.Post, "/orders/regular");
        request.Content = new FormUrlEncodedContent(formData);

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").GetProperty("order_id").GetString();
    }

    public async Task<IReadOnlyList<string>> GetNseEquitySymbolsAsync()
    {
        if (_nseEquitySymbols is not null)
            return _nseEquitySymbols;

        if (!IsConnected)
            return NiftyConstituents.ScanUniverse.ToList();

        try
        {
            var request = CreateRequest(HttpMethod.Get, "/instruments");
            var response = await _http.SendAsync(request);
            var csv = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return NiftyConstituents.ScanUniverse.ToList();

            var symbols = new List<string>();
            using var reader = new StringReader(csv);
            _ = reader.ReadLine();

            while (reader.ReadLine() is { } line)
            {
                var parts = line.Split(',');
                if (parts.Length < 12)
                    continue;

                if (parts[9] != "EQ" || parts[11] != "NSE")
                    continue;

                var symbol = parts[2].Trim('"');
                if (string.IsNullOrWhiteSpace(symbol) || symbol.Contains('-', StringComparison.Ordinal))
                    continue;

                symbols.Add(symbol);
            }

            _nseEquitySymbols = symbols.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            return _nseEquitySymbols;
        }
        catch
        {
            return NiftyConstituents.ScanUniverse.ToList();
        }
    }

    public void Disconnect()
    {
        _settings.Settings.AccessToken = string.Empty;
        _ = _settings.SaveSettingsAsync();
        IsConnected = false;
        UserId = null;
        ConnectionChanged?.Invoke(false);
    }

    private static readonly Dictionary<string, int> InstrumentTokens = new()
    {
        ["NSE:NIFTY 50"] = 256265,
        ["NSE:NIFTY BANK"] = 260105,
        ["NSE:RELIANCE"] = 738561,
        ["NSE:INFY"] = 408065,
        ["NSE:TCS"] = 2953217,
        ["NSE:SBIN"] = 779521,
        ["NSE:HDFCBANK"] = 341249
    };

    private static string MapKiteInterval(string interval) => interval switch
    {
        "5m" => "5minute",
        "15m" => "15minute",
        "1H" => "60minute",
        "1D" => "day",
        _ => "5minute"
    };

    private static (DateTime From, DateTime To) GetHistoricalRange(string interval, int count)
    {
        var ist = MarketHours.GetIstNow();
        var to = MarketHours.IsOpen(ist) ? ist : MarketHours.GetLastSessionClose(ist);

        // ~6.25 trading hours per session. Convert bar count into enough calendar
        // days (with slack for weekends/holidays) so Kite returns `count` bars.
        var barsPerDay = interval switch
        {
            "5m" => 75,
            "15m" => 25,
            "1H" => 7,
            _ => 1
        };

        var from = interval == "1D"
            ? to.AddDays(-(int)(count * 1.6) - 5)
            : to.AddDays(-(int)Math.Ceiling(count / (double)barsPerDay * 1.6) - 3);

        while (from.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            from = from.AddDays(-1);

        from = from.Date.Add(MarketHours.OpenTime);
        if (to.TimeOfDay > MarketHours.CloseTime || !MarketHours.IsOpen(to))
            to = to.Date.Add(MarketHours.CloseTime);

        return (from, to);
    }

    private static int GetIntervalMinutes(string interval) => interval switch
    {
        "5m" => 5,
        "15m" => 15,
        "1H" => 60,
        "1D" => 1440,
        _ => 5
    };

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}{endpoint}");
        var apiKey = _settings.Settings.ApiKey;
        var accessToken = _settings.Settings.AccessToken;
        request.Headers.Add("X-Kite-Version", "3");
        request.Headers.Authorization = new AuthenticationHeaderValue("token", $"{apiKey}:{accessToken}");
        return request;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static List<Holding> GetDemoHoldings() =>
    [
        new Holding { Symbol = "RELIANCE", Exchange = "NSE", Quantity = 10, AveragePrice = 2700m, LastPrice = 2845.50m, DayChangePercent = 1.24m, PnL = 1455m },
        new Holding { Symbol = "INFY", Exchange = "NSE", Quantity = 25, AveragePrice = 1750m, LastPrice = 1820.30m, DayChangePercent = -0.52m, PnL = 1757.50m },
        new Holding { Symbol = "HDFCBANK", Exchange = "NSE", Quantity = 15, AveragePrice = 1720m, LastPrice = 1680.45m, DayChangePercent = -0.81m, PnL = -593.25m },
        new Holding { Symbol = "TCS", Exchange = "NSE", Quantity = 5, AveragePrice = 4200m, LastPrice = 4125.80m, DayChangePercent = 0.31m, PnL = -371m },
        new Holding { Symbol = "SBIN", Exchange = "NSE", Quantity = 20, AveragePrice = 760m, LastPrice = 785.20m, DayChangePercent = 0.95m, PnL = 504m }
    ];

    private static Dictionary<string, decimal> GetDemoQuotes(string[] instruments)
    {
        var demo = new Dictionary<string, decimal>
        {
            ["NSE:NIFTY 50"] = 25325.40m,
            ["NSE:NIFTY BANK"] = 52100.75m,
            ["NSE:RELIANCE"] = 2845.50m,
            ["NSE:INFY"] = 1820.30m,
            ["NSE:TCS"] = 4125.80m,
            ["NSE:SBIN"] = 785.20m,
            ["NSE:HDFCBANK"] = 1680.45m
        };

        return instruments.ToDictionary(i => i, i => demo.GetValueOrDefault(i, 1000m));
    }
}
