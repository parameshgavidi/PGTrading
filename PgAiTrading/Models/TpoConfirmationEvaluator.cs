namespace PgAiTrading.Models;

public static class TpoConfirmationEvaluator
{
  public static TpoConfirmationAnalysis Evaluate(
    decimal price,
    decimal sessionOpen,
    VolumeProfileLevels profile,
    decimal adx1H,
    bool cprNarrow,
    StrategyConfig config)
  {
    var result = new TpoConfirmationAnalysis { CprNarrow = cprNarrow };

    if (!profile.HasData || price <= 0)
    {
      if (profile.PrevDayPoc > 0)
      {
        result.BuyConfirmed = price > profile.PrevDayPoc;
        result.SellConfirmed = price < profile.PrevDayPoc;
        result.Summary = result.BuyConfirmed ? "Bull — above prev POC"
          : result.SellConfirmed ? "Bear — below prev POC"
          : "At prev POC";
      }
      else
        result.Summary = "Await volume profile data";

      return result;
    }

    result.InsideValueArea = profile.IsInsideValueArea(price);
    result.AboveValueArea = profile.IsAboveValueArea(price);
    result.BelowValueArea = profile.IsBelowValueArea(price);
    result.BuyConfirmed = profile.ConfirmsBuy(price);
    result.SellConfirmed = profile.ConfirmsSell(price);

    result.RotationInsideVa = adx1H < config.AdxWeakThreshold && result.InsideValueArea;
    result.TrendDayOutsideVa = adx1H >= config.AdxStrongThreshold
      && (result.AboveValueArea || result.BelowValueArea);

    if (sessionOpen > 0)
      result.OpenOutsideValueArea = sessionOpen > profile.Vah || sessionOpen < profile.Val;

    result.StrongTrendDay = cprNarrow && result.OpenOutsideValueArea;

    result.Summary = BuildSummary(result);
    return result;
  }

  private static string BuildSummary(TpoConfirmationAnalysis tpo)
  {
    if (tpo.BuyConfirmed)
      return "Bull — above POC";

    if (tpo.SellConfirmed)
      return "Bear — below POC";

    if (tpo.RotationInsideVa)
      return "Rotation inside VA — avoid breakouts";

    if (tpo.InsideValueArea)
      return "Inside value area";

    if (tpo.AboveValueArea)
      return "Above value area";

    if (tpo.BelowValueArea)
      return "Below value area";

    return "TPO not confirmed";
  }
}
