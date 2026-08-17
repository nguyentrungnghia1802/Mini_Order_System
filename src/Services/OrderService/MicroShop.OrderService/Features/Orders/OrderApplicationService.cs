using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Features.Orders;

public sealed class OrderApplicationService(
    OrderDbContext dbContext,
    IProductCatalogClient productCatalogClient)
{
    public async Task<CreateOrderOutcome> CreateAsync(
        CreateOrderRequest request,
        string traceId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(
            Guid.NewGuid(),
            request.CustomerName!.Trim(),
            request.CustomerEmail!.Trim(),
            now);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var requestedItems = request.Items!
            .Select(item => new FakeProductRequestItem(item.ProductId, item.Quantity))
            .ToArray();
        var resolution = await productCatalogClient.ResolveAsync(requestedItems, cancellationToken);

        if (!resolution.IsSuccess)
        {
            var failure = MapFailure(resolution);
            return await RejectAsync(
                order,
                failure.Code,
                failure.Detail,
                traceId,
                cancellationToken,
                failure.StatusCode,
                failure.Title);
        }

        var total = decimal.Round(
            resolution.Items.Sum(item => item.Subtotal),
            2,
            MidpointRounding.ToEven);
        if (total > OrderValidator.MaxOrderTotal)
        {
            return await RejectAsync(
                order,
                "ORDER_TOTAL_LIMIT_EXCEEDED",
                $"The maximum order total is {OrderValidator.MaxOrderTotal:0.##} VND.",
                traceId,
                cancellationToken,
                StatusCodes.Status400BadRequest,
                "Validation error");
        }

        foreach (var snapshot in resolution.Items)
        {
            order.AddItem(OrderItem.Create(
                snapshot.ProductId,
                snapshot.ProductName,
                snapshot.UnitPrice,
                snapshot.Quantity));
        }

        order.TransitionTo(
            OrderStatuses.Confirmed,
            "FAKE_PRODUCT_CONFIRMED",
            traceId,
            DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateOrderOutcome.Success(order);
    }

    private async Task<CreateOrderOutcome> RejectAsync(
        Order order,
        string code,
        string detail,
        string traceId,
        CancellationToken cancellationToken,
        int statusCode = StatusCodes.Status409Conflict,
        string title = "Order rejected")
    {
        var now = DateTimeOffset.UtcNow;
        order.RecordFailure(code, detail, now);
        order.TransitionTo(OrderStatuses.Rejected, code, traceId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreateOrderOutcome.Failure(order, statusCode, title, detail, code);
    }

    private static (int StatusCode, string Title, string Code, string Detail) MapFailure(
        FakeProductResolution resolution)
    {
        return resolution.Failure switch
        {
            FakeProductFailure.NotFound => (
                StatusCodes.Status404NotFound,
                "Product not found",
                "PRODUCT_NOT_FOUND",
                $"Product '{resolution.ProductId}' was not found in the fake Product catalog."),
            FakeProductFailure.Inactive => (
                StatusCodes.Status409Conflict,
                "Product inactive",
                "PRODUCT_INACTIVE",
                $"Product '{resolution.ProductId}' is inactive in the fake Product catalog."),
            FakeProductFailure.InsufficientStock => (
                StatusCodes.Status409Conflict,
                "Insufficient stock",
                "INSUFFICIENT_STOCK",
                $"Product '{resolution.ProductId}' has only {resolution.AvailableStock} unit(s) available."),
            _ => throw new InvalidOperationException("The fake Product resolution is successful.")
        };
    }
}

public sealed record CreateOrderOutcome(
    bool IsSuccess,
    Order Order,
    int StatusCode,
    string Title,
    string Detail,
    string Code)
{
    public static CreateOrderOutcome Success(Order order)
    {
        return new(true, order, StatusCodes.Status201Created, "", "", "");
    }

    public static CreateOrderOutcome Failure(
        Order order,
        int statusCode,
        string title,
        string detail,
        string code)
    {
        return new(false, order, statusCode, title, detail, code);
    }
}
