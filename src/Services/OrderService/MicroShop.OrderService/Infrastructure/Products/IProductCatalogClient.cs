namespace MicroShop.OrderService.Infrastructure.Products;

public interface IProductCatalogClient
{
    Task<FakeProductResolution> ResolveAsync(
        IReadOnlyList<FakeProductRequestItem> requestedItems,
        CancellationToken cancellationToken);
}

public sealed record FakeProductRequestItem(Guid ProductId, int Quantity);

public sealed record FakeProductSnapshot(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public enum FakeProductFailure
{
    None,
    NotFound,
    Inactive,
    InsufficientStock
}

public sealed record FakeProductResolution(
    FakeProductFailure Failure,
    Guid? ProductId,
    int? AvailableStock,
    IReadOnlyList<FakeProductSnapshot> Items)
{
    public bool IsSuccess => Failure is FakeProductFailure.None;
}
