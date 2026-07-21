using PGOne.Models;

namespace PGOne.Services;

public interface ITrailingStopLossService
{
    event Action? Updated;
    List<TrailingStopRow> Items { get; }
    bool IsLoading { get; }
    bool IsMonitoring { get; }
    string? StatusMessage { get; }
    Task RefreshAsync();
    Task SetMonitoringAsync(bool enabled);
}

public class TrailingStopLossService : ITrailingStopLossService, IDisposable
{
    private readonly IZerodhaService _zerodha;
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;

    private readonly HashSet<string> _exitedKeys = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _monitorCts;
    private bool _disposed;

    public event Action? Updated;
    public List<TrailingStopRow> Items { get; private set; } = new();
    public bool IsLoading { get; private set; }
    public bool IsMonitoring { get; private set; }
    public string? StatusMessage { get; private set; }

    public TrailingStopLossService(
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
        StatusMessage = null;
        Notify();

        try
        {
            if (!_zerodha.IsConnected)
            {
                Items = new List<TrailingStopRow>();
                StatusMessage = "Connect to Zerodha to monitor MIS positions.";
                return;
            }

            var positions = await _zerodha.GetMisPositionsAsync(includeClosed: false);
            var rows = new List<TrailingStopRow>();

            foreach (var position in positions.Where(p => p.Quantity != 0))
            {
                var row = await BuildRowAsync(position);
                if (row.IsTriggered && IsMonitoring && !row.ExitPlaced)
                    await TryExitPositionAsync(position, row);

                rows.Add(row);
            }

            Items = rows
                .OrderByDescending(r => r.IsTriggered)
                .ThenBy(r => r.Symbol)
                .ToList();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Trailing stop refresh failed: {ex.Message}";
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
            _exitedKeys.Clear();
            _monitorCts = new CancellationTokenSource();
            _ = MonitorLoopAsync(_monitorCts.Token);
            StatusMessage = "Auto-exit monitoring active — exits on 5m candle close vs ST(7,2.5).";
        }
        else
        {
            StatusMessage = "Auto-exit monitoring stopped.";
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
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Monitoring stopped.
        }
    }

    private async Task<TrailingStopRow> BuildRowAsync(Position position)
    {
        var instrument = InstrumentMapper.ToZerodhaKey(position.Symbol, position.Exchange);
        var candles = await _marketData.GetCandlesAsync(
            instrument,
            TrailingStopDefaults.Interval,
            120);

        var isLong = position.Quantity > 0;
        var side = isLong ? TrendDirection.Buy : TrendDirection.Sell;
        var key = PositionKey(position);

        var row = new TrailingStopRow
        {
            Symbol = position.Symbol,
            Exchange = position.Exchange,
            Quantity = Math.Abs(position.Quantity),
            Side = side,
            AveragePrice = position.AveragePrice,
            LastPrice = position.LastPrice,
            PnL = position.PnL,
            ExitPlaced = _exitedKeys.Contains(key)
        };

        if (candles.Count < TrailingStopDefaults.Period + 2)
        {
            row.Status = "No data";
            row.Detail = "Not enough 5m candles for ST(7,2.5)";
            return row;
        }

        var (close, superTrend, triggered) = EvaluateTrailingStop(candles, isLong);
        row.LastClosedPrice = close;
        row.SuperTrendLevel = superTrend;
        row.IsTriggered = triggered;

        if (row.ExitPlaced)
        {
            row.Status = "Exit placed";
            row.Detail = "Square-off order sent for this position";
        }
        else if (triggered)
        {
            row.Status = "Exit signal";
            row.Detail = isLong
                ? $"5m close {close:N2} closed below ST {superTrend:N2}"
                : $"5m close {close:N2} closed above ST {superTrend:N2}";
        }
        else
        {
            row.Status = "Monitoring";
            row.Detail = isLong
                ? $"Hold above ST {superTrend:N2} (last close {close:N2})"
                : $"Hold below ST {superTrend:N2} (last close {close:N2})";
        }

        return row;
    }

    private (decimal close, decimal superTrend, bool triggered) EvaluateTrailingStop(
        List<Candle> candles,
        bool isLong)
    {
        var (_, values) = _superTrend.Calculate(
            candles,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier);

        if (values.Count < 2)
            return (0m, 0m, false);

        var closedCandleIndex = candles.Count - 2;
        var offset = candles.Count - values.Count;
        var stIndex = closedCandleIndex - offset;
        if (stIndex < 0)
            stIndex = 0;
        if (stIndex >= values.Count)
            stIndex = values.Count - 1;

        var close = candles[closedCandleIndex].Close;
        var superTrend = values[stIndex];
        var triggered = isLong ? close < superTrend : close > superTrend;
        return (close, superTrend, triggered);
    }

    private async Task TryExitPositionAsync(Position position, TrailingStopRow row)
    {
        var key = PositionKey(position);
        if (_exitedKeys.Contains(key))
            return;

        var transactionType = position.Quantity > 0 ? "SELL" : "BUY";
        var quantity = Math.Abs(position.Quantity);

        var result = await _zerodha.PlaceOrderAsync(
            position.Exchange,
            position.Symbol,
            transactionType,
            quantity,
            "MARKET");

        if (result.IsSuccess)
        {
            _exitedKeys.Add(key);
            row.ExitPlaced = true;
            row.Status = "Exit placed";
            row.Detail = $"Order {result.OrderId} — {transactionType} {quantity} @ MARKET";
            StatusMessage = $"Exit placed for {position.Symbol}: {transactionType} {quantity}";
        }
        else
        {
            row.Detail = result.ErrorMessage ?? "Exit signal — order placement failed";
            StatusMessage = $"Failed to place exit for {position.Symbol}: {result.ErrorMessage}";
        }
    }

    private static string PositionKey(Position position) =>
        $"{position.Exchange}:{position.Symbol}:{position.Product}";

    private void Notify()
    {
        Updated?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _disposed = true;
    }
}
