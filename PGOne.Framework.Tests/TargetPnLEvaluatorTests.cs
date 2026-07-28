using PGOne.Models;
using Xunit;

namespace PGOne.Framework.Tests;

public class TargetPnLEvaluatorTests
{
  [Fact]
  public void Profit_trigger_when_aggregate_meets_target()
  {
    Assert.Equal(
      TargetPnLTrigger.Profit,
      TargetPnLEvaluator.Evaluate(1500m, 1500m, 1500m));
  }

  [Fact]
  public void Loss_trigger_when_aggregate_meets_limit()
  {
    Assert.Equal(
      TargetPnLTrigger.Loss,
      TargetPnLEvaluator.Evaluate(-1500m, 1500m, 1500m));
  }

  [Fact]
  public void No_trigger_when_inside_band()
  {
    Assert.Equal(
      TargetPnLTrigger.None,
      TargetPnLEvaluator.Evaluate(500m, 1500m, 1500m));
  }

  [Fact]
  public void Zero_targets_never_trigger()
  {
    Assert.Equal(TargetPnLTrigger.None, TargetPnLEvaluator.Evaluate(5000m, 0m, 0m));
  }
}
