namespace MicroShop.OrderService.Infrastructure.Products;

public interface IProductInventoryClient
{
    Task<ProductReservationResult> ReserveAsync(
        ProductReservationRequest request,
        CancellationToken cancellationToken);

    Task<ProductReleaseResult> ReleaseAsync(
        ProductReleaseRequest request,
        CancellationToken cancellationToken);

    Task<ProductReservationLookupResult> GetReservationByOrderAsync(
        ProductReservationLookupRequest request,
        CancellationToken cancellationToken);
}

public sealed record ProductReservationRequest(
    Guid OrderId,
    IReadOnlyList<ProductReservationRequestItem> Items,
    string? TraceParent);

public sealed record ProductReservationRequestItem(Guid ProductId, int Quantity);

public sealed record ProductReleaseRequest(Guid OrderId, string? TraceParent);

public sealed record ProductReservationLookupRequest(Guid OrderId, string? TraceParent);

public sealed record ProductReservationSnapshot(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public enum ProductReservationFailure
{
    None,
    ProductNotFound,
    ProductInactive,
    InsufficientStock,
    RequestMismatch,
    DependencyUnavailable,
    OutcomeUnknown,
    ReservationStateConflict,
    ReservationNotFound,
    InvalidResponse
}

public sealed record ProductReservationResult(
    ProductReservationFailure Failure,
    Guid? ReservationId,
    IReadOnlyList<ProductReservationSnapshot> Items,
    decimal TotalAmount,
    Guid? ProductId,
    int? AvailableStock,
    string? Detail,
    bool Created,
    bool IdempotentReplay)
{
    public bool IsSuccess => Failure is ProductReservationFailure.None;

    public bool IsAmbiguous => Failure is ProductReservationFailure.OutcomeUnknown;
}

public sealed record ProductReleaseResult(
    ProductReservationFailure Failure,
    Guid? ReservationId,
    string? Detail,
    bool IdempotentReplay)
{
    public bool IsSuccess => Failure is ProductReservationFailure.None;

    public bool IsAmbiguous => Failure is ProductReservationFailure.OutcomeUnknown;
}

public sealed record ProductReservationLookupResult(
    ProductReservationFailure Failure,
    Guid? ReservationId,
    string? Status,
    string? Currency,
    IReadOnlyList<ProductReservationSnapshot> Items,
    decimal TotalAmount,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? ReleasedAtUtc,
    string? Detail)
{
    public bool IsSuccess => Failure is ProductReservationFailure.None;

    public bool IsReservationMissing => Failure is ProductReservationFailure.ReservationNotFound;
}
