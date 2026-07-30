namespace PGOne.Models;

/// <summary>
/// Independent Top 10 index-weight probability — not part of the trade framework.
/// Sums SELL-stock weight % + BUY (green) stock weight % from the Top 10 list (~100%).
/// </summary>
public sealed record Top10WeightProbability(
    decimal BuyWeightPercent,
    decimal SellWeightPercent,
    decimal NeutralWeightPercent,
    decimal TotalWeightPercent,
    int Probability,
    string DirectionLabel,
    string SummaryLabel,
    string CssClass);

public static class Top10WeightProbabilityHelper
{
    private const decimal WaitThresholdPercent = 5m;

    public static Top10WeightProbability Calculate(IReadOnlyList<WatchItem> items)
    {
        if (items.Count == 0)
            return Empty();

        decimal buyWeight = 0m;
        decimal sellWeight = 0m;
        decimal neutralWeight = 0m;

        foreach (var item in items)
        {
            var weight = item.Weight;
            if (weight <= 0)
                continue;

            switch (item.Trend)
            {
                case TrendDirection.Buy:
                    buyWeight += weight;
                    break;
                case TrendDirection.Sell:
                    sellWeight += weight;
                    break;
                default:
                    neutralWeight += weight;
                    break;
            }
        }

        var totalWeight = buyWeight + sellWeight + neutralWeight;
        if (totalWeight <= 0)
            return Empty();

        var gap = buyWeight - sellWeight;
        string direction;
        string cssClass;
        int probability;

        if (Math.Abs(gap) < WaitThresholdPercent)
        {
            direction = "Wait";
            cssClass = "wait";
            probability = (int)Math.Round((buyWeight + sellWeight) / 2m, MidpointRounding.AwayFromZero);
        }
        else if (gap > 0)
        {
            direction = "Bull";
            cssClass = "bull";
            probability = (int)Math.Round(buyWeight, MidpointRounding.AwayFromZero);
        }
        else
        {
            direction = "Bear";
            cssClass = "bear";
            probability = (int)Math.Round(sellWeight, MidpointRounding.AwayFromZero);
        }

        var summary = $"SELL {sellWeight:N1}% + BUY {buyWeight:N1}% = {totalWeight:N1}%";

        return new Top10WeightProbability(
            buyWeight,
            sellWeight,
            neutralWeight,
            totalWeight,
            probability,
            direction,
            summary,
            cssClass);
    }

    private static Top10WeightProbability Empty() =>
        new(0m, 0m, 0m, 0m, 0, "Wait", "No Top 10 data", "wait");
}
