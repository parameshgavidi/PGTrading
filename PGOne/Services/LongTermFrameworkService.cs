using PGOne.Models;

namespace PGOne.Services;

public interface ILongTermFrameworkService
{
    Task<LongTermEvaluation> EvaluateAsync(string symbol, decimal lastPrice, string exchange = "NSE");
    IReadOnlyList<string> FrameworkConditions { get; }
}

public class LongTermFrameworkService : ILongTermFrameworkService
{
    private const int SuperTrendPeriod = 10;
    private const double SuperTrendMultiplier = 3.0;

    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;
    private readonly IIndicatorService _indicators;
    private readonly IFundamentalDataService _fundamentals;

    public IReadOnlyList<string> FrameworkConditions { get; } =
    [
        "Yearly Return on net worth % > 15",
        "Yearly Return on capital employed % > 15",
        "Yearly Debt equity ratio < 1",
        "Quarterly Close / Yearly Book value < 5",
        "Market Cap > 1000",
        "Daily Close >= Yearly High * 0.4",
        "Daily Close <= Yearly High * 0.8",
        "Daily SMA(Volume, 20) > 100000",
        "Weekly Close > Weekly SuperTrend(10, 3)",
        "Daily Close > Daily SuperTrend(10, 3)",
        "Daily ADX DI+(14) > 20",
        "Daily EMA(20) > Daily EMA(50)",
        "Daily WMA(20) > Daily WMA(50)",
        "Daily ATR(14) > Close * 0.001",
        "Stop Loss: 1Day SuperTrend (10, 3)"
    ];

    public LongTermFrameworkService(
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        IIndicatorService indicators,
        IFundamentalDataService fundamentals)
    {
        _marketData = marketData;
        _superTrend = superTrend;
        _indicators = indicators;
        _fundamentals = fundamentals;
    }

    public async Task<LongTermEvaluation> EvaluateAsync(string symbol, decimal lastPrice, string exchange = "NSE")
    {
        var instrument = InstrumentMapper.ToZerodhaKey(symbol, exchange);
        var daily = await _marketData.GetCandlesAsync(instrument, "1D", 300);
        var weekly = CandleAggregator.ToWeekly(daily);
        var fundamentals = _fundamentals.GetFundamentals(symbol);
        var conditions = new List<FrameworkConditionResult>();

        if (fundamentals is not null)
        {
            conditions.Add(Condition("Yearly ROE % > 15", fundamentals.RoePercent > 15, $"{fundamentals.RoePercent:0.#}%"));
            conditions.Add(Condition("Yearly ROCE % > 15", fundamentals.RocePercent > 15, $"{fundamentals.RocePercent:0.#}%"));
            conditions.Add(Condition("Debt/Equity < 1", fundamentals.DebtEquityRatio < 1, $"{fundamentals.DebtEquityRatio:0.##}"));
            conditions.Add(Condition("P/B < 5", fundamentals.PriceToBook < 5, $"{fundamentals.PriceToBook:0.##}"));
            conditions.Add(Condition("Market Cap > 1000 Cr", fundamentals.MarketCapCr > 1000, $"₹{fundamentals.MarketCapCr:N0} Cr"));
        }
        else
        {
            conditions.Add(Condition("Fundamental data", false, "Unavailable for this symbol"));
        }

        if (daily.Count > 0)
        {
            var close = daily[^1].Close;
            var yearlyHigh = daily.Max(c => c.High);
            var lowerBand = yearlyHigh * 0.4m;
            var upperBand = yearlyHigh * 0.8m;
            var volumeSma = _indicators.CalculateSmaVolume(daily, 20);
            var ema20 = _indicators.CalculateEma(daily, 20);
            var ema50 = _indicators.CalculateEma(daily, 50);
            var wma20 = _indicators.CalculateWma(daily, 20);
            var wma50 = _indicators.CalculateWma(daily, 50);
            var atr = _indicators.CalculateAtr(daily, 14);
            var plusDi = _indicators.CalculatePlusDi(daily, 14);
            var (_, dailyStValues) = _superTrend.Calculate(daily, SuperTrendPeriod, SuperTrendMultiplier);
            var dailySuperTrend = dailyStValues.Count > 0 ? dailyStValues[^1] : 0m;

            conditions.Add(Condition("Close >= Yearly High × 0.4", close >= lowerBand, $"{close:N2} vs {lowerBand:N2}"));
            conditions.Add(Condition("Close <= Yearly High × 0.8", close <= upperBand, $"{close:N2} vs {upperBand:N2}"));
            conditions.Add(Condition("SMA(Volume,20) > 1L", volumeSma > 100_000, $"{volumeSma:N0}"));
            conditions.Add(Condition("Daily Close > Daily ST(10,3)", close > dailySuperTrend, $"{close:N2} vs {dailySuperTrend:N2}"));
            conditions.Add(Condition("ADX DI+(14) > 20", plusDi > 20, $"{plusDi:0.#}"));
            conditions.Add(Condition("EMA(20) > EMA(50)", ema20 > ema50, $"{ema20:N2} > {ema50:N2}"));
            conditions.Add(Condition("WMA(20) > WMA(50)", wma20 > wma50, $"{wma20:N2} > {wma50:N2}"));
            conditions.Add(Condition("ATR(14) > Close × 0.001", atr > close * 0.001m, $"{atr:N2}"));

            if (weekly.Count > 0)
            {
                var weeklyClose = weekly[^1].Close;
                var (_, weeklyStValues) = _superTrend.Calculate(weekly, SuperTrendPeriod, SuperTrendMultiplier);
                var weeklySuperTrend = weeklyStValues.Count > 0 ? weeklyStValues[^1] : 0m;
                conditions.Add(Condition("Weekly Close > Weekly ST(10,3)", weeklyClose > weeklySuperTrend, $"{weeklyClose:N2} vs {weeklySuperTrend:N2}"));
            }

            var stopLoss = dailySuperTrend > 0 ? $"₹{dailySuperTrend:N2} (1D SuperTrend 10,3)" : "1D SuperTrend (10,3)";
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
            StopLoss = "1D SuperTrend (10,3)",
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
