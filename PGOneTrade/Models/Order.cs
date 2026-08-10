namespace PGOneTrade.Models;

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderTime { get; set; }
}

public sealed class OrderPlacementResult
{
    public string? OrderId { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSuccess => !string.IsNullOrEmpty(OrderId);

    public static OrderPlacementResult Ok(string orderId) => new() { OrderId = orderId };

    public static OrderPlacementResult Fail(string message) => new() { ErrorMessage = message };
}
