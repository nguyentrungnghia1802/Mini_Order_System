using MicroShop.OrderService.Domain;

namespace MicroShop.OrderService.Persistence.Entities;

public sealed class Order
{
    private Order()
    {
    }

    public Guid Id { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public string Status { get; private set; } = OrderStatuses.PendingInventory;

    public string Currency { get; private set; } = "VND";

    public decimal TotalAmount { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureDetail { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public long Version { get; private set; } = 1;

    public ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();

    public ICollection<OrderStateHistory> StateHistory { get; private set; } = new List<OrderStateHistory>();

    public static Order Create(
        Guid id,
        string customerName,
        string customerEmail,
        DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("Customer name is required.", nameof(customerName));
        }

        if (customerName.Trim().Length > 150)
        {
            throw new ArgumentException("Customer name cannot exceed 150 characters.", nameof(customerName));
        }

        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            throw new ArgumentException("Customer email is required.", nameof(customerEmail));
        }

        var order = new Order
        {
            Id = id,
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail.Trim().ToLowerInvariant(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        order.StateHistory.Add(OrderStateHistory.Create(
            id,
            fromStatus: null,
            OrderStatuses.PendingInventory,
            reasonCode: "ORDER_CREATED",
            traceId: null,
            now));

        return order;
    }

    public void AddItem(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.ProductId == Guid.Empty)
        {
            throw new ArgumentException("Product ID is required.", nameof(item));
        }

        if (Items.Any(existing => existing.ProductId == item.ProductId))
        {
            throw new InvalidOperationException("An order cannot contain duplicate Product IDs.");
        }

        item.AttachTo(this);
        Items.Add(item);
        RecalculateTotal();
    }

    public void RecalculateTotal()
    {
        TotalAmount = decimal.Round(
            Items.Sum(item => item.Subtotal),
            2,
            MidpointRounding.ToEven);
    }

    public void RecordFailure(string code, string? detail, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Failure code is required.", nameof(code));
        }

        FailureCode = code.Trim();
        FailureDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        UpdatedAtUtc = now;
    }

    public void TransitionTo(
        string nextStatus,
        string? reasonCode,
        string? traceId,
        DateTimeOffset now)
    {
        if (!OrderStatuses.CanTransition(Status, nextStatus))
        {
            throw new InvalidOperationException(
                $"Order cannot transition from '{Status}' to '{nextStatus}'.");
        }

        if (string.Equals(Status, nextStatus, StringComparison.Ordinal))
        {
            return;
        }

        var previousStatus = Status;
        Status = nextStatus;
        UpdatedAtUtc = now;
        Version++;

        if (nextStatus == OrderStatuses.Confirmed)
        {
            ConfirmedAtUtc = now;
        }

        if (nextStatus == OrderStatuses.Cancelled)
        {
            CancelledAtUtc = now;
        }

        StateHistory.Add(OrderStateHistory.Create(
            Id,
            previousStatus,
            nextStatus,
            reasonCode,
            traceId,
            now));
    }
}
