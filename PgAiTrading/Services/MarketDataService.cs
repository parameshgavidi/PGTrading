using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IMarketDataService
{
    bool IsMarketOpen { get; }
    Task<List<Candle>> GetCandlesAsync(string instrument, string interval, int count = 100);
    Task<CandleSeriesResult> GetCandlesResultAsync(string instrument, string interval, int count = 100);
    /// <summary>Attach SuperTrend, VWAP, EMA, Keltner, etc. for chart overlays.</summary>
    void ApplyChartIndicators(List<Candle> candles, string interval);
    /// <summary>
    /// 5m candles with real volume for footprint — uses nearest index future when index volume is zero.
    /// </summary>
    Task<(List<Candle> Candles, string VolumeSource, string? FuturesSymbol)> GetFootprintCandlesAsync(
        string instrument,
        IReadOnlyList<Candle> candles5M);
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
    private readonly IChartPatternService _patterns;
    private readonly ISettingsService _settings;
    private System.Timers.Timer? _timer;
    private string _streamingInstrument = "NSE:NIFTY 50";
    private readonly Random _random = new(42);

    public event Action<string, decimal>? PriceUpdated;

    public MarketDataService(
        IZerodhaService zerodha,
        ISuperTrendService superTrend,
        IIndicatorService indicators,
        IChartPatternService patterns,
        ISettingsService settings)
    {
        _zerodha = zerodha;
        _superTrend = superTrend;
        _indicators = indicators;
        _patterns = patterns;
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

    public async Task<(List<Candle> Candles, string VolumeSource, string? FuturesSymbol)> GetFootprintCandlesAsync(
        string instrument,
        IReadOnlyList<Candle> candles5M)
    {
        if (candles5M.Count == 0)
            return ([], "none", null);

        if (CandleVolumeMerger.HasTradeableVolume(candles5M))
            return (CandleVolumeMerger.CopyWithVolumeFrom(candles5M, candles5M), "equity", null);

        if (!InstrumentMapper.IsIndexSymbol(instrument))
            return (CandleVolumeMerger.CopyWithVolumeFrom(candles5M, candles5M), "range_proxy", null);

        var underlying = InstrumentMapper.FromZerodhaKey(instrument);
        var futureKey = await _zerodha.ResolveNearestFutureKeyAsync(underlying);
        if (futureKey is null)
            return (CandleVolumeMerger.CopyWithVolumeFrom(candles5M, candles5M), "range_proxy", null);

        var futuresSymbol = futureKey.Contains(':') ? futureKey.Split(':', 2)[1] : futureKey;
        var futureCandles = await _zerodha.GetHistoricalCandlesAsync(futureKey, "5m", candles5M.Count + 30);
        if (!CandleVolumeMerger.HasTradeableVolume(futureCandles))
            return (CandleVolumeMerger.CopyWithVolumeFrom(candles5M, candles5M), "range_proxy", null);

        // Footprint uses nearest index future OHLCV (price + volume), not index with merged volume.
        var futuresBars = CandleVolumeMerger.SelectFuturesBarsMatchingIndex(candles5M, futureCandles);
        if (futuresBars.Count < 10 || !CandleVolumeMerger.HasTradeableVolume(futuresBars))
            return (CandleVolumeMerger.CopyWithVolumeFrom(candles5M, candles5M), "range_proxy", null);

        return (futuresBars, "futures", futuresSymbol);
    }

    public async Task<CandleSeriesResult> GetCandlesResultAsync(string instrument, string interval, int count = 100)
    {
        if (interval == "1W")
            return await GetWeeklyCandlesResultAsync(instrument, count);

        if (_zerodha.IsConnected)
        {
            var result = await _zerodha.GetHistoricalCandlesResultAsync(instrument, interval, count + WarmupBars);
            if (result.IsFromZerodha && result.Candles.Count > 0)
            {
                var withIndicators = AttachIndicators(result.Candles, interval);
                var display = TrimToDisplayCount(withIndicators, count);

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

    private async Task<CandleSeriesResult> GetWeeklyCandlesResultAsync(string instrument, int count)
    {
        var dailyBarsNeeded = (count + WarmupBars) * 7;

        if (_zerodha.IsConnected)
        {
            var dailyResult = await _zerodha.GetHistoricalCandlesResultAsync(instrument, "1D", dailyBarsNeeded);
            if (dailyResult.IsFromZerodha && dailyResult.Candles.Count > 0)
            {
                var weekly = CandleAggregator.ToWeekly(dailyResult.Candles);
                var weeklyWithIndicators = AttachIndicators(weekly, "1W");
                var weeklyDisplay = TrimToDisplayCount(weeklyWithIndicators, count);

                return new CandleSeriesResult
                {
                    Candles = weeklyDisplay,
                    IsFromZerodha = true
                };
            }

            if (!string.IsNullOrEmpty(dailyResult.Error))
                return dailyResult;
        }

        var demoDaily = await GenerateDemoCandlesAsync(instrument, "1D", dailyBarsNeeded);
        var demoWeekly = CandleAggregator.ToWeekly(demoDaily);
        var demoWeeklyWithIndicators = AttachIndicators(demoWeekly, "1W");
        var demoWeeklyDisplay = TrimToDisplayCount(demoWeeklyWithIndicators, count);

        return new CandleSeriesResult
        {
            Candles = demoWeeklyDisplay,
            IsFromZerodha = false,
            Error = _zerodha.IsConnected ? "Using demo weekly candles because Zerodha daily data was unavailable." : null
        };
    }

    private static List<Candle> TrimToDisplayCount(List<Candle> candles, int count) =>
        candles.Count > count ? candles.GetRange(candles.Count - count, count) : candles;

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

    public void ApplyChartIndicators(List<Candle> candles, string interval)
    {
        if (candles.Count == 0)
            return;

        AttachIndicators(candles, interval);
    }

    private List<Candle> AttachIndicators(List<Candle> candles, string interval)
    {
        AttachSuperTrendValues(candles, 10, 3.0, (c, v) => c.SuperTrend = v);

        if (interval is "1m" or "5m" or "15m")
            AttachSuperTrendValues(
                candles,
                TrailingStopDefaults.Period,
                TrailingStopDefaults.Multiplier,
                (c, v) => c.SuperTrendEntry = v);

        // Keltner on 1m and 5m; VWAP + EMAs on all chart timeframes so overlay buttons always work.
        if (interval is "1m" or "5m")
        {
            var cfg = _settings.Strategy;
            _indicators.ApplyKeltner(candles, cfg.KeltnerEmaLength, cfg.KeltnerAtrLength,
                cfg.KeltnerMultiplierInner, cfg.KeltnerMultiplierOuter);
        }

        _indicators.ApplyVwap(candles);
        _indicators.ApplyChartEmas(candles);

        _patterns.ApplyPatterns(candles);
        return candles;
    }

    private void AttachSuperTrendValues(
        List<Candle> candles,
        int period,
        double multiplier,
        Action<Candle, decimal> assign)
    {
        var (_, values) = _superTrend.Calculate(candles, period, multiplier);
        if (values.Count == 0)
            return;

        var startIndex = candles.Count - values.Count;
        for (var i = 0; i < values.Count; i++)
            assign(candles[startIndex + i], values[i]);
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
        "1m" => 1,
        "5m" => 5,
        "15m" => 15,
        "1H" => 60,
        "1D" => 1440,
        "1W" => 10080,
        _ => 5
    };
}
