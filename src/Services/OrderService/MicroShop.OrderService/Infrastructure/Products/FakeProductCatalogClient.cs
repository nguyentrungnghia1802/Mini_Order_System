namespace MicroShop.OrderService.Infrastructure.Products;

public sealed class FakeProductCatalogClient : IProductCatalogClient
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
}
