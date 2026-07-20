using PGOne.Models;

namespace PGOne.Services;

public interface IMarketDataService
{
    bool IsMarketOpen { get; }
    Task<List<Candle>> GetCandlesAsync(string instrument, string interval, int count = 100);
    Task<CandleSeriesResult> GetCandlesResultAsync(string instrument, string interval, int count = 100);
    Task<decimal> GetCurrentPriceAsync(string instrument);
    Task<InstrumentQuote?> GetQuoteAsync(string instrument);
    event Action<string, decimal>? PriceUpdated;
    void StartStreaming(string? instrument = null);
    void StopStreaming();
}

public class MarketDataService : IMarketDataService
{
    private readonly IZerodhaService _zerodha;
    private readonly ISuperTrendService _superTrend;
    private readonly IIndicatorService _indicators;
    private readonly ISettingsService _settings;
    private System.Timers.Timer? _timer;
    private string _streamingInstrument = "NSE:NIFTY 50";
    private readonly Random _random = new(42);

    public event Action<string, decimal>? PriceUpdated;

    public MarketDataService(
        IZerodhaService zerodha,
        ISuperTrendService superTrend,
        IIndicatorService indicators,
        ISettingsService settings)
    {
        _zerodha = zerodha;
        _superTrend = superTrend;
        _indicators = indicators;
        _settings = settings;
    }

    public bool IsMarketOpen => MarketHours.IsOpen();

    public async Task<List<Candle>> GetCandlesAsync(string instrument, string interval, int count = 100)
    {
        var result = await GetCandlesResultAsync(instrument, interval, count);
        return result.Candles;
    }

    // Extra history fed to indicators so SuperTrend/ATR converge to the same
    // state as TradingView (which computes over full history) before we trim
    // to the visible window. SuperTrend is path-dependent, so warmup matters.
    private const int WarmupBars = 300;

    public async Task<CandleSeriesResult> GetCandlesResultAsync(string instrument, string interval, int count = 100)
    {
        if (_zerodha.IsConnected)
        {
            var result = await _zerodha.GetHistoricalCandlesResultAsync(instrument, interval, count + WarmupBars);
            if (result.IsFromZerodha && result.Candles.Count > 0)
            {
                var withIndicators = AttachIndicators(result.Candles, interval);
                var display = withIndicators.Count > count
                    ? withIndicators.GetRange(withIndicators.Count - count, count)
                    : withIndicators;

                return new CandleSeriesResult
                {
                    Candles = display,
                    IsFromZerodha = true
                };
            }

            if (!string.IsNullOrEmpty(result.Error))
                return result;
        }

        return new CandleSeriesResult
        {
            Candles = AttachIndicators(await GenerateDemoCandlesAsync(instrument, interval, count), interval),
            IsFromZerodha = false,
            Error = _zerodha.IsConnected ? "Using demo candles because Zerodha historical data was unavailable." : null
        };
    }

    public async Task<decimal> GetCurrentPriceAsync(string instrument)
    {
        if (_zerodha.IsConnected)
        {
            var quote = await _zerodha.GetQuoteAsync(instrument);
            if (quote is { LastPrice: > 0 })
                return quote.LastPrice;

            var price = await _zerodha.GetLtpAsync(instrument);
            if (price > 0)
                return price;
        }

        return 25325.40m;
    }

    public Task<InstrumentQuote?> GetQuoteAsync(string instrument) =>
        _zerodha.GetQuoteAsync(instrument);

    public void StartStreaming(string? instrument = null)
    {
        if (!string.IsNullOrWhiteSpace(instrument))
            _streamingInstrument = instrument;

        if (!MarketHours.IsOpen())
            return;

        _timer?.Stop();
        _timer?.Dispose();

        _timer = new System.Timers.Timer(3000);
        _timer.Elapsed += async (_, _) =>
        {
            if (!MarketHours.IsOpen())
                return;

            var price = await _zerodha.GetLtpAsync(_streamingInstrument);
            if (price > 0)
                PriceUpdated?.Invoke(_streamingInstrument, price);
        };
        _timer.Start();
    }

    public void StopStreaming()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }

    private List<Candle> AttachIndicators(List<Candle> candles, string interval)
    {
        AttachSuperTrend(candles);

        // Keltner Channels + VWAP are used for the 5m range-bound playbook.
        if (interval == "5m")
        {
            var cfg = _settings.Strategy;
            _indicators.ApplyKeltner(candles, cfg.KeltnerEmaLength, cfg.KeltnerAtrLength,
                cfg.KeltnerMultiplierInner, cfg.KeltnerMultiplierOuter);
            _indicators.ApplyVwap(candles);
        }

        return candles;
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
