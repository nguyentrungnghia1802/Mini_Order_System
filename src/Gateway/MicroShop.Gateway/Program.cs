using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.AddMicroShopServiceDefaults(builder, "gateway");
MicroShop.Gateway.BootstrapConfiguration.AddYarp(builder.Services, builder.Configuration);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_048_576;
});

var app = builder.Build();

MicroShop.ServiceDefaults.RequestObservabilityExtensions.UseMicroShopRequestObservability(app);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.MapMicroShopHealth(app);
app.UseCors("frontend");
app.Use(async (httpContext, next) =>
{
    if (httpContext.Request.Path.StartsWithSegments("/internal"))
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://microshop.local/problems/gateway-route-not-found",
            title = "Gateway route not found",
            status = StatusCodes.Status404NotFound,
            detail = "Internal service routes are not exposed through the Gateway.",
            code = "GATEWAY_ROUTE_NOT_FOUND",
            traceId = httpContext.TraceIdentifier
        });
        return;
    }

    await next();
});
app.MapGet("/", () => Results.Ok(new
{
    service = "gateway",
    status = "running",
    message = "YARP Gateway routes Product, Order, and Notification public APIs."
}));
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (httpContext, next) =>
    {
        await next();
        var errorFeature = httpContext.GetForwarderErrorFeature();
        if (errorFeature is not null
            && (errorFeature.Error is ForwarderError.Request
                or ForwarderError.RequestTimedOut
                or ForwarderError.NoAvailableDestinations)
            && !httpContext.Response.HasStarted)
        {
            httpContext.Response.Clear();
            httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://microshop.local/problems/downstream-unavailable",
                title = "Downstream service unavailable",
                status = StatusCodes.Status502BadGateway,
                detail = "The requested downstream service is unavailable.",
                code = "DOWNSTREAM_UNAVAILABLE",
                traceId = httpContext.TraceIdentifier
            });
        }
    });
});

app.Run();

public partial class Program
{
}
