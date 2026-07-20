using PGOne.Models;

namespace PGOne.Services;

public interface IMarketDataService
{
    bool IsMarketOpen { get; }
    Task<List<Candle>> GetCandlesAsync(string instrument, string interval, int count = 100);
    Task<decimal> GetCurrentPriceAsync(string instrument);
    event Action<string, decimal>? PriceUpdated;
    void StartStreaming();
    void StopStreaming();
}

public class MarketDataService : IMarketDataService
{
    private readonly IZerodhaService _zerodha;
    private readonly ISuperTrendService _superTrend;
    private System.Timers.Timer? _timer;
    private readonly Random _random = new(42);

    public event Action<string, decimal>? PriceUpdated;

    public MarketDataService(IZerodhaService zerodha, ISuperTrendService superTrend)
    {
        _zerodha = zerodha;
        _superTrend = superTrend;
    }

    public bool IsMarketOpen => MarketHours.IsOpen();

    public async Task<List<Candle>> GetCandlesAsync(string instrument, string interval, int count = 100)
    {
        if (_zerodha.IsConnected)
        {
            var historical = await _zerodha.GetHistoricalCandlesAsync(instrument, interval, count);
            if (historical.Count > 0)
                return AttachSuperTrend(historical);
        }

        return AttachSuperTrend(await GenerateDemoCandlesAsync(instrument, interval, count));
    }

    public async Task<decimal> GetCurrentPriceAsync(string instrument)
    {
        if (_zerodha.IsConnected)
        {
            var price = await _zerodha.GetLtpAsync(instrument);
            if (price > 0)
                return price;
        }

        return 25325.40m;
    }

    public void StartStreaming()
    {
        if (!MarketHours.IsOpen())
            return;

        _timer = new System.Timers.Timer(3000);
        _timer.Elapsed += async (_, _) =>
        {
            if (!MarketHours.IsOpen())
                return;

            var price = await _zerodha.GetLtpAsync("NSE:NIFTY 50");
            if (price > 0)
                PriceUpdated?.Invoke("NSE:NIFTY 50", price);
        };
        _timer.Start();
    }

    public void StopStreaming()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }

    private List<Candle> AttachSuperTrend(List<Candle> candles)
    {
        var (_, values) = _superTrend.Calculate(candles, 10, 3.0);
        if (values.Count == 0)
            return candles;

        var startIndex = candles.Count - values.Count;
        for (var i = 0; i < values.Count; i++)
            candles[startIndex + i].SuperTrend = values[i];

        return candles;
    }

    private async Task<List<Candle>> GenerateDemoCandlesAsync(string instrument, string interval, int count)
    {
        var basePrice = await GetCurrentPriceAsync(instrument);
        var candles = new List<Candle>();
        var price = basePrice;
        var now = MarketHours.GetIstNow();

        for (int i = count - 1; i >= 0; i--)
        {
            var change = (decimal)(_random.NextDouble() * 40 - 20);
            var open = price;
            var close = price + change;
            var high = Math.Max(open, close) + (decimal)(_random.NextDouble() * 10);
            var low = Math.Min(open, close) - (decimal)(_random.NextDouble() * 10);

            candles.Add(new Candle
            {
                Timestamp = now.AddMinutes(-i * GetIntervalMinutes(interval)),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = _random.Next(10000, 500000)
            });

            price = close;
        }

        return candles;
    }

    private static int GetIntervalMinutes(string interval) => interval switch
    {
        "5m" => 5,
        "15m" => 15,
        "1H" => 60,
        "1D" => 1440,
        _ => 5
    };
}
