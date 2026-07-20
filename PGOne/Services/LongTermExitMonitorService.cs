using PGOne.Models;

namespace PGOne.Services;

public interface ILongTermExitMonitorService
{
    event Action? Updated;
    IReadOnlyList<LongTermExitRow> Items { get; }
    bool IsLoading { get; }
    bool IsMonitoring { get; }
    string? StatusMessage { get; }
    Task RefreshAsync();
    Task SetMonitoringAsync(bool enabled);
}

public class LongTermExitMonitorService : ILongTermExitMonitorService, IDisposable
{
    private readonly IZerodhaService _zerodha;
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;

    private CancellationTokenSource? _monitorCts;
    private bool _disposed;

    public event Action? Updated;
    public IReadOnlyList<LongTermExitRow> Items { get; private set; } = Array.Empty<LongTermExitRow>();
    public bool IsLoading { get; private set; }
    public bool IsMonitoring { get; private set; }
    public string? StatusMessage { get; private set; }

    public LongTermExitMonitorService(
        IZerodhaService zerodha,
        IMarketDataService marketData,
        ISuperTrendService superTrend)
    {
        _zerodha = zerodha;
        _marketData = marketData;
        _superTrend = superTrend;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        Notify();

        try
        {
            if (!_zerodha.IsConnected)
            {
                Items = Array.Empty<LongTermExitRow>();
                StatusMessage = "Connect to Zerodha to monitor delivery holdings.";
                return;
            }

            var holdings = await _zerodha.GetHoldingsAsync();
            var rows = new List<LongTermExitRow>();

            foreach (var holding in holdings)
            {
                rows.Add(await BuildRowAsync(holding));
            }

            Items = rows
                .OrderByDescending(r => r.IsExitSignal)
                .ThenBy(r => r.Symbol)
                .ToList();

            var alertCount = Items.Count(r => r.IsExitSignal);
            StatusMessage = alertCount > 0
                ? $"{alertCount} holding(s) — daily close below 1D SuperTrend. Review exit."
                : IsMonitoring
                    ? "Monitoring delivery holdings — no exit alerts right now."
                    : null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Long-term monitor failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            Notify();
        }
    }

    public Task SetMonitoringAsync(bool enabled)
    {
        if (enabled == IsMonitoring)
            return Task.CompletedTask;

        IsMonitoring = enabled;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        if (enabled)
        {
            _monitorCts = new CancellationTokenSource();
            _ = MonitorLoopAsync(_monitorCts.Token);
            StatusMessage = "Monitoring delivery holdings — notifications on 1D ST breach (no auto-exit).";
        }
        else
        {
            StatusMessage = "Long-term exit monitoring stopped.";
        }

        Notify();
        return Task.CompletedTask;
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await RefreshAsync();
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Monitoring stopped.
        }
    }

    private async Task<LongTermExitRow> BuildRowAsync(Holding holding)
    {
        var cfg = FrameworkDefaults.LongTerm;
        var instrument = InstrumentMapper.ToZerodhaKey(holding.Symbol, holding.Exchange);
        var daily = await _marketData.GetCandlesAsync(instrument, "1D", 120);

        var row = new LongTermExitRow
        {
            Symbol = holding.Symbol,
            Exchange = holding.Exchange,
            Quantity = holding.Quantity,
            AveragePrice = holding.AveragePrice,
            LastPrice = holding.LastPrice,
            PnL = holding.PnL
        };

        if (daily.Count < cfg.SuperTrendPeriod + 2)
        {
            row.Status = "No data";
            row.Detail = "Not enough daily candles for 1D SuperTrend";
            return row;
        }

        var (close, superTrend, triggered) = EvaluateDailyExit(daily, cfg.SuperTrendPeriod, cfg.SuperTrendMultiplier);
        row.LastClosedPrice = close;
        row.SuperTrendLevel = superTrend;
        row.IsExitSignal = triggered;

        if (triggered)
        {
            row.Status = "Exit alert";
            row.Detail = $"1D close {close:N2} closed below ST({cfg.SuperTrendPeriod},{cfg.SuperTrendMultiplier:0.#}) {superTrend:N2} — review exit";
        }
        else
        {
            row.Status = "Monitoring";
            row.Detail = $"Hold above 1D ST {superTrend:N2} (last close {close:N2})";
        }

        return row;
    }

    private (decimal close, decimal superTrend, bool triggered) EvaluateDailyExit(
        List<Candle> candles,
        int period,
        double multiplier)
    {
        var (_, values) = _superTrend.Calculate(candles, period, multiplier);
        if (values.Count < 2)
            return (0m, 0m, false);

        var closedIndex = candles.Count - 2;
        var offset = candles.Count - values.Count;
        var stIndex = Math.Clamp(closedIndex - offset, 0, values.Count - 1);

        var close = candles[closedIndex].Close;
        var superTrend = values[stIndex];
        return (close, superTrend, close < superTrend);
    }

    private void Notify() => Updated?.Invoke();

    public void Dispose()
    {
        if (_disposed)
            return;

        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _disposed = true;
    }
}
