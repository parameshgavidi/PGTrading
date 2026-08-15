using PgAiTrading.Models.Ui;
using Xunit;

namespace PgAiTrading.Framework.Tests;

public class BrokerUiMessagesTests
{
    [Theory]
    [InlineData("Incorrect api_key or access_token.")]
    [InlineData("incorrect API_KEY or ACCESS_TOKEN")]
    public void IsInvalidCredentialsError_detects_kite_auth_failures(string message)
    {
        Assert.True(BrokerUiMessages.IsInvalidCredentialsError(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Holdings API failed (HTTP 500).")]
    [InlineData("Order rejected")]
    [InlineData("api_key missing")]
    public void IsInvalidCredentialsError_ignores_other_errors(string? message)
    {
        Assert.False(BrokerUiMessages.IsInvalidCredentialsError(message));
    }
}
