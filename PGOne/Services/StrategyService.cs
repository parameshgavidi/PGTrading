using PGOne.Models;

namespace PGOne.Services;

public interface IStrategyService
{
    StrategyConfig Config { get; }
    Task SaveAsync(StrategyConfig config);
}

public class StrategyService : IStrategyService
{
    private readonly ISettingsService _settings;

    public StrategyConfig Config => _settings.Strategy;

    public StrategyService(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task SaveAsync(StrategyConfig config)
    {
        _settings.Strategy.SuperTrend1HPeriod = config.SuperTrend1HPeriod;
        _settings.Strategy.SuperTrend1HMultiplier = config.SuperTrend1HMultiplier;
        _settings.Strategy.SuperTrend15MPeriod = config.SuperTrend15MPeriod;
        _settings.Strategy.SuperTrend15MMultiplier = config.SuperTrend15MMultiplier;
        _settings.Strategy.SuperTrend5MPeriod = config.SuperTrend5MPeriod;
        _settings.Strategy.SuperTrend5MMultiplier = config.SuperTrend5MMultiplier;
        _settings.Strategy.RsiLength = config.RsiLength;
        _settings.Strategy.AdxLength = config.AdxLength;
        _settings.Strategy.MinimumAdx = config.MinimumAdx;
        _settings.Strategy.EntryMode = config.EntryMode;
        await _settings.SaveStrategyAsync();
    }
}
