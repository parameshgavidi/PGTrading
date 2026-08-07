namespace PGOne.Models.Ui;

/// <summary>Canonical user-facing broker/connection copy — keep strings in one place.</summary>
public static class BrokerUiMessages
{
    public const string ConnectRequired = "Please connect to Zerodha first.";
    public const string ConnectBeforeOrder = "Connect to Zerodha in Settings before placing an order.";
    public const string ConnectForLiveData = "Connect Zerodha in Settings for live data.";
    public const string BrokerOfflinePositions = "Broker offline — connect Zerodha to view live positions.";
    public const string BrokerOfflineOrders = "Broker offline — connect Zerodha to view today’s orders.";
    public const string QuantityInvalid = "Quantity must be at least 1.";
    public const string CouldNotFetchPrice = "Could not fetch price for limit order.";
}
