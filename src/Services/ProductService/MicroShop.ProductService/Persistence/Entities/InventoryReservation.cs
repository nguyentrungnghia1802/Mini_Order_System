using MicroShop.ProductService.Domain;

namespace MicroShop.ProductService.Persistence.Entities;

public sealed class InventoryReservation
{
    private InventoryReservation()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string RequestHash { get; private set; } = string.Empty;

    public string Status { get; private set; } = InventoryReservationStatuses.Reserved;

    public string Currency { get; private set; } = "VND";

    public decimal TotalAmount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public ICollection<InventoryReservationItem> Items { get; private set; } =
        new List<InventoryReservationItem>();

    public static InventoryReservation Create(
        Guid id,
        Guid orderId,
        string requestHash,
        decimal totalAmount,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Reservation ID is required.", nameof(id));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(requestHash))
        {
            throw new ArgumentException("Request hash is required.", nameof(requestHash));
        }

        if (totalAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "Reservation total cannot be negative.");
        }

        return new InventoryReservation
        {
            Id = id,
            OrderId = orderId,
            RequestHash = requestHash.Trim().ToLowerInvariant(),
            Status = InventoryReservationStatuses.Reserved,
            Currency = "VND",
            TotalAmount = decimal.Round(totalAmount, 2, MidpointRounding.ToEven),
            CreatedAtUtc = createdAtUtc
        };
    }

    public void AddItem(InventoryReservationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Items.Any(existing => existing.ProductId == item.ProductId))
        {
            throw new InvalidOperationException("A reservation cannot contain duplicate Product IDs.");
        }

        item.AttachTo(this);
        Items.Add(item);
    }

    public void Release(DateTimeOffset releasedAtUtc)
    {
        if (Status == InventoryReservationStatuses.Released)
        {
            return;
        }

        if (Status != InventoryReservationStatuses.Reserved)
        {
            throw new InvalidOperationException($"Reservation status '{Status}' cannot be released.");
        }

        Status = InventoryReservationStatuses.Released;
        ReleasedAtUtc = releasedAtUtc;
        foreach (var item in Items)
        {
            item.Release();
        }
    }
}
