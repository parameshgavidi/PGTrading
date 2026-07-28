using PGOne.Models;

namespace PGOne.Services;

public interface ITargetPnLMonitorService
{
    event Action? Updated;
    decimal AggregatePnL { get; }
    int OpenPositionCount { get; }
    bool IsLoading { get; }
    bool IsMonitoring { get; }
    string? StatusMessage { get; }
    TargetPnLTrigger LastTrigger { get; }
    DateTime? LastUpdatedAt { get; }
    Task RefreshAsync();
    Task SetMonitoringAsync(bool enabled);
}

public class TargetPnLMonitorService : ITargetPnLMonitorService, IDisposable
{
    private const int MonitorIntervalSeconds = 15;

    private readonly IZerodhaService _zerodha;
    private readonly ISettingsService _settings;

    private readonly HashSet<string> _exitedKeys = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _monitorCts;
    private bool _disposed;

    public event Action? Updated;
    public decimal AggregatePnL { get; private set; }
    public int OpenPositionCount { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsMonitoring { get; private set; }
    public string? StatusMessage { get; private set; }
    public TargetPnLTrigger LastTrigger { get; private set; } = TargetPnLTrigger.None;
    public DateTime? LastUpdatedAt { get; private set; }

    public TargetPnLMonitorService(IZerodhaService zerodha, ISettingsService settings)
    {
        _zerodha = zerodha;
        _settings = settings;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        Notify();

        try
        {
            await _settings.LoadAsync();

            if (!_zerodha.IsConnected)
            {
                AggregatePnL = 0;
                OpenPositionCount = 0;
                StatusMessage = "Connect to Zerodha to monitor open P&L.";
                return;
            }

            var positions = await _zerodha.GetPositionsAsync();
            var open = positions.Where(p => p.Quantity != 0).ToList();
            OpenPositionCount = open.Count;
            AggregatePnL = open.Sum(p => p.PnL);

            if (IsMonitoring && open.Count > 0)
            {
                var trigger = EvaluateTrigger(AggregatePnL);
                if (trigger != TargetPnLTrigger.None)
                {
                    LastTrigger = trigger;
                    await ExitAllPositionsAsync(open, trigger);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Target P&L refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            LastUpdatedAt = DateTime.Now;
            Notify();
        }
    }

    public async Task SetMonitoringAsync(bool enabled)
    {
        if (enabled == IsMonitoring)
            return;

        IsMonitoring = enabled;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        if (enabled)
        {
            _exitedKeys.Clear();
            LastTrigger = TargetPnLTrigger.None;
            _monitorCts = new CancellationTokenSource();

            var profit = _settings.Settings.TargetProfitAmount;
            var loss = _settings.Settings.TargetLossAmount;
            StatusMessage = profit > 0 && loss > 0
                ? $"Auto-exit active — book profit at +₹{profit:N0} or loss at −₹{loss:N0} aggregate P&L."
                : profit > 0
                    ? $"Auto-exit active — book profit at +₹{profit:N0} aggregate P&L."
                    : loss > 0
                        ? $"Auto-exit active — book loss at −₹{loss:N0} aggregate P&L."
                        : "Set profit or loss target amounts to enable auto-exit.";

            Notify();
            await RefreshAsync();
            _ = MonitorLoopAsync(_monitorCts.Token);
        }
        else
        {
            StatusMessage = "Target P&L auto-exit stopped.";
            Notify();
        }
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await RefreshAsync();
                await Task.Delay(TimeSpan.FromSeconds(MonitorIntervalSeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Monitoring stopped.
        }
    }

    private TargetPnLTrigger EvaluateTrigger(decimal aggregatePnL)
    {
        return TargetPnLEvaluator.Evaluate(
            aggregatePnL,
            _settings.Settings.TargetProfitAmount,
            _settings.Settings.TargetLossAmount);
    }

    private async Task ExitAllPositionsAsync(List<Position> positions, TargetPnLTrigger trigger)
    {
        var label = trigger == TargetPnLTrigger.Profit ? "Profit target" : "Loss limit";
        var placed = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var position in positions)
        {
            var key = PositionKey(position);
            if (_exitedKeys.Contains(key))
            {
                skipped++;
                continue;
            }

            var result = await _zerodha.ExitPositionAsync(position);
            if (result.IsSuccess)
            {
                _exitedKeys.Add(key);
                placed++;
            }
            else
            {
                errors.Add($"{position.Symbol}: {result.ErrorMessage}");
            }
        }

        if (placed > 0)
        {
            StatusMessage = $"{label} hit (₹{AggregatePnL:N2}) — exit placed for {placed} position(s).";
            if (errors.Count > 0)
                StatusMessage += $" {errors.Count} failed: {errors[0]}";
        }
        else if (errors.Count > 0)
        {
            StatusMessage = $"{label} hit (₹{AggregatePnL:N2}) — exit failed: {errors[0]}";
            if (errors.Count > 1)
                StatusMessage += $" (+{errors.Count - 1} more)";
        }
        else if (skipped > 0 && skipped == positions.Count)
        {
            StatusMessage = $"{label} hit — exit orders already sent for all open positions.";
        }
        else
        {
            StatusMessage = $"{label} hit but no open positions to exit.";
        }
    }

    private static string PositionKey(Position position) =>
        $"{position.Exchange}:{position.Symbol}:{position.Product}";

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
