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
    Task<bool> GenerateSessionAsync(string requestToken);
    Task<decimal> GetLtpAsync(string instrument);
    Task<Dictionary<string, decimal>> GetQuotesAsync(string[] instruments);
    Task<List<Position>> GetPositionsAsync();
    Task<List<Order>> GetOrdersAsync();
    Task<string?> PlaceOrderAsync(string exchange, string tradingsymbol, string transactionType, int quantity, string orderType, decimal? price = null);
    void Disconnect();
}

public class ZerodhaService : IZerodhaService
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.kite.trade";
    private const string LoginUrl = "https://kite.zerodha.com/connect/login";

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

    public async Task<bool> GenerateSessionAsync(string requestToken)
    {
        var apiKey = _settings.Settings.ApiKey;
        var apiSecret = _settings.Settings.ApiSecret;

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            return false;

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
            return false;

        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        _settings.Settings.AccessToken = data.GetProperty("access_token").GetString() ?? string.Empty;
        UserId = data.GetProperty("user_id").GetString();
        await _settings.SaveSettingsAsync();

        IsConnected = true;
        ConnectionChanged?.Invoke(true);
        return true;
    }

    public async Task<decimal> GetLtpAsync(string instrument)
    {
        var quotes = await GetQuotesAsync(new[] { instrument });
        return quotes.GetValueOrDefault(instrument, 0);
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

    public async Task<List<Position>> GetPositionsAsync()
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
                if (item.GetProperty("quantity").GetInt32() == 0) continue;

                positions.Add(new Position
                {
                    Symbol = item.GetProperty("tradingsymbol").GetString() ?? "",
                    Instrument = item.GetProperty("exchange").GetString() ?? "",
                    Quantity = item.GetProperty("quantity").GetInt32(),
                    AveragePrice = item.GetProperty("average_price").GetDecimal(),
                    LastPrice = item.GetProperty("last_price").GetDecimal(),
                    PnL = item.GetProperty("pnl").GetDecimal(),
                    Side = item.GetProperty("quantity").GetInt32() > 0 ? TrendDirection.Buy : TrendDirection.Sell
                });
            }

            return positions;
        }
        catch
        {
            return new List<Position>();
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

    public async Task<string?> PlaceOrderAsync(string exchange, string tradingsymbol, string transactionType, int quantity, string orderType, decimal? price = null)
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
            ["product"] = "MIS",
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

    public void Disconnect()
    {
        _settings.Settings.AccessToken = string.Empty;
        _ = _settings.SaveSettingsAsync();
        IsConnected = false;
        UserId = null;
        ConnectionChanged?.Invoke(false);
    }

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
