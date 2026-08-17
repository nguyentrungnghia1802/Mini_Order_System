namespace MicroShop.Contracts.Orders;

public sealed record OrderConfirmedV1(
    Guid MessageId,
    Guid OrderId,
    string CustomerName,
    string CustomerEmail,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderConfirmedItemV1> Items,
    DateTimeOffset OccurredAtUtc,
    int SchemaVersion = 1);

public sealed record OrderConfirmedItemV1(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);
