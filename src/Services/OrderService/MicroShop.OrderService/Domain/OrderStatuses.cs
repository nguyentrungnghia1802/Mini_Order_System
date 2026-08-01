namespace MicroShop.OrderService.Domain;

public static class OrderStatuses
{
    public const string PendingInventory = "pending_inventory";
    public const string Confirmed = "confirmed";
    public const string Rejected = "rejected";
    public const string InventoryUnknown = "inventory_unknown";
    public const string CancellationPending = "cancellation_pending";
    public const string Cancelled = "cancelled";

    public const string ValidStatusSqlValues =
        "'pending_inventory', 'confirmed', 'rejected', 'inventory_unknown', 'cancellation_pending', 'cancelled'";

    public static bool IsKnown(string status)
    {
        return status is PendingInventory
            or Confirmed
            or Rejected
            or InventoryUnknown
            or CancellationPending
            or Cancelled;
    }

    public static bool CanTransition(string currentStatus, string nextStatus)
    {
        if (!IsKnown(currentStatus) || !IsKnown(nextStatus))
        {
            return false;
        }

        if (string.Equals(currentStatus, nextStatus, StringComparison.Ordinal))
        {
            return currentStatus is Rejected or Cancelled;
        }

        return (currentStatus, nextStatus) switch
        {
            (PendingInventory, Confirmed) => true,
            (PendingInventory, Rejected) => true,
            (PendingInventory, InventoryUnknown) => true,
            (Confirmed, CancellationPending) => true,
            (Confirmed, Cancelled) => true,
            (CancellationPending, Cancelled) => true,
            _ => false
        };
    }
}
