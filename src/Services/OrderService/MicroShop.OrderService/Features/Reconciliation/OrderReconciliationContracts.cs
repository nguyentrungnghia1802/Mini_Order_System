using MicroShop.OrderService.Features.Orders;

namespace MicroShop.OrderService.Features.Reconciliation;

public static class ReconciliationOperations
{
    public const string Inventory = "inventory";

    public const string Cancellation = "cancellation";
}

public static class ReconciliationOutcomes
{
    public const string Confirmed = "confirmed";

    public const string Rejected = "rejected";

    public const string Cancelled = "cancelled";

    public const string StillPending = "still_pending";

    public const string Conflict = "conflict";

    public const string DependencyUnavailable = "dependency_unavailable";
}

public sealed record OrderReconciliationResponse(
    OrderResponse Order,
    string Operation,
    string Outcome,
    bool Changed,
    string? ReservationStatus,
    Guid? ReservationId,
    DateTimeOffset ReconciledAtUtc);

public sealed record OrderReconciliationOutcome(
    bool IsSuccess,
    bool Changed,
    Persistence.Entities.Order? Order,
    string Operation,
    string Outcome,
    string? ReservationStatus,
    Guid? ReservationId,
    DateTimeOffset ReconciledAtUtc,
    int StatusCode,
    string Title,
    string Detail,
    string Code)
{
    public static OrderReconciliationOutcome NotFound(
        string operation,
        DateTimeOffset reconciledAtUtc)
    {
        return new(
            false,
            false,
            null,
            operation,
            ReconciliationOutcomes.Conflict,
            null,
            null,
            reconciledAtUtc,
            StatusCodes.Status404NotFound,
            "Order not found",
            "The order could not be found for reconciliation.",
            "ORDER_NOT_FOUND");
    }

    public static OrderReconciliationOutcome Success(
        Persistence.Entities.Order order,
        string operation,
        string outcome,
        bool changed,
        string? reservationStatus,
        Guid? reservationId,
        DateTimeOffset reconciledAtUtc,
        string detail)
    {
        return new(
            true,
            changed,
            order,
            operation,
            outcome,
            reservationStatus,
            reservationId,
            reconciledAtUtc,
            StatusCodes.Status200OK,
            "Reconciliation completed",
            detail,
            string.Empty);
    }

    public static OrderReconciliationOutcome Failure(
        Persistence.Entities.Order order,
        string operation,
        string outcome,
        string? reservationStatus,
        Guid? reservationId,
        DateTimeOffset reconciledAtUtc,
        int statusCode,
        string title,
        string detail,
        string code)
    {
        return new(
            false,
            false,
            order,
            operation,
            outcome,
            reservationStatus,
            reservationId,
            reconciledAtUtc,
            statusCode,
            title,
            detail,
            code);
    }
}
