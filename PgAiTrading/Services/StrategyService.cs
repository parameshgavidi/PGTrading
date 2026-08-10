using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IStrategyService
{
    StrategyConfig IntradayConfig { get; }
    LongTermStrategyConfig LongTermConfig { get; }
    Task SaveIntradayAsync(StrategyConfig config);
    Task SaveLongTermAsync(LongTermStrategyConfig config);
}

public class StrategyService : IStrategyService
{
    private readonly ISettingsService _settings;

    public StrategyConfig IntradayConfig => _settings.Strategy;
    public LongTermStrategyConfig LongTermConfig => _settings.LongTermStrategy;

    public StrategyService(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task SaveIntradayAsync(StrategyConfig config)
    {
        _settings.Strategy.SuperTrend1HPeriod = config.SuperTrend1HPeriod;
        _settings.Strategy.SuperTrend1HMultiplier = config.SuperTrend1HMultiplier;
        _settings.Strategy.SuperTrend15MPeriod = config.SuperTrend15MPeriod;
        _settings.Strategy.SuperTrend15MMultiplier = config.SuperTrend15MMultiplier;
        _settings.Strategy.SuperTrend5MPeriod = config.SuperTrend5MPeriod;
        _settings.Strategy.SuperTrend5MMultiplier = config.SuperTrend5MMultiplier;
        _settings.Strategy.RsiLength = config.RsiLength;
        _settings.Strategy.RsiTrendLength = config.RsiTrendLength;
        _settings.Strategy.RsiBullThreshold = config.RsiBullThreshold;
        _settings.Strategy.RsiBearThreshold = config.RsiBearThreshold;
        _settings.Strategy.RsiReversalThreshold = config.RsiReversalThreshold;
        _settings.Strategy.AdxLength = config.AdxLength;
        _settings.Strategy.AdxWeakThreshold = config.AdxWeakThreshold;
        _settings.Strategy.AdxStrongThreshold = config.AdxStrongThreshold;
        _settings.Strategy.MinimumAdx = config.MinimumAdx;
        _settings.Strategy.KeltnerEmaLength = config.KeltnerEmaLength;
        _settings.Strategy.KeltnerAtrLength = config.KeltnerAtrLength;
        _settings.Strategy.KeltnerMultiplierInner = config.KeltnerMultiplierInner;
        _settings.Strategy.KeltnerMultiplierOuter = config.KeltnerMultiplierOuter;
        _settings.Strategy.EntryMode = config.EntryMode;
        await _settings.SaveStrategyAsync();
    }

    public async Task SaveLongTermAsync(LongTermStrategyConfig config)
    {
        _settings.LongTermStrategy.SuperTrendPeriod = config.SuperTrendPeriod;
        _settings.LongTermStrategy.SuperTrendMultiplier = config.SuperTrendMultiplier;
        _settings.LongTermStrategy.MinRoePercent = config.MinRoePercent;
        _settings.LongTermStrategy.MinRocePercent = config.MinRocePercent;
        _settings.LongTermStrategy.MaxDebtEquityRatio = config.MaxDebtEquityRatio;
        _settings.LongTermStrategy.MaxPriceToBook = config.MaxPriceToBook;
        _settings.LongTermStrategy.MinMarketCapCr = config.MinMarketCapCr;
        _settings.LongTermStrategy.YearlyHighLowerBand = config.YearlyHighLowerBand;
        _settings.LongTermStrategy.YearlyHighUpperBand = config.YearlyHighUpperBand;
        _settings.LongTermStrategy.MinVolumeSma = config.MinVolumeSma;
        _settings.LongTermStrategy.AdxPeriod = config.AdxPeriod;
        _settings.LongTermStrategy.MinPlusDi = config.MinPlusDi;
        _settings.LongTermStrategy.EmaFastPeriod = config.EmaFastPeriod;
        _settings.LongTermStrategy.EmaSlowPeriod = config.EmaSlowPeriod;
        _settings.LongTermStrategy.WmaFastPeriod = config.WmaFastPeriod;
        _settings.LongTermStrategy.WmaSlowPeriod = config.WmaSlowPeriod;
        _settings.LongTermStrategy.AtrPeriod = config.AtrPeriod;
        _settings.LongTermStrategy.AtrMinCloseRatio = config.AtrMinCloseRatio;
        await _settings.SaveLongTermStrategyAsync();
    }
}
