namespace PgAiTrading.Models;

public class StockFundamentals
{
    public decimal RoePercent { get; set; }
    public decimal RocePercent { get; set; }
    public decimal DebtEquityRatio { get; set; }
    public decimal PriceToBook { get; set; }
    public decimal MarketCapCr { get; set; }
}

public class FrameworkConditionResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public class LongTermEvaluation
{
    public bool Satisfied { get; set; }
    public int Score { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StopLoss { get; set; }
    public List<FrameworkConditionResult> Conditions { get; set; } = new();
}
