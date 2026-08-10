namespace PgAiTrading.Models;

public static class TargetPnLEvaluator
{
    public static TargetPnLTrigger Evaluate(decimal aggregatePnL, decimal profitTarget, decimal lossTarget)
    {
        if (profitTarget > 0 && aggregatePnL >= profitTarget)
            return TargetPnLTrigger.Profit;

        if (lossTarget > 0 && aggregatePnL <= -lossTarget)
            return TargetPnLTrigger.Loss;

        return TargetPnLTrigger.None;
    }
}
