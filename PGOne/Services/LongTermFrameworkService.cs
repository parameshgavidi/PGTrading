using PGOne.Models;

namespace PGOne.Services;

public interface ILongTermFrameworkService
{
    Task<LongTermEvaluation> EvaluateAsync(string symbol, decimal lastPrice, string exchange = "NSE");
    IReadOnlyList<string> FrameworkConditions { get; }
}

public class LongTermFrameworkService : ILongTermFrameworkService
{
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;
    private readonly IIndicatorService _indicators;
    private readonly IFundamentalDataService _fundamentals;
    private readonly ISettingsService _settings;

    public LongTermFrameworkService(
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        IIndicatorService indicators,
        IFundamentalDataService fundamentals,
        ISettingsService settings)
    {
        _marketData = marketData;
        _superTrend = superTrend;
        _indicators = indicators;
        _fundamentals = fundamentals;
        _settings = settings;
    }

    public IReadOnlyList<string> FrameworkConditions
    {
        get
        {
            var cfg = _settings.LongTermStrategy;
            return
            [
                $"Yearly Return on net worth % > {cfg.MinRoePercent:0.#}",
                $"Yearly Return on capital employed % > {cfg.MinRocePercent:0.#}",
                $"Yearly Debt equity ratio < {cfg.MaxDebtEquityRatio:0.#}",
                $"Quarterly Close / Yearly Book value < {cfg.MaxPriceToBook:0.#}",
                $"Market Cap > {cfg.MinMarketCapCr:0}",
                $"Daily Close >= Yearly High * {cfg.YearlyHighLowerBand:0.#}",
                $"Daily Close <= Yearly High * {cfg.YearlyHighUpperBand:0.#}",
                $"Daily SMA(Volume, 20) > {cfg.MinVolumeSma:N0}",
                $"Weekly Close > Weekly SuperTrend({cfg.SuperTrendPeriod}, {cfg.SuperTrendMultiplier:0.#})",
                $"Daily Close > Daily SuperTrend({cfg.SuperTrendPeriod}, {cfg.SuperTrendMultiplier:0.#})",
                $"Daily ADX DI+({cfg.AdxPeriod}) > {cfg.MinPlusDi:0.#}",
                $"Daily EMA({cfg.EmaFastPeriod}) > Daily EMA({cfg.EmaSlowPeriod})",
                $"Daily WMA({cfg.WmaFastPeriod}) > Daily WMA({cfg.WmaSlowPeriod})",
                $"Daily ATR({cfg.AtrPeriod}) > Close * {cfg.AtrMinCloseRatio:0.###}",
                $"Stop Loss: 1Day SuperTrend ({cfg.SuperTrendPeriod}, {cfg.SuperTrendMultiplier:0.#})"
            ];
        }
    }

    public async Task<LongTermEvaluation> EvaluateAsync(string symbol, decimal lastPrice, string exchange = "NSE")
    {
        var cfg = _settings.LongTermStrategy;
        var instrument = InstrumentMapper.ToZerodhaKey(symbol, exchange);
        var daily = await _marketData.GetCandlesAsync(instrument, "1D", 300);
        var weekly = CandleAggregator.ToWeekly(daily);
        var fundamentals = _fundamentals.GetFundamentals(symbol);
        var conditions = new List<FrameworkConditionResult>();

        if (fundamentals is not null)
        {
            conditions.Add(Condition($"Yearly ROE % > {cfg.MinRoePercent:0.#}", fundamentals.RoePercent > cfg.MinRoePercent, $"{fundamentals.RoePercent:0.#}%"));
            conditions.Add(Condition($"Yearly ROCE % > {cfg.MinRocePercent:0.#}", fundamentals.RocePercent > cfg.MinRocePercent, $"{fundamentals.RocePercent:0.#}%"));
            conditions.Add(Condition($"Debt/Equity < {cfg.MaxDebtEquityRatio:0.#}", fundamentals.DebtEquityRatio < cfg.MaxDebtEquityRatio, $"{fundamentals.DebtEquityRatio:0.##}"));
            conditions.Add(Condition($"P/B < {cfg.MaxPriceToBook:0.#}", fundamentals.PriceToBook < cfg.MaxPriceToBook, $"{fundamentals.PriceToBook:0.##}"));
            conditions.Add(Condition($"Market Cap > {cfg.MinMarketCapCr:0} Cr", fundamentals.MarketCapCr > cfg.MinMarketCapCr, $"₹{fundamentals.MarketCapCr:N0} Cr"));
        }
        else
        {
            conditions.Add(Condition("Fundamental data", false, "Unavailable for this symbol"));
        }

        if (daily.Count > 0)
        {
            var close = daily[^1].Close;
            var yearlyHigh = daily.Max(c => c.High);
            var lowerBand = yearlyHigh * cfg.YearlyHighLowerBand;
            var upperBand = yearlyHigh * cfg.YearlyHighUpperBand;
            var volumeSma = _indicators.CalculateSmaVolume(daily, 20);
            var emaFast = _indicators.CalculateEma(daily, cfg.EmaFastPeriod);
            var emaSlow = _indicators.CalculateEma(daily, cfg.EmaSlowPeriod);
            var wmaFast = _indicators.CalculateWma(daily, cfg.WmaFastPeriod);
            var wmaSlow = _indicators.CalculateWma(daily, cfg.WmaSlowPeriod);
            var atr = _indicators.CalculateAtr(daily, cfg.AtrPeriod);
            var plusDi = _indicators.CalculatePlusDi(daily, cfg.AdxPeriod);
            var (_, dailyStValues) = _superTrend.Calculate(daily, cfg.SuperTrendPeriod, cfg.SuperTrendMultiplier);
            var dailySuperTrend = dailyStValues.Count > 0 ? dailyStValues[^1] : 0m;

            conditions.Add(Condition($"Close >= Yearly High × {cfg.YearlyHighLowerBand:0.#}", close >= lowerBand, $"{close:N2} vs {lowerBand:N2}"));
            conditions.Add(Condition($"Close <= Yearly High × {cfg.YearlyHighUpperBand:0.#}", close <= upperBand, $"{close:N2} vs {upperBand:N2}"));
            conditions.Add(Condition($"SMA(Volume,20) > {cfg.MinVolumeSma:N0}", volumeSma > cfg.MinVolumeSma, $"{volumeSma:N0}"));
            conditions.Add(Condition($"Daily Close > Daily ST({cfg.SuperTrendPeriod},{cfg.SuperTrendMultiplier:0.#})", close > dailySuperTrend, $"{close:N2} vs {dailySuperTrend:N2}"));
            conditions.Add(Condition($"ADX DI+({cfg.AdxPeriod}) > {cfg.MinPlusDi:0.#}", plusDi > cfg.MinPlusDi, $"{plusDi:0.#}"));
            conditions.Add(Condition($"EMA({cfg.EmaFastPeriod}) > EMA({cfg.EmaSlowPeriod})", emaFast > emaSlow, $"{emaFast:N2} > {emaSlow:N2}"));
            conditions.Add(Condition($"WMA({cfg.WmaFastPeriod}) > WMA({cfg.WmaSlowPeriod})", wmaFast > wmaSlow, $"{wmaFast:N2} > {wmaSlow:N2}"));
            conditions.Add(Condition($"ATR({cfg.AtrPeriod}) > Close × {cfg.AtrMinCloseRatio:0.###}", atr > close * cfg.AtrMinCloseRatio, $"{atr:N2}"));

            if (weekly.Count > 0)
            {
                var weeklyClose = weekly[^1].Close;
                var (_, weeklyStValues) = _superTrend.Calculate(weekly, cfg.SuperTrendPeriod, cfg.SuperTrendMultiplier);
                var weeklySuperTrend = weeklyStValues.Count > 0 ? weeklyStValues[^1] : 0m;
                conditions.Add(Condition($"Weekly Close > Weekly ST({cfg.SuperTrendPeriod},{cfg.SuperTrendMultiplier:0.#})", weeklyClose > weeklySuperTrend, $"{weeklyClose:N2} vs {weeklySuperTrend:N2}"));
            }

            var stopLoss = dailySuperTrend > 0
                ? $"₹{dailySuperTrend:N2} (1D SuperTrend {cfg.SuperTrendPeriod},{cfg.SuperTrendMultiplier:0.#})"
                : $"1D SuperTrend ({cfg.SuperTrendPeriod},{cfg.SuperTrendMultiplier:0.#})";
            var passed = conditions.Count(c => c.Passed);
            var score = conditions.Count > 0 ? (int)Math.Round(passed * 100m / conditions.Count) : 0;
            var satisfied = conditions.Count > 0 && conditions.All(c => c.Passed);

            return new LongTermEvaluation
            {
                Satisfied = satisfied,
                Score = score,
                Status = satisfied ? "Up" : GetStatus(conditions),
                StopLoss = satisfied ? null : stopLoss,
                Conditions = conditions
            };
        }

        return new LongTermEvaluation
        {
            Satisfied = false,
            Score = 0,
            Status = "No data",
            StopLoss = $"1D SuperTrend ({cfg.SuperTrendPeriod},{cfg.SuperTrendMultiplier:0.#})",
            Conditions = conditions
        };
    }

    private static FrameworkConditionResult Condition(string name, bool passed, string detail) =>
        new() { Name = name, Passed = passed, Detail = detail };

    private static string GetStatus(List<FrameworkConditionResult> conditions)
    {
        if (conditions.Any(c => c.Name.StartsWith("Fundamental", StringComparison.Ordinal) && !c.Passed))
            return "Fundamentals";

        if (conditions.Any(c => c.Name.Contains("Weekly", StringComparison.Ordinal) && !c.Passed))
            return "Weekly ST";

        if (conditions.Any(c => c.Name.Contains("Daily ST", StringComparison.Ordinal) && !c.Passed))
            return "Daily ST";

        if (conditions.Any(c => c.Name.Contains("EMA", StringComparison.Ordinal) && !c.Passed))
            return "EMA trend";

        if (conditions.Any(c => c.Name.Contains("WMA", StringComparison.Ordinal) && !c.Passed))
            return "WMA trend";

        if (conditions.Any(c => c.Name.Contains("Yearly High", StringComparison.Ordinal) && !c.Passed))
            return "Price zone";

        return "Need review";
    }
}
