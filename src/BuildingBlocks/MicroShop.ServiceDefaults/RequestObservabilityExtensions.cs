using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MicroShop.ServiceDefaults;

public static class RequestObservabilityExtensions
{
    public static IApplicationBuilder UseMicroShopRequestObservability(
        this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);

        return application.Use(async (httpContext, next) =>
        {
            var identity = httpContext.RequestServices.GetRequiredService<MicroShopServiceIdentity>();
            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("MicroShop.Http");
            var scopeValues = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["service.name"] = identity.Name,
                ["deployment.environment"] = identity.Environment,
                ["trace.id"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
                ["span.id"] = Activity.Current?.SpanId.ToString() ?? string.Empty
            };
            AddRouteIdentity(scopeValues, httpContext, "orderId", "order.id");
            AddRouteIdentity(scopeValues, httpContext, "reservationId", "reservation.id");
            AddRouteIdentity(scopeValues, httpContext, "notificationId", "notification.id");

            using var scope = logger.BeginScope(scopeValues);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next();
                stopwatch.Stop();
                MicroShopRequestLog.Completed(
                    logger,
                    "HTTP_REQUEST_COMPLETED",
                    httpContext.Request.Method,
                    httpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    httpContext.Response.StatusCode,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                MicroShopRequestLog.Failed(
                    logger,
                    exception,
                    "HTTP_REQUEST_FAILED",
                    httpContext.Request.Method,
                    httpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    httpContext.Response.StatusCode,
                    stopwatch.Elapsed.TotalMilliseconds);
                throw;
            }
        });
    }

    private static void AddRouteIdentity(
        Dictionary<string, object?> scopeValues,
        HttpContext httpContext,
        string routeKey,
        string logKey)
    {
        if (httpContext.Request.RouteValues.TryGetValue(routeKey, out var value)
            && value is not null
            && Guid.TryParse(value.ToString(), out var id))
        {
            scopeValues[logKey] = id;
        }
    }
}

internal static partial class MicroShopRequestLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "HTTP request completed. EventCode={EventCode} Method={Method} Endpoint={Endpoint} StatusCode={StatusCode} DurationMs={DurationMs}")]
    public static partial void Completed(
        ILogger logger,
        string eventCode,
        string method,
        string endpoint,
        int statusCode,
        double durationMs);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "HTTP request failed. EventCode={EventCode} Method={Method} Endpoint={Endpoint} StatusCode={StatusCode} DurationMs={DurationMs}")]
    public static partial void Failed(
        ILogger logger,
        Exception exception,
        string eventCode,
        string method,
        string endpoint,
        int statusCode,
        double durationMs);
}
