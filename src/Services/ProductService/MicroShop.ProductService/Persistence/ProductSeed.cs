using MicroShop.ProductService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.ProductService.Persistence;

public static class ProductSeed
{
    private static readonly Product[] Products =
    [
        new Product
        {
            Id = new Guid("00000000-0000-0000-0000-000000000101"),
            Name = "Mechanical Keyboard",
            UnitPrice = 1_200_000m,
            Currency = "VND",
            AvailableStock = 10,
            IsActive = true
        },
        new Product
        {
            Id = new Guid("00000000-0000-0000-0000-000000000102"),
            Name = "Wireless Mouse",
            UnitPrice = 450_000m,
            Currency = "VND",
            AvailableStock = 20,
            IsActive = true
        },
        new Product
        {
            Id = new Guid("00000000-0000-0000-0000-000000000103"),
            Name = "USB-C Hub",
            UnitPrice = 800_000m,
            Currency = "VND",
            AvailableStock = 0,
            IsActive = true
        },
        new Product
        {
            Id = new Guid("00000000-0000-0000-0000-000000000104"),
            Name = "Archived Headset",
            UnitPrice = 600_000m,
            Currency = "VND",
            AvailableStock = 5,
            IsActive = false
        }
    ];

    public static async Task<int> SeedAsync(ProductDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var seedIds = Products.Select(product => product.Id).ToArray();
        var existingIds = await dbContext.Products
            .Where(product => seedIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToHashSetAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var missingProducts = Products
            .Where(product => !existingIds.Contains(product.Id))
            .Select(product =>
            {
                product.CreatedAtUtc = now;
                product.UpdatedAtUtc = now;
                product.Version = 1;
                return product;
            })
            .ToArray();

        if (missingProducts.Length == 0)
        {
            return 0;
        }

        await dbContext.Products.AddRangeAsync(missingProducts, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return missingProducts.Length;
    }
}
