using MicroShop.OrderService.Features.Orders;

namespace MicroShop.OrderService.Features.Reconciliation;

public static class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1/reconciliation")
            .WithTags("Internal Reconciliation");

        group.MapPost("/orders/{orderId:guid}", ReconcileOrderAsync)
            .WithName("ReconcileOrder")
            .WithDescription("Internal/manual reconciliation path. This route must not be exposed through the public Gateway.")
            .Produces<OrderReconciliationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> ReconcileOrderAsync(
        Guid orderId,
        HttpContext httpContext,
        OrderReconciliationService reconciliationService,
        CancellationToken cancellationToken)
    {
        var outcome = await reconciliationService.ReconcileAsync(
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

        return Results.Ok(new OrderReconciliationResponse(
            OrderEndpoints.ToResponse(outcome.Order),
            outcome.Operation,
            outcome.Outcome,
            outcome.Changed,
            outcome.ReservationStatus,
            outcome.ReservationId,
            outcome.ReconciledAtUtc));
    }
}
