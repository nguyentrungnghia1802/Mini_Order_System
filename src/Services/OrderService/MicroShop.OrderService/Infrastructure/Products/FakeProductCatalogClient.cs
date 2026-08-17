namespace MicroShop.OrderService.Infrastructure.Products;

public sealed class FakeProductCatalogClient : IProductCatalogClient, IProductInventoryClient
{
    public Task<FakeProductResolution> ResolveAsync(
        IReadOnlyList<FakeProductRequestItem> requestedItems,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshots = new List<FakeProductSnapshot>(requestedItems.Count);
        foreach (var requestedItem in requestedItems)
        {
            if (!FakeProductCatalog.Products.TryGetValue(requestedItem.ProductId, out var product))
            {
                return Task.FromResult(new FakeProductResolution(
                    FakeProductFailure.NotFound,
                    requestedItem.ProductId,
                    null,
                    []));
            }

            if (!product.IsActive)
            {
                return Task.FromResult(new FakeProductResolution(
                    FakeProductFailure.Inactive,
                    product.Id,
                    product.AvailableStock,
                    []));
            }

            if (requestedItem.Quantity > product.AvailableStock)
            {
                return Task.FromResult(new FakeProductResolution(
                    FakeProductFailure.InsufficientStock,
                    product.Id,
                    product.AvailableStock,
                    []));
            }

            snapshots.Add(new FakeProductSnapshot(
                product.Id,
                product.Name,
                product.UnitPrice,
                requestedItem.Quantity,
                decimal.Round(product.UnitPrice * requestedItem.Quantity, 2, MidpointRounding.ToEven)));
        }

        return Task.FromResult(new FakeProductResolution(
            FakeProductFailure.None,
            null,
            null,
            snapshots));
    }

    public async Task<ProductReservationResult> ReserveAsync(
        ProductReservationRequest request,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveAsync(
            request.Items
                .Select(item => new FakeProductRequestItem(item.ProductId, item.Quantity))
                .ToArray(),
            cancellationToken);
        if (!resolution.IsSuccess)
        {
            return new ProductReservationResult(
                resolution.Failure switch
                {
                    FakeProductFailure.NotFound => ProductReservationFailure.ProductNotFound,
                    FakeProductFailure.Inactive => ProductReservationFailure.ProductInactive,
                    FakeProductFailure.InsufficientStock => ProductReservationFailure.InsufficientStock,
                    _ => ProductReservationFailure.InvalidResponse
                },
                null,
                [],
                0,
                resolution.ProductId,
                resolution.AvailableStock,
                "The deterministic fake Product catalog rejected the reservation.",
                false,
                false);
        }

        return new ProductReservationResult(
            ProductReservationFailure.None,
            request.OrderId,
            resolution.Items
                .Select(item => new ProductReservationSnapshot(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.Subtotal))
                .ToArray(),
            resolution.Items.Sum(item => item.Subtotal),
            null,
            null,
            null,
            true,
            false);
    }

    public Task<ProductReleaseResult> ReleaseAsync(
        ProductReleaseRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProductReleaseResult(
            ProductReservationFailure.None,
            request.OrderId,
            null,
            false));
    }
}
