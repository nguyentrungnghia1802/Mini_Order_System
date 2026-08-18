using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MicroShop.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IHostApplicationBuilder AddMicroShopServiceDefaults(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        builder.Services.Configure<ConsoleLoggerOptions>(options =>
        {
            options.FormatterName = ConsoleFormatterNames.Json;
        });
        builder.Services.Configure<JsonConsoleFormatterOptions>(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "O";
            options.UseUtcTimestamp = true;
        });

        var configuredServiceName = builder.Configuration["OTEL_SERVICE_NAME"];
        var resolvedServiceName = string.IsNullOrWhiteSpace(configuredServiceName)
            ? serviceName
            : configuredServiceName.Trim();
        var otlpEndpoint = ParseOtlpEndpoint(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        builder.Services.AddMicroShopServiceDefaults(
            resolvedServiceName,
            builder.Environment.EnvironmentName,
            otlpEndpoint);
        return builder;
    }

    public static IServiceCollection AddMicroShopServiceDefaults(
        this IServiceCollection services,
        string serviceName = "microshop-service",
        string? environmentName = null,
        Uri? otlpEndpoint = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var identity = new MicroShopServiceIdentity(
            serviceName.Trim(),
            string.IsNullOrWhiteSpace(environmentName) ? Environments.Production : environmentName.Trim(),
            typeof(ServiceDefaultsExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0");
        services.TryAddSingleton(identity);
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

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: identity.Name,
                    serviceVersion: identity.Version)
                .AddAttributes([
                    new KeyValuePair<string, object>(
                        "deployment.environment",
                        identity.Environment)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                    .AddHttpClientInstrumentation(options => options.RecordException = true)
                    .AddSource(MicroShopTelemetry.ActivitySourceName);
                if (otlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(MicroShopTelemetry.MeterName);
                if (otlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                }
            });
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

    private static Uri? ParseOtlpEndpoint(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps)
            ? endpoint
            : null;
    }
}

public sealed record MicroShopServiceIdentity(string Name, string Environment, string Version);

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
