using PGOne.Models;

namespace PGOne.Services;

public interface IMarketDataService
{
    Task<List<Candle>> GetCandlesAsync(string instrument, string interval, int count = 100);
    Task<decimal> GetCurrentPriceAsync(string instrument);
    event Action<string, decimal>? PriceUpdated;
    void StartStreaming();
    void StopStreaming();
}

public class MarketDataService : IMarketDataService
{
    private readonly IZerodhaService _zerodha;
    private System.Timers.Timer? _timer;
    private readonly Random _random = new(42);

    public event Action<string, decimal>? PriceUpdated;

    public MarketDataService(IZerodhaService zerodha)
    {
        _zerodha = zerodha;
    }

    public async Task<List<Candle>> GetCandlesAsync(string instrument, string interval, int count = 100)
    {
        var basePrice = await GetCurrentPriceAsync(instrument);
        var candles = new List<Candle>();
        var price = basePrice;
        var now = DateTime.Now;

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

    public async Task<decimal> GetCurrentPriceAsync(string instrument)
    {
        return await _zerodha.GetLtpAsync(instrument);
    }

    public void StartStreaming()
    {
        _timer = new System.Timers.Timer(3000);
        _timer.Elapsed += async (_, _) =>
        {
            var price = await _zerodha.GetLtpAsync("NSE:NIFTY 50");
            var jitter = (decimal)(_random.NextDouble() * 4 - 2);
            PriceUpdated?.Invoke("NSE:NIFTY 50", price + jitter);
        };
        _timer.Start();
    }

    public void StopStreaming()
    {
        _timer?.Stop();
        _timer?.Dispose();
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
