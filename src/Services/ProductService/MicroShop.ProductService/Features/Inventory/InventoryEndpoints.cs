using System.Globalization;
using MicroShop.ProductService.Persistence;

namespace MicroShop.ProductService.Features.Inventory;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1/inventory")
            .WithTags("Internal Inventory");

        group.MapPost("/reservations", ReserveInventoryAsync)
            .WithName("ReserveInventory")
            .WithDescription("Internal service-to-service endpoint. This route must not be exposed through the public Gateway.")
            .Produces<InventoryReservationResponse>(StatusCodes.Status201Created)
            .Produces<InventoryReservationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/reservations/by-order/{orderId:guid}", GetReservationByOrderAsync)
            .WithName("GetInventoryReservationByOrder")
            .WithDescription("Internal reconciliation query. This route must not be exposed through the public Gateway.")
            .Produces<InventoryReservationQueryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/reservations/{orderId:guid}/release", ReleaseInventoryAsync)
            .WithName("ReleaseInventory")
            .WithDescription("Internal service-to-service endpoint. Release is idempotent by order ID.")
            .Produces<InventoryReleaseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetReservationByOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        InventoryReservationService reservationService,
        CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty)
        {
            return InventoryProblems.NotFound(httpContext, orderId);
        }

        var response = await reservationService.GetByOrderIdAsync(orderId, cancellationToken);
        return response is null
            ? InventoryProblems.NotFound(httpContext, orderId)
            : Results.Ok(response);
    }

    private static async Task<IResult> ReserveInventoryAsync(
        ReserveInventoryRequest? request,
        HttpContext httpContext,
        InventoryReservationService reservationService,
        CancellationToken cancellationToken)
    {
        var validationErrors = InventoryValidator.ValidateReserve(request);
        if (validationErrors.Count > 0)
        {
            return InventoryProblems.Validation(httpContext, validationErrors);
        }

        var outcome = await reservationService.ReserveAsync(request!, cancellationToken);
        if (!outcome.IsSuccess)
        {
            return InventoryProblems.Business(httpContext, outcome);
        }

        return outcome.Created
            ? Results.Created(
                $"/internal/v1/inventory/reservations/{outcome.Response!.ReservationId}",
                outcome.Response)
            : Results.Ok(outcome.Response);
    }

    private static async Task<IResult> ReleaseInventoryAsync(
        Guid orderId,
        HttpContext httpContext,
        InventoryReservationService reservationService,
        CancellationToken cancellationToken)
    {
        var outcome = await reservationService.ReleaseAsync(orderId, cancellationToken);
        if (!outcome.IsSuccess)
        {
            return InventoryProblems.Business(httpContext, outcome);
        }

        return Results.Ok(outcome.Response);
    }
}

internal static class InventoryProblems
{
    public static IResult NotFound(HttpContext httpContext, Guid orderId)
    {
        return Problem(
            httpContext,
            StatusCodes.Status404NotFound,
            "Reservation not found",
            $"No reservation exists for order '{orderId}'.",
            "RESERVATION_NOT_FOUND",
            new Dictionary<string, string[]>(StringComparer.Ordinal));
    }

    public static IResult Validation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return Problem(
            httpContext,
            StatusCodes.Status400BadRequest,
            "Validation error",
            "The inventory reservation request is invalid.",
            "VALIDATION_ERROR",
            errors);
    }

    public static IResult Business(
        HttpContext httpContext,
        InventoryReserveOutcome outcome)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (outcome.ProductId is not null)
        {
            errors["productId"] = [outcome.ProductId.Value.ToString()];
        }

        if (outcome.AvailableStock is not null)
        {
            errors["availableStock"] = [outcome.AvailableStock.Value.ToString(CultureInfo.InvariantCulture)];
        }

        return Problem(
            httpContext,
            outcome.StatusCode,
            outcome.Title,
            outcome.Detail,
            outcome.Code,
            errors);
    }

    public static IResult Business(
        HttpContext httpContext,
        InventoryReleaseOutcome outcome)
    {
        return Problem(
            httpContext,
            outcome.StatusCode,
            outcome.Title,
            outcome.Detail,
            outcome.Code,
            new Dictionary<string, string[]>(StringComparer.Ordinal));
    }

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string code,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            instance: httpContext.Request.Path,
            type: $"https://microshop.local/problems/{code.ToLowerInvariant().Replace('_', '-')}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier,
                ["errors"] = errors
            });
    }
}
