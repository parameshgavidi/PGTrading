namespace PGOneTrade.Models;

public class LongTermStrategyConfig
{
    public int SuperTrendPeriod { get; set; } = 10;
    public double SuperTrendMultiplier { get; set; } = 3.0;

    public decimal MinRoePercent { get; set; } = 15m;
    public decimal MinRocePercent { get; set; } = 15m;
    public decimal MaxDebtEquityRatio { get; set; } = 1m;
    public decimal MaxPriceToBook { get; set; } = 5m;
    public decimal MinMarketCapCr { get; set; } = 1000m;

    public decimal YearlyHighLowerBand { get; set; } = 0.4m;
    public decimal YearlyHighUpperBand { get; set; } = 0.8m;
    public int MinVolumeSma { get; set; } = 100_000;

    public int AdxPeriod { get; set; } = 14;
    public decimal MinPlusDi { get; set; } = 20m;

    public int EmaFastPeriod { get; set; } = 20;
    public int EmaSlowPeriod { get; set; } = 50;
    public int WmaFastPeriod { get; set; } = 20;
    public int WmaSlowPeriod { get; set; } = 50;

    public int AtrPeriod { get; set; } = 14;
    public decimal AtrMinCloseRatio { get; set; } = 0.001m;
}
