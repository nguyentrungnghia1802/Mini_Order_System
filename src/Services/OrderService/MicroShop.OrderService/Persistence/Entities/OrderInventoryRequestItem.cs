namespace MicroShop.OrderService.Persistence.Entities;

public sealed class OrderInventoryRequestItem
{
    private OrderInventoryRequestItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Order? Order { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public static OrderInventoryRequestItem Create(
        Guid productId,
        int quantity)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID is required.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new OrderInventoryRequestItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity
        };
    }

    internal void AttachTo(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        Order = order;
        OrderId = order.Id;
    }
}
