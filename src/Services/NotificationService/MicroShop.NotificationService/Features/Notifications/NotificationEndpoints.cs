using MicroShop.NotificationService.Persistence;
using MicroShop.NotificationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.NotificationService.Features.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/notifications")
            .WithTags("Notifications");

        group.MapGet("", ListNotificationsAsync)
            .WithName("ListNotifications")
            .WithDescription("Lists generated notifications from the Notification database.")
            .Produces<NotificationPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapPost("/{notificationId:guid}/read", MarkAsReadAsync)
            .WithName("MarkNotificationAsRead")
            .Produces<NotificationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> ListNotificationsAsync(
        HttpContext httpContext,
        NotificationDbContext dbContext,
        int page = 1,
        int limit = 20,
        string? customerEmail = null,
        Guid? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = NotificationValidator.ValidateList(page, limit, customerEmail);
        if (validationErrors.Count > 0)
        {
            return NotificationProblems.Validation(httpContext, validationErrors);
        }

        var query = dbContext.Notifications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            var normalizedEmail = customerEmail.Trim().ToLowerInvariant();
            query = query.Where(notification => notification.CustomerEmail == normalizedEmail);
        }

        if (orderId.HasValue)
        {
            query = query.Where(notification => notification.OrderId == orderId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var notifications = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var response = new NotificationPageResponse(
            notifications.Select(ToResponse).ToArray(),
            page,
            limit,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit));

        return Results.Ok(response);
    }

    private static async Task<IResult> MarkAsReadAsync(
        Guid notificationId,
        HttpContext httpContext,
        NotificationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(candidate => candidate.Id == notificationId, cancellationToken);
        if (notification is null)
        {
            return NotificationProblems.NotFound(httpContext, notificationId);
        }

        notification.MarkAsRead(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(notification));
    }

    private static NotificationResponse ToResponse(Notification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.OrderId,
            notification.CustomerEmail,
            notification.Subject,
            notification.Body,
            notification.TotalAmount,
            notification.Currency,
            notification.IsRead,
            notification.CreatedAtUtc);
    }
}

internal static class NotificationProblems
{
    public static IResult Validation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return Problem(
            httpContext,
            StatusCodes.Status400BadRequest,
            "Validation error",
            "The notification query is invalid.",
            "VALIDATION_ERROR",
            errors);
    }

    public static IResult NotFound(HttpContext httpContext, Guid notificationId)
    {
        return Problem(
            httpContext,
            StatusCodes.Status404NotFound,
            "Notification not found",
            $"Notification '{notificationId}' was not found.",
            "NOTIFICATION_NOT_FOUND",
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
