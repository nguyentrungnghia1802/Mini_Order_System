using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Persistence;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Features.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/orders")
            .WithTags("Orders");

        group.MapPost("", CreateOrderAsync)
            .WithName("CreateOrder")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("", ListOrdersAsync)
            .WithName("ListOrders")
            .Produces<OrderPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/{orderId:guid}", GetOrderAsync)
            .WithName("GetOrder")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{orderId:guid}/cancel", CancelOrderAsync)
            .WithName("CancelOrder")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> CreateOrderAsync(
        CreateOrderRequest? request,
        HttpContext httpContext,
        OrderApplicationService applicationService,
        CancellationToken cancellationToken)
    {
        var validationErrors = OrderValidator.ValidateCreate(request);
        if (validationErrors.Count > 0)
        {
            return OrderProblems.Validation(httpContext, validationErrors);
        }

        var outcome = await applicationService.CreateAsync(
            request!,
            httpContext.TraceIdentifier,
            httpContext.Request.Headers.TraceParent.ToString(),
            cancellationToken);
        if (!outcome.IsSuccess)
        {
            return OrderProblems.Business(
                httpContext,
                outcome.StatusCode,
                outcome.Title,
                outcome.Detail,
                outcome.Code,
                outcome.Order.Id);
        }

        return Results.Created($"/api/v1/orders/{outcome.Order.Id}", ToResponse(outcome.Order));
    }

    private static async Task<IResult> ListOrdersAsync(
        HttpContext httpContext,
        OrderDbContext dbContext,
        int page = 1,
        int limit = 20,
        string? status = null,
        string? customerEmail = null,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = OrderValidator.ValidateList(page, limit, status, customerEmail);
        if (validationErrors.Count > 0)
        {
            return OrderProblems.Validation(httpContext, validationErrors);
        }

        var query = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(order => order.Status == status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            var normalizedEmail = customerEmail.Trim().ToLowerInvariant();
            query = query.Where(order => order.CustomerEmail == normalizedEmail);
        }

        var total = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var response = new OrderPageResponse(
            orders.Select(ToResponse).ToArray(),
            page,
            limit,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit));

        return Results.Ok(response);
    }

    private static async Task<IResult> GetOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        OrderDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        return order is null
            ? OrderProblems.NotFound(httpContext, orderId)
            : Results.Ok(ToResponse(order));
    }

    private static async Task<IResult> CancelOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        OrderApplicationService applicationService,
        CancellationToken cancellationToken)
    {
        var outcome = await applicationService.CancelAsync(
            orderId,
            httpContext.TraceIdentifier,
            httpContext.Request.Headers.TraceParent.ToString(),
            cancellationToken);
        if (outcome.Order is null)
        {
            return OrderProblems.NotFound(httpContext, orderId);
        }

        if (!outcome.IsSuccess)
        {
            return OrderProblems.Business(
                httpContext,
                outcome.StatusCode,
                outcome.Title,
                outcome.Detail,
                outcome.Code,
                outcome.Order.Id);
        }

        return Results.Ok(ToResponse(outcome.Order));
    }

    private static OrderResponse ToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.CustomerName,
            order.CustomerEmail,
            order.Status,
            order.Currency,
            order.TotalAmount,
            order.Items
                .OrderBy(item => item.ProductId)
                .Select(item => new OrderItemResponse(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.Subtotal))
                .ToArray(),
            order.Status == OrderStatuses.Confirmed,
            order.FailureCode,
            order.FailureDetail,
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            order.ConfirmedAtUtc,
            order.CancelledAtUtc,
            order.Version);
    }
}

internal static class OrderProblems
{
    public static IResult Validation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return Problem(
            httpContext,
            StatusCodes.Status400BadRequest,
            "Validation error",
            "The order request is invalid.",
            "VALIDATION_ERROR",
            errors);
    }

    public static IResult NotFound(HttpContext httpContext, Guid orderId)
    {
        return Problem(
            httpContext,
            StatusCodes.Status404NotFound,
            "Order not found",
            $"Order '{orderId}' was not found.",
            "ORDER_NOT_FOUND",
            new Dictionary<string, string[]>(StringComparer.Ordinal));
    }

    public static IResult Business(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string code,
        Guid orderId)
    {
        return Problem(
            httpContext,
            statusCode,
            title,
            detail,
            code,
            new Dictionary<string, string[]>(StringComparer.Ordinal),
            orderId);
    }

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string code,
        IReadOnlyDictionary<string, string[]> errors,
        Guid? orderId = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["traceId"] = httpContext.TraceIdentifier,
            ["errors"] = errors
        };
        if (orderId is not null)
        {
            extensions["orderId"] = orderId;
        }

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            instance: httpContext.Request.Path,
            type: $"https://microshop.local/problems/{code.ToLowerInvariant().Replace('_', '-')}",
            extensions: extensions);
    }
}
