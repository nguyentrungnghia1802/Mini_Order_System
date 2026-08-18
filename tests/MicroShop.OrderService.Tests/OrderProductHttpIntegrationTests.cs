using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Tests;

public sealed class OrderProductHttpIntegrationTests(OrderDatabaseFixture fixture)
    : IClassFixture<OrderDatabaseFixture>
{
    [Fact]
    public async Task OrderApiUsesProductHttpReservationAndPersistsSnapshots()
    {
        var productId = Guid.NewGuid();
        string? receivedTraceParent = null;
        using var productClient = CreateProductClient(async (request, cancellationToken) =>
        {
            receivedTraceParent = request.Headers.GetValues("traceparent").Single();
            var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var orderId = payload.GetProperty("orderId").GetGuid();
            return JsonResponse(HttpStatusCode.Created, new
            {
                reservationId = Guid.NewGuid(),
                orderId,
                status = "reserved",
                currency = "VND",
                totalAmount = 125_000m,
                items = new[]
                {
                    new
                    {
                        productId,
                        productName = "HTTP Product",
                        unitPrice = 125_000m,
                        quantity = 1,
                        subtotal = 125_000m
                    }
                },
                idempotentReplay = false
            });
        });
        using var orderClient = fixture.CreateClientUsingProduct(productClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.TryAddWithoutValidation(
            "traceparent",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        request.Content = JsonContent.Create(new
        {
            customerName = "HTTP Integration",
            customerEmail = $"{Guid.NewGuid():N}@example.com",
            items = new[] { new { productId, quantity = 1 } }
        });

        using var response = await orderClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(receivedTraceParent);
        Assert.StartsWith(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-",
            receivedTraceParent,
            StringComparison.Ordinal);
        Assert.EndsWith("-01", receivedTraceParent, StringComparison.Ordinal);
        Assert.NotEqual(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            receivedTraceParent);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orderIdFromResponse = body.GetProperty("id").GetGuid();
        Assert.Equal(OrderStatuses.Confirmed, body.GetProperty("status").GetString());
        Assert.Equal("HTTP Product", body.GetProperty("items")[0].GetProperty("productName").GetString());

        await using var dbContext = fixture.CreateDbContext();
        var order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.Id == orderIdFromResponse);
        Assert.Equal(OrderStatuses.Confirmed, order.Status);
        Assert.Equal("HTTP Product", order.Items.Single().ProductName);
    }

    [Fact]
    public async Task ProductHttpUnavailableReturnsStable503AndPersistsUnknownOrder()
    {
        using var productClient = CreateProductClient((_, _) =>
            throw new HttpRequestException("Product service stopped"));
        using var orderClient = fixture.CreateClientUsingProduct(productClient);

        using var response = await orderClient.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerName = "Unavailable Product",
                customerEmail = $"{Guid.NewGuid():N}@example.com",
                items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } }
            });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PRODUCT_SERVICE_UNAVAILABLE", problem.GetProperty("code").GetString());
        var orderId = problem.GetProperty("orderId").GetGuid();

        await using var dbContext = fixture.CreateDbContext();
        var order = await dbContext.Orders
            .Include(candidate => candidate.StateHistory)
            .SingleAsync(candidate => candidate.Id == orderId);
        Assert.Equal(OrderStatuses.InventoryUnknown, order.Status);
        Assert.Equal("PRODUCT_SERVICE_UNAVAILABLE", order.FailureCode);
        Assert.Equal(2, order.StateHistory.Count);
    }

    [Fact]
    public async Task ProductHttpTimeoutReturnsAmbiguous503AndPersistsUnknownOrder()
    {
        using var productClient = CreateProductClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var orderClient = fixture.CreateClientUsingProduct(productClient, timeoutMilliseconds: 25);

        using var response = await orderClient.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerName = "Timed Out Product",
                customerEmail = $"{Guid.NewGuid():N}@example.com",
                items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } }
            });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVENTORY_OUTCOME_UNKNOWN", problem.GetProperty("code").GetString());
        var orderId = problem.GetProperty("orderId").GetGuid();

        await using var dbContext = fixture.CreateDbContext();
        var order = await dbContext.Orders.SingleAsync(candidate => candidate.Id == orderId);
        Assert.Equal(OrderStatuses.InventoryUnknown, order.Status);
        Assert.Equal("INVENTORY_OUTCOME_UNKNOWN", order.FailureCode);
    }

    [Fact]
    public async Task CancellationReleasesReservationOnceAndRepeatedCancelIsIdempotent()
    {
        var productId = Guid.NewGuid();
        var releaseCalls = 0;
        using var productClient = CreateProductClient(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/release", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref releaseCalls);
                var pathSegments = request.RequestUri.AbsolutePath.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries);
                var orderId = Guid.Parse(pathSegments[^2]);
                return JsonResponse(HttpStatusCode.OK, new
                {
                    orderId,
                    reservationId = Guid.NewGuid(),
                    status = "released",
                    idempotentReplay = false,
                    releasedAtUtc = DateTimeOffset.UtcNow
                });
            }

            var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var orderIdFromReservation = payload.GetProperty("orderId").GetGuid();
            return JsonResponse(HttpStatusCode.Created, new
            {
                reservationId = Guid.NewGuid(),
                orderId = orderIdFromReservation,
                status = "reserved",
                currency = "VND",
                totalAmount = 100m,
                items = new[]
                {
                    new
                    {
                        productId,
                        productName = "Cancellable Product",
                        unitPrice = 100m,
                        quantity = 1,
                        subtotal = 100m
                    }
                },
                idempotentReplay = false
            });
        });
        using var orderClient = fixture.CreateClientUsingProduct(productClient);
        using var createResponse = await orderClient.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerName = "Cancellation User",
                customerEmail = $"{Guid.NewGuid():N}@example.com",
                items = new[] { new { productId, quantity = 1 } }
            });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("id").GetGuid();

        using var cancelResponse = await orderClient.PostAsync(
            $"/api/v1/orders/{orderId}/cancel",
            content: null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(OrderStatuses.Cancelled, cancelled.GetProperty("status").GetString());

        using var repeatedResponse = await orderClient.PostAsync(
            $"/api/v1/orders/{orderId}/cancel",
            content: null);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(1, releaseCalls);
    }

    [Fact]
    public async Task AmbiguousCancellationRetriesIdempotentReleaseButDoesNotStartAnotherCancellation()
    {
        var productId = Guid.NewGuid();
        var releaseCalls = 0;
        using var productClient = CreateProductClient(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/release", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref releaseCalls);
                throw new HttpRequestException("Product release connection lost");
            }

            var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var orderId = payload.GetProperty("orderId").GetGuid();
            return JsonResponse(HttpStatusCode.Created, new
            {
                reservationId = Guid.NewGuid(),
                orderId,
                status = "reserved",
                currency = "VND",
                totalAmount = 100m,
                items = new[]
                {
                    new
                    {
                        productId,
                        productName = "Pending Cancellation Product",
                        unitPrice = 100m,
                        quantity = 1,
                        subtotal = 100m
                    }
                },
                idempotentReplay = false
            });
        });
        using var orderClient = fixture.CreateClientUsingProduct(productClient);
        using var createResponse = await orderClient.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerName = "Pending Cancellation User",
                customerEmail = $"{Guid.NewGuid():N}@example.com",
                items = new[] { new { productId, quantity = 1 } }
            });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("id").GetGuid();

        using var cancelResponse = await orderClient.PostAsync(
            $"/api/v1/orders/{orderId}/cancel",
            content: null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, cancelResponse.StatusCode);
        var problem = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PRODUCT_SERVICE_UNAVAILABLE", problem.GetProperty("code").GetString());

        using var repeatedResponse = await orderClient.PostAsync(
            $"/api/v1/orders/{orderId}/cancel",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedResponse.StatusCode);
        Assert.Equal("ORDER_STATE_CONFLICT", (await repeatedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(2, releaseCalls);

        await using var dbContext = fixture.CreateDbContext();
        var order = await dbContext.Orders
            .Include(candidate => candidate.StateHistory)
            .SingleAsync(candidate => candidate.Id == orderId);
        Assert.Equal(OrderStatuses.CancellationPending, order.Status);
        Assert.Equal("PRODUCT_SERVICE_UNAVAILABLE", order.FailureCode);
        Assert.Equal(3, order.StateHistory.Count);
    }

    private static HttpClient CreateProductClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
    {
        return new HttpClient(new ProductStubHandler(callback))
        {
            BaseAddress = new Uri("http://product.stub/")
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed class ProductStubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return callback(request, cancellationToken);
        }
    }
}
