namespace PgAiTrading.Models.Ui;

/// <summary>Canonical user-facing broker/connection copy — keep strings in one place.</summary>
public static class BrokerUiMessages
{
    /// <summary>Sentiment / older UI paths.</summary>
    public const string ConnectRequired = "Please connect to Zerodha first.";

    /// <summary>Signals panel and similar.</summary>
    public const string ConnectBeforeOrder = "Connect to Zerodha in Settings before placing an order.";

    /// <summary>Matches <c>ZerodhaService.PlaceOrderAsync</c> when disconnected.</summary>
    public const string BrokerNotConnected =
        "Not connected to Zerodha. Connect in Settings and try again.";

    public const string ConnectForLiveData = "Connect Zerodha in Settings for live data.";
    public const string BrokerOfflinePositions = "Broker offline — connect Zerodha to view live positions.";
    public const string BrokerOfflineOrders = "Broker offline — connect Zerodha to view today’s orders.";

    /// <summary>Shown when Kite rejects the session (bad API key or expired access token).</summary>
    public const string InvalidCredentials =
        "Incorrect API key or access token. Update credentials in Settings and reconnect to Zerodha.";

    /// <summary>Sentiment / UI quantity validation.</summary>
    public const string QuantityInvalid = "Quantity must be at least 1.";

    /// <summary>Matches <c>ZerodhaService.PlaceOrderAsync</c> quantity check.</summary>
    public const string OrderQuantityInvalid = "Order quantity must be at least 1.";

    public const string CouldNotFetchPrice = "Could not fetch price for limit order.";
    public const string OrderPlacementFailed = "Order placement failed.";
    public const string OrderRejected = "Order rejected";

    /// <summary>True when a Kite error message indicates a bad API key or access token.</summary>
    public static bool IsInvalidCredentialsError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return false;

        return error.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            && error.Contains("access_token", StringComparison.OrdinalIgnoreCase);
    }
}
