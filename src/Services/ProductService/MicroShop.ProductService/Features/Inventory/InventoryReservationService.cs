using System.Security.Cryptography;
using System.Text;
using MicroShop.ProductService.Domain;
using MicroShop.ProductService.Persistence;
using MicroShop.ProductService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.ProductService.Features.Inventory;

public sealed class InventoryReservationService(ProductDbContext dbContext)
{
    public async Task<InventoryReserveOutcome> ReserveAsync(
        ReserveInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var requestHash = ComputeRequestHash(request.Items!);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireOrderLockAsync(request.OrderId, cancellationToken);

        var existing = await dbContext.InventoryReservations
            .Include(reservation => reservation.Items)
            .SingleOrDefaultAsync(reservation => reservation.OrderId == request.OrderId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing.RequestHash == requestHash
                ? existing.Status == InventoryReservationStatuses.Reserved
                    ? InventoryReserveOutcome.Success(ToResponse(existing, idempotentReplay: true), created: false)
                    : InventoryReserveOutcome.Failure(
                        StatusCodes.Status409Conflict,
                        "Reservation state conflict",
                        "RESERVATION_STATE_CONFLICT",
                        "The reservation was already released and cannot be reserved again.")
                : InventoryReserveOutcome.Failure(
                    StatusCodes.Status409Conflict,
                    "Reservation request mismatch",
                    "RESERVATION_REQUEST_MISMATCH",
                    "The same order ID was already used with a different item set.");
        }

        var requestedItems = request.Items!
            .OrderBy(item => item.ProductId)
            .ToArray();
        var products = await LockProductsAsync(
            requestedItems.Select(item => item.ProductId).ToArray(),
            cancellationToken);
        var productsById = products.ToDictionary(product => product.Id);

        foreach (var requestedItem in requestedItems)
        {
            if (!productsById.ContainsKey(requestedItem.ProductId))
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryReserveOutcome.Failure(
                    StatusCodes.Status404NotFound,
                    "Product not found",
                    "PRODUCT_NOT_FOUND",
                    $"Product '{requestedItem.ProductId}' was not found.",
                    requestedItem.ProductId);
            }
        }

        foreach (var requestedItem in requestedItems)
        {
            var product = productsById[requestedItem.ProductId];
            if (!product.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryReserveOutcome.Failure(
                    StatusCodes.Status409Conflict,
                    "Product inactive",
                    "PRODUCT_INACTIVE",
                    $"Product '{product.Id}' is inactive.",
                    product.Id);
            }

            if (requestedItem.Quantity > product.AvailableStock)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryReserveOutcome.Failure(
                    StatusCodes.Status409Conflict,
                    "Insufficient stock",
                    "INSUFFICIENT_STOCK",
                    $"Product '{product.Id}' has only {product.AvailableStock} unit(s) available.",
                    product.Id,
                    product.AvailableStock);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var snapshots = requestedItems
            .Select(requestedItem =>
            {
                var product = productsById[requestedItem.ProductId];
                return new ProductReservationSnapshot(
                    product,
                    requestedItem.Quantity,
                    decimal.Round(
                        product.UnitPrice * requestedItem.Quantity,
                        2,
                        MidpointRounding.ToEven));
            })
            .ToArray();
        var total = decimal.Round(
            snapshots.Sum(snapshot => snapshot.Subtotal),
            2,
            MidpointRounding.ToEven);

        var reservation = InventoryReservation.Create(
            Guid.NewGuid(),
            request.OrderId,
            requestHash,
            total,
            now);
        foreach (var snapshot in snapshots)
        {
            reservation.AddItem(InventoryReservationItem.Create(
                snapshot.Product.Id,
                snapshot.Product.Name,
                snapshot.Product.UnitPrice,
                snapshot.Quantity));

            snapshot.Product.AvailableStock -= snapshot.Quantity;
            snapshot.Product.UpdatedAtUtc = now;
            snapshot.Product.Version++;
        }

        dbContext.InventoryReservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return InventoryReserveOutcome.Success(ToResponse(reservation, idempotentReplay: false), created: true);
    }

    public async Task<InventoryReleaseOutcome> ReleaseAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireOrderLockAsync(orderId, cancellationToken);

        var reservation = await dbContext.InventoryReservations
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(candidate => candidate.OrderId == orderId, cancellationToken);
        if (reservation is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InventoryReleaseOutcome.Failure(
                StatusCodes.Status404NotFound,
                "Reservation not found",
                "RESERVATION_NOT_FOUND",
                $"No reservation exists for order '{orderId}'.");
        }

        if (reservation.Status == InventoryReservationStatuses.Released)
        {
            await transaction.CommitAsync(cancellationToken);
            return InventoryReleaseOutcome.Success(ToReleaseResponse(reservation, idempotentReplay: true));
        }

        var productIds = reservation.Items
            .OrderBy(item => item.ProductId)
            .Select(item => item.ProductId)
            .ToArray();
        var products = await LockProductsAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(product => product.Id);
        foreach (var item in reservation.Items)
        {
            if (!productsById.ContainsKey(item.ProductId))
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryReleaseOutcome.Failure(
                    StatusCodes.Status409Conflict,
                    "Reservation state conflict",
                    "RESERVATION_STATE_CONFLICT",
                    $"Product '{item.ProductId}' for reservation '{reservation.Id}' no longer exists.");
            }
        }

        var releasedAtUtc = TruncateToPostgresPrecision(DateTimeOffset.UtcNow);
        foreach (var item in reservation.Items)
        {
            var product = productsById[item.ProductId];
            product.AvailableStock = checked(product.AvailableStock + item.Quantity);
            product.UpdatedAtUtc = releasedAtUtc;
            product.Version++;
        }

        reservation.Release(releasedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return InventoryReleaseOutcome.Success(ToReleaseResponse(reservation, idempotentReplay: false));
    }

    public async Task<InventoryReservationQueryResponse?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.InventoryReservations
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(candidate => candidate.OrderId == orderId, cancellationToken);

        return reservation is null ? null : ToQueryResponse(reservation);
    }

    private async Task<List<Product>> LockProductsAsync(
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .FromSqlInterpolated($"SELECT * FROM products WHERE id = ANY ({productIds.ToArray()}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    private async Task AcquireOrderLockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var orderIdText = orderId.ToString("D");
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({orderIdText}, 0))",
            cancellationToken);
    }

    private static string ComputeRequestHash(IReadOnlyList<ReserveInventoryItemRequest> items)
    {
        var canonical = string.Join(
            "|",
            items
                .OrderBy(item => item.ProductId)
                .Select(item => $"{item.ProductId:D}:{item.Quantity}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"v1|{canonical}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static DateTimeOffset TruncateToPostgresPrecision(DateTimeOffset value)
    {
        var ticks = value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond;
        return new DateTimeOffset(ticks, value.Offset);
    }

    private static InventoryReservationResponse ToResponse(
        InventoryReservation reservation,
        bool idempotentReplay)
    {
        return new InventoryReservationResponse(
            reservation.Id,
            reservation.OrderId,
            reservation.Status,
            reservation.Currency,
            reservation.TotalAmount,
            reservation.Items
                .OrderBy(item => item.ProductId)
                .Select(item => new InventoryReservationItemResponse(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.Subtotal))
                .ToArray(),
            idempotentReplay);
    }

    private static InventoryReleaseResponse ToReleaseResponse(
        InventoryReservation reservation,
        bool idempotentReplay)
    {
        return new InventoryReleaseResponse(
            reservation.OrderId,
            reservation.Id,
            reservation.Status,
            idempotentReplay,
            reservation.ReleasedAtUtc);
    }

    private static InventoryReservationQueryResponse ToQueryResponse(
        InventoryReservation reservation)
    {
        return new InventoryReservationQueryResponse(
            reservation.Id,
            reservation.OrderId,
            reservation.Status,
            reservation.Currency,
            reservation.TotalAmount,
            reservation.Items
                .OrderBy(item => item.ProductId)
                .Select(item => new InventoryReservationItemResponse(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.Subtotal))
                .ToArray(),
            reservation.CreatedAtUtc,
            reservation.ReleasedAtUtc);
    }

    private sealed record ProductReservationSnapshot(
        Product Product,
        int Quantity,
        decimal Subtotal);
}

public sealed record InventoryReserveOutcome(
    bool IsSuccess,
    bool Created,
    InventoryReservationResponse? Response,
    int StatusCode,
    string Title,
    string Detail,
    string Code,
    Guid? ProductId = null,
    int? AvailableStock = null)
{
    public static InventoryReserveOutcome Success(
        InventoryReservationResponse response,
        bool created)
    {
        return new(true, created, response, 0, string.Empty, string.Empty, string.Empty);
    }

    public static InventoryReserveOutcome Failure(
        int statusCode,
        string title,
        string code,
        string detail,
        Guid? productId = null,
        int? availableStock = null)
    {
        return new(false, false, null, statusCode, title, detail, code, productId, availableStock);
    }
}

public sealed record InventoryReleaseOutcome(
    bool IsSuccess,
    InventoryReleaseResponse? Response,
    int StatusCode,
    string Title,
    string Detail,
    string Code)
{
    public static InventoryReleaseOutcome Success(InventoryReleaseResponse response)
    {
        return new(true, response, 0, string.Empty, string.Empty, string.Empty);
    }

    public static InventoryReleaseOutcome Failure(
        int statusCode,
        string title,
        string code,
        string detail)
    {
        return new(false, null, statusCode, title, detail, code);
    }
}
