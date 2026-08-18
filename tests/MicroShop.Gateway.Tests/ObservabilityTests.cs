using System.Diagnostics;
using System.Diagnostics.Metrics;
using MicroShop.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;

namespace MicroShop.Gateway.Tests;

public sealed class ObservabilityTests
{
    [Fact]
    public void ServiceDefaultsRegisterIdentityAndForceW3CActivityIds()
    {
        var services = new ServiceCollection();
        services.AddMicroShopServiceDefaults("gateway-test", "Testing");
        using var provider = services.BuildServiceProvider();

        var identity = provider.GetRequiredService<MicroShopServiceIdentity>();

        Assert.Equal("gateway-test", identity.Name);
        Assert.Equal("Testing", identity.Environment);
        Assert.Equal(ActivityIdFormat.W3C, Activity.DefaultIdFormat);
    }

    [Fact]
    public void ConsumerSpanKeepsTheIncomingW3CTraceAndCreatesANewSpan()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MicroShopTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var incoming = new Activity("incoming");
        incoming.SetIdFormat(ActivityIdFormat.W3C);
        incoming.Start();
        Assert.NotNull(incoming.Id);
        Assert.True(ActivityContext.TryParse(incoming.Id, null, true, out var parentContext));

        using var consumer = MicroShopTelemetry.ActivitySource.StartActivity(
            "microshop.order_confirmed.consume",
            ActivityKind.Consumer,
            parentContext);

        Assert.NotNull(consumer);
        Assert.Equal(incoming.TraceId, consumer.TraceId);
        Assert.NotEqual(incoming.SpanId, consumer.SpanId);
        Assert.Equal(ActivityKind.Consumer, consumer.Kind);
    }

    [Fact]
    public void DomainMetersUseBoundedOperationAndResultTags()
    {
        var observations = new List<(string Name, string? Operation, string? Result)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == MicroShopTelemetry.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            string? operation = null;
            string? result = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "operation")
                {
                    operation = tag.Value?.ToString();
                }
                else if (tag.Key == "result")
                {
                    result = tag.Value?.ToString();
                }
            }

            observations.Add((instrument.Name, operation, result));
        });
        listener.Start();

        MicroShopTelemetry.OrderOutcomes.Add(
            1,
            MicroShopTelemetry.Tags("create", "confirmed"));
        MicroShopTelemetry.ReservationResults.Add(
            1,
            MicroShopTelemetry.Tags("reserve", "success"));

        Assert.Contains(
            observations,
            observation => observation.Name == "microshop.order.outcomes"
                && observation.Operation == "create"
                && observation.Result == "confirmed");
        Assert.Contains(
            observations,
            observation => observation.Name == "microshop.inventory.reservation.results"
                && observation.Operation == "reserve"
                && observation.Result == "success");
    }
}
