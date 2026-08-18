using MicroShop.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MicroShop.Gateway.Tests;

public sealed class ServiceReadinessTests
{
    [Fact]
    public async Task ApplicationLifecycleHealthCheckBecomesUnhealthyWhenStoppingStarts()
    {
        using var lifetime = new TestApplicationLifetime();
        var healthCheck = new ApplicationLifecycleHealthCheck(lifetime);

        var healthy = await healthCheck.CheckHealthAsync(new());
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, healthy.Status);

        lifetime.BeginStopping();

        var stopping = await healthCheck.CheckHealthAsync(new());
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, stopping.Status);
    }

    [Fact]
    public void ServiceDefaultsUseOneToSixtySecondBoundedShutdownTimeout()
    {
        var services = new ServiceCollection();
        services.AddMicroShopServiceDefaults();
        using var provider = services.BuildServiceProvider();

        var microShopOptions = provider.GetRequiredService<IOptions<MicroShopHostOptions>>().Value;
        var hostOptions = provider.GetRequiredService<IOptions<HostOptions>>().Value;

        Assert.InRange(
            microShopOptions.ShutdownTimeout,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(60));
        Assert.Equal(microShopOptions.ShutdownTimeout, hostOptions.ShutdownTimeout);
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => started.Token;

        public CancellationToken ApplicationStopping => stopping.Token;

        public CancellationToken ApplicationStopped => stopped.Token;

        public void BeginStopping() => stopping.Cancel();

        public void StopApplication() => BeginStopping();

        public void Dispose()
        {
            started.Dispose();
            stopping.Dispose();
            stopped.Dispose();
        }
    }
}
