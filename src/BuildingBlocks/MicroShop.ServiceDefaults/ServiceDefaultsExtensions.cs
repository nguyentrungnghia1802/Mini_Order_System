using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MicroShop.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddMicroShopServiceDefaults(this IServiceCollection services)
    {
        services.AddOptions<MicroShopHostOptions>()
            .Configure(options =>
            {
                options.ShutdownTimeout = ParseShutdownTimeout(
                    Environment.GetEnvironmentVariable("MICROSHOP_SHUTDOWN_TIMEOUT_MS"));
            });
        services.AddOptions<HostOptions>()
            .Configure<IOptions<MicroShopHostOptions>>((hostOptions, microShopOptions) =>
            {
                hostOptions.ShutdownTimeout = microShopOptions.Value.ShutdownTimeout;
            });
        services.AddHealthChecks()
            .AddCheck<ApplicationLifecycleHealthCheck>(
                "application-lifecycle",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["readiness"]);
        return services;
    }

    public static IEndpointRouteBuilder MapMicroShopHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions());
        return endpoints;
    }

    private static TimeSpan ParseShutdownTimeout(string? value)
    {
        return int.TryParse(value, out var milliseconds)
            && milliseconds is >= 1_000 and <= 60_000
            ? TimeSpan.FromMilliseconds(milliseconds)
            : MicroShopHostOptions.DefaultShutdownTimeout;
    }
}

public sealed class MicroShopHostOptions
{
    public const int DefaultShutdownTimeoutMilliseconds = 10_000;

    public static TimeSpan DefaultShutdownTimeout =>
        TimeSpan.FromMilliseconds(DefaultShutdownTimeoutMilliseconds);

    public TimeSpan ShutdownTimeout { get; set; } = DefaultShutdownTimeout;
}

public sealed class ApplicationLifecycleHealthCheck(
    IHostApplicationLifetime applicationLifetime) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            applicationLifetime.ApplicationStopping.IsCancellationRequested
                ? HealthCheckResult.Unhealthy("The application is stopping.")
                : HealthCheckResult.Healthy());
    }
}
