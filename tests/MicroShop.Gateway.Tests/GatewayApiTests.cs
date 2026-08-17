using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicroShop.Gateway.Tests;

public sealed class GatewayApiTests
{
    [Fact]
    public void InvalidDownstreamAddressFailsStartupConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PRODUCT_SERVICE_URL"] = "ftp://product.local/",
                ["ORDER_SERVICE_URL"] = "http://order.local/"
            })
            .Build();

        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(
            () => MicroShop.Gateway.BootstrapConfiguration.AddYarp(services, configuration));

        Assert.Contains("Product Service", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductRouteTransformsPathQueryAndTraceParent()
    {
        string? receivedPath = null;
        string? receivedTraceParent = null;
        await using var downstream = await DownstreamServer.StartAsync(async context =>
        {
            receivedPath = context.Request.Path + context.Request.QueryString;
            receivedTraceParent = context.Request.Headers.TraceParent.ToString();
            await context.Response.WriteAsJsonAsync(new { service = "product" });
        });
        await using var factory = new GatewayFactory(downstream.Address, downstream.Address);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/products?active=true&page=2");
        request.Headers.TryAddWithoutValidation(
            "traceparent",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/api/v1/products?active=true&page=2", receivedPath);
        Assert.NotNull(receivedTraceParent);
        Assert.StartsWith(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-",
            receivedTraceParent,
            StringComparison.Ordinal);
        Assert.EndsWith("-01", receivedTraceParent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderRouteTransformsCancellationPath()
    {
        string? receivedPath = null;
        await using var downstream = await DownstreamServer.StartAsync(async context =>
        {
            receivedPath = context.Request.Path;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            await context.Response.CompleteAsync();
        });
        await using var factory = new GatewayFactory(downstream.Address, downstream.Address);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/orders/11111111-1111-1111-1111-111111111111/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "/api/v1/orders/11111111-1111-1111-1111-111111111111/cancel",
            receivedPath);
    }

    [Fact]
    public async Task NotificationRouteTransformsPathAndQuery()
    {
        string? receivedPath = null;
        await using var downstream = await DownstreamServer.StartAsync(async context =>
        {
            receivedPath = context.Request.Path + context.Request.QueryString;
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.CompleteAsync();
        });
        await using var factory = new GatewayFactory(
            downstream.Address,
            downstream.Address,
            downstream.Address);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/notifications?customerEmail=a%40example.com&page=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "/api/v1/notifications?customerEmail=a%40example.com&page=2",
            receivedPath);
    }

    [Fact]
    public async Task InternalProductRouteIsRejectedWithoutForwarding()
    {
        var forwarded = false;
        await using var downstream = await DownstreamServer.StartAsync(context =>
        {
            forwarded = true;
            return Task.CompletedTask;
        });
        await using var factory = new GatewayFactory(downstream.Address, downstream.Address);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/internal/v1/inventory/reservations",
            new { orderId = Guid.NewGuid(), items = Array.Empty<object>() });
        var problem = await response.Content.ReadFromJsonAsync<GatewayProblem>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("GATEWAY_ROUTE_NOT_FOUND", problem!.Code);
        Assert.False(forwarded);
    }

    [Fact]
    public async Task DownstreamUnavailableReturnsStable502()
    {
        var address = GetUnusedAddress();
        await using var factory = new GatewayFactory(address, address);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/products");
        var problem = await response.Content.ReadFromJsonAsync<GatewayProblem>();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("DOWNSTREAM_UNAVAILABLE", problem!.Code);
    }

    [Fact]
    public async Task GatewayHealthAndCorsAreAvailable()
    {
        var address = GetUnusedAddress();
        await using var factory = new GatewayFactory(address, address);
        using var client = factory.CreateClient();

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        preflight.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");
        preflight.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");
        using var cors = await client.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cors.StatusCode);
        Assert.Equal("http://localhost:4200", cors.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    private static Uri GetUnusedAddress()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return new Uri($"http://127.0.0.1:{port}/");
    }

    private sealed record GatewayProblem(string Code);

    private sealed class GatewayFactory(
        Uri productAddress,
        Uri orderAddress,
        Uri? notificationAddress = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var actualNotificationAddress = notificationAddress ?? productAddress;
            builder.UseEnvironment("Testing");
            builder.UseSetting("PRODUCT_SERVICE_URL", productAddress.ToString());
            builder.UseSetting("ORDER_SERVICE_URL", orderAddress.ToString());
            builder.UseSetting("NOTIFICATION_SERVICE_URL", actualNotificationAddress.ToString());
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PRODUCT_SERVICE_URL"] = productAddress.ToString(),
                    ["ORDER_SERVICE_URL"] = orderAddress.ToString(),
                    ["NOTIFICATION_SERVICE_URL"] = actualNotificationAddress.ToString(),
                    ["Gateway:ProductServiceUrl"] = productAddress.ToString(),
                    ["Gateway:OrderServiceUrl"] = orderAddress.ToString(),
                    ["Gateway:NotificationServiceUrl"] = actualNotificationAddress.ToString()
                });
            });
        }
    }

    private sealed class DownstreamServer(WebApplication application, Uri address)
        : IAsyncDisposable
    {
        public Uri Address => address;

        public static async Task<DownstreamServer> StartAsync(
            RequestDelegate handler)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            var application = builder.Build();
            application.Run(handler);
            await application.StartAsync();

            var addresses = application.Services
                .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                .Features
                .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                .Addresses;
            return new DownstreamServer(application, new Uri(addresses.Single()));
        }

        public async ValueTask DisposeAsync()
        {
            await application.StopAsync();
            await application.DisposeAsync();
        }
    }
}
