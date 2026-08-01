namespace MicroShop.OrderService.Persistence.Entities;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Order? Order { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal Subtotal { get; private set; }

    public static OrderItem Create(
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID is required.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name is required.", nameof(productName));
        }

        if (productName.Trim().Length > 200)
        {
            throw new ArgumentException("Product name cannot exceed 200 characters.", nameof(productName));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        var normalizedUnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.ToEven);
        return new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = normalizedUnitPrice,
            Quantity = quantity,
            Subtotal = decimal.Round(normalizedUnitPrice * quantity, 2, MidpointRounding.ToEven)
        };
    }

    internal void AttachTo(Order order)
    {
        Order = order;
        OrderId = order.Id;
    }
}
