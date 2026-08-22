namespace PgAiTrading.Models;

public class StockFundamentals
{
    public decimal RoePercent { get; set; }
    public decimal RocePercent { get; set; }
    public decimal DebtEquityRatio { get; set; }

    /// <summary>Yearly book value per share — Chartink P/B uses Close / this value.</summary>
    public decimal BookValuePerShare { get; set; }

    /// <summary>Optional cached P/B. Prefer <see cref="BookValuePerShare"/> with live close.</summary>
    public decimal PriceToBook { get; set; }

    public decimal MarketCapCr { get; set; }

    /// <summary>
    /// Chartink parity: Quarterly/latest Close ÷ Yearly Book Value.
    /// Falls back to stored <see cref="PriceToBook"/> when book value is missing.
    /// </summary>
    public decimal ResolvePriceToBook(decimal close)
    {
        if (BookValuePerShare > 0 && close > 0)
            return close / BookValuePerShare;

        return PriceToBook;
    }
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
