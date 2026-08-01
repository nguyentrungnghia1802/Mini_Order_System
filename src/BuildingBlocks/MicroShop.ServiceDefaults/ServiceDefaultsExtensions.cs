using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MicroShop.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddMicroShopServiceDefaults(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }

    public static IEndpointRouteBuilder MapMicroShopHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        endpoints.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
        return endpoints;
    }
}
