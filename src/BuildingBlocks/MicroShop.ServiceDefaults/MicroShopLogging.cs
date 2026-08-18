using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MicroShop.ServiceDefaults;

public static class MicroShopLogging
{
    public static IDisposable BeginScope(
        ILogger logger,
        MicroShopServiceIdentity? identity,
        Activity? activity = null,
        Guid? orderId = null,
        Guid? reservationId = null,
        Guid? messageId = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        activity ??= Activity.Current;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["service.name"] = identity?.Name ?? "microshop-service",
            ["deployment.environment"] = identity?.Environment ?? "unknown",
            ["trace.id"] = activity?.TraceId.ToString() ?? string.Empty,
            ["span.id"] = activity?.SpanId.ToString() ?? string.Empty
        };
        if (orderId.HasValue)
        {
            values["order.id"] = orderId.Value;
        }

        if (reservationId.HasValue)
        {
            values["reservation.id"] = reservationId.Value;
        }

        if (messageId.HasValue)
        {
            values["message.id"] = messageId.Value;
        }

        return logger.BeginScope(values)!;
    }
}
