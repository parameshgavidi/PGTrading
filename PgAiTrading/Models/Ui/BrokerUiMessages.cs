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

    /// <summary>Sentiment / UI quantity validation.</summary>
    public const string QuantityInvalid = "Quantity must be at least 1.";

    /// <summary>Matches <c>ZerodhaService.PlaceOrderAsync</c> quantity check.</summary>
    public const string OrderQuantityInvalid = "Order quantity must be at least 1.";

    public const string CouldNotFetchPrice = "Could not fetch price for limit order.";
    public const string OrderPlacementFailed = "Order placement failed.";
    public const string OrderRejected = "Order rejected";
}
