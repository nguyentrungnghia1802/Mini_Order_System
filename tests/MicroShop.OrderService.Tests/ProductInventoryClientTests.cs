using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroShop.OrderService.Infrastructure.Products;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MicroShop.OrderService.Tests;

public sealed class ProductInventoryClientTests
{
    [Fact]
    public async Task ReserveSendsInternalRequestAndParsesAuthoritativeSnapshot()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "http://product.test/internal/v1/inventory/reservations",
                request.RequestUri!.ToString());
            Assert.Equal(
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                request.Headers.GetValues("traceparent").Single());

            var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Equal(orderId, payload.GetProperty("orderId").GetGuid());
            Assert.Equal(productId, payload.GetProperty("items")[0].GetProperty("productId").GetGuid());
            Assert.Equal(2, payload.GetProperty("items")[0].GetProperty("quantity").GetInt32());

            return JsonResponse(
                HttpStatusCode.Created,
                new
                {
                    reservationId = Guid.NewGuid(),
                    orderId,
                    status = "reserved",
                    currency = "VND",
                    totalAmount = 2_500_000m,
                    items = new[]
                    {
                        new
                        {
                            productId,
                            productName = "Keyboard",
                            unitPrice = 1_250_000m,
                            quantity = 2,
                            subtotal = 2_500_000m
                        }
                    },
                    idempotentReplay = false
                });
        });
        var client = CreateClient(handler);

        var result = await client.ReserveAsync(
            new ProductReservationRequest(
                orderId,
                [new ProductReservationRequestItem(productId, 2)],
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IdempotentReplay);
        Assert.Equal(2_500_000m, result.TotalAmount);
        Assert.Single(result.Items);
        Assert.Equal("Keyboard", result.Items[0].ProductName);
    }

    [Fact]
    public async Task ReleaseUsesOrderIdentityAndParsesIdempotentResponse()
    {
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(
                $"http://product.test/internal/v1/inventory/reservations/{orderId:D}/release",
                request.RequestUri!.ToString());
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                new
                {
                    orderId,
                    reservationId,
                    status = "released",
                    idempotentReplay = true,
                    releasedAtUtc = DateTimeOffset.UtcNow
                }));
        });
        var client = CreateClient(handler);

        var result = await client.ReleaseAsync(
            new ProductReleaseRequest(orderId, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.IdempotentReplay);
        Assert.Equal(reservationId, result.ReservationId);
    }

    [Fact]
    public async Task ReleaseRetriesTransientHttpFailureWithSameOrderIdentity()
    {
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var attempts = 0;
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(
                $"http://product.test/internal/v1/inventory/reservations/{orderId:D}/release",
                request.RequestUri!.ToString());
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new HttpRequestException("transient connection failure");
            }

            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                new
                {
                    orderId,
                    reservationId,
                    status = "released",
                    idempotentReplay = false,
                    releasedAtUtc = DateTimeOffset.UtcNow
                }));
        });
        var client = CreateClient(
            handler,
            safeRetryCount: 1,
            safeRetryDelayMilliseconds: 10);

        var result = await client.ReleaseAsync(
            new ProductReleaseRequest(orderId, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, attempts);
        Assert.Equal(reservationId, result.ReservationId);
    }

    [Fact]
    public async Task LookupSendsOrderIdentityAndParsesReservedReservation()
    {
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"http://product.test/internal/v1/inventory/reservations/by-order/{orderId:D}",
                request.RequestUri!.ToString());
            Assert.Equal(
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                request.Headers.GetValues("traceparent").Single());
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                new
                {
                    reservationId,
                    orderId,
                    status = "reserved",
                    currency = "VND",
                    totalAmount = 250_000m,
                    items = new[]
                    {
                        new
                        {
                            productId,
                            productName = "Keyboard",
                            unitPrice = 125_000m,
                            quantity = 2,
                            subtotal = 250_000m
                        }
                    },
                    createdAtUtc,
                    releasedAtUtc = (DateTimeOffset?)null
                }));
        });
        var client = CreateClient(handler);

        var result = await client.GetReservationByOrderAsync(
            new ProductReservationLookupRequest(
                orderId,
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(reservationId, result.ReservationId);
        Assert.Equal("reserved", result.Status);
        Assert.Equal("VND", result.Currency);
        Assert.Equal(createdAtUtc, result.CreatedAtUtc);
        Assert.Equal(250_000m, result.TotalAmount);
        Assert.Equal(productId, Assert.Single(result.Items).ProductId);
    }

    [Fact]
    public async Task LookupRetriesTransientServerResponse()
    {
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var attempts = 0;
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(
                $"http://product.test/internal/v1/inventory/reservations/by-order/{orderId:D}",
                request.RequestUri!.ToString());
            if (Interlocked.Increment(ref attempts) == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                new
                {
                    reservationId,
                    orderId,
                    status = "reserved",
                    currency = "VND",
                    totalAmount = 250_000m,
                    items = new[]
                    {
                        new
                        {
                            productId,
                            productName = "Keyboard",
                            unitPrice = 125_000m,
                            quantity = 2,
                            subtotal = 250_000m
                        }
                    },
                    createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                    releasedAtUtc = (DateTimeOffset?)null
                }));
        });
        var client = CreateClient(
            handler,
            safeRetryCount: 1,
            safeRetryDelayMilliseconds: 10);

        var result = await client.GetReservationByOrderAsync(
            new ProductReservationLookupRequest(orderId, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, attempts);
        Assert.Equal(reservationId, result.ReservationId);
    }

    [Fact]
    public async Task LookupMapsReservationNotFoundProblem()
    {
        var orderId = Guid.NewGuid();
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.NotFound,
            new
            {
                title = "Reservation not found",
                status = 404,
                detail = "No reservation exists for this order.",
                code = "RESERVATION_NOT_FOUND"
            })));
        var client = CreateClient(handler);

        var result = await client.GetReservationByOrderAsync(
            new ProductReservationLookupRequest(orderId, null),
            CancellationToken.None);

        Assert.True(result.IsReservationMissing);
        Assert.Equal(ProductReservationFailure.ReservationNotFound, result.Failure);
        Assert.Equal("No reservation exists for this order.", result.Detail);
    }

    [Fact]
    public async Task LookupRejectsMalformedSuccessfulResponse()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"reserved\"}")
            }));
        var client = CreateClient(handler);

        var result = await client.GetReservationByOrderAsync(
            new ProductReservationLookupRequest(Guid.NewGuid(), null),
            CancellationToken.None);

        Assert.Equal(ProductReservationFailure.InvalidResponse, result.Failure);
    }

    [Fact]
    public async Task ReserveMapsProductBusinessProblemDetails()
    {
        var productId = Guid.NewGuid();
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.Conflict,
            new
            {
                type = "https://microshop.local/problems/insufficient-stock",
                title = "Insufficient stock",
                status = 409,
                detail = "Only 3 unit(s) are available.",
                code = "INSUFFICIENT_STOCK",
                errors = new
                {
                    productId = new[] { productId.ToString() },
                    availableStock = new List<string> { "3" }
                }
            })));
        var client = CreateClient(handler);

        var result = await client.ReserveAsync(
            new ProductReservationRequest(
                Guid.NewGuid(),
                [new ProductReservationRequestItem(productId, 4)],
                null),
            CancellationToken.None);

        Assert.Equal(ProductReservationFailure.InsufficientStock, result.Failure);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal(3, result.AvailableStock);
        Assert.Equal("Only 3 unit(s) are available.", result.Detail);
    }

    [Fact]
    public async Task ReserveMapsUnavailableDependency()
    {
        var handler = new StubHandler((_, _) =>
            throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var result = await client.ReserveAsync(
            new ProductReservationRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)], null),
            CancellationToken.None);

        Assert.Equal(ProductReservationFailure.DependencyUnavailable, result.Failure);
        Assert.False(result.IsAmbiguous);
    }

    [Fact]
    public async Task ReserveDoesNotRetryAfterTransientFailure()
    {
        var attempts = 0;
        var handler = new StubHandler((_, _) =>
        {
            Interlocked.Increment(ref attempts);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var client = CreateClient(
            handler,
            safeRetryCount: 1,
            safeRetryDelayMilliseconds: 10);

        var result = await client.ReserveAsync(
            new ProductReservationRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)], null),
            CancellationToken.None);

        Assert.Equal(ProductReservationFailure.DependencyUnavailable, result.Failure);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ReserveMapsTimeoutToAmbiguousOutcome()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler, timeoutMilliseconds: 20);

        var result = await client.ReserveAsync(
            new ProductReservationRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)], null),
            CancellationToken.None);

        Assert.Equal(ProductReservationFailure.OutcomeUnknown, result.Failure);
        Assert.True(result.IsAmbiguous);
    }

    [Fact]
    public async Task ReleaseMapsTimeoutToAmbiguousOutcome()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler, timeoutMilliseconds: 20);

        var result = await client.ReleaseAsync(
            new ProductReleaseRequest(Guid.NewGuid(), null),
            CancellationToken.None);

        Assert.Equal(ProductReservationFailure.OutcomeUnknown, result.Failure);
        Assert.True(result.IsAmbiguous);
    }

    [Fact]
    public async Task ReservePreservesCallerCancellation()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler, timeoutMilliseconds: 5_000);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReserveAsync(
            new ProductReservationRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)], null),
            cancellation.Token));
    }

    [Fact]
    public async Task ReleasePreservesCallerCancellationDuringRetryableOperation()
    {
        var attempts = 0;
        var handler = new StubHandler((_, _) =>
        {
            Interlocked.Increment(ref attempts);
            throw new HttpRequestException("transient connection failure");
        });
        var client = CreateClient(
            handler,
            timeoutMilliseconds: 5_000,
            safeRetryCount: 1,
            safeRetryDelayMilliseconds: 10);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReleaseAsync(
            new ProductReleaseRequest(Guid.NewGuid(), null),
            cancellation.Token));
    }

    [Fact]
    public async Task ReserveRejectsMalformedSuccessfulResponse()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"reserved\"}")
            }));
        var client = CreateClient(handler);

        var result = await client.ReserveAsync(
            new ProductReservationRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)], null),
            CancellationToken.None);

        Assert.Equal(ProductReservationFailure.InvalidResponse, result.Failure);
    }

    private static ProductInventoryClient CreateClient(
        HttpMessageHandler handler,
        int timeoutMilliseconds = 5_000,
        int safeRetryCount = 1,
        int safeRetryDelayMilliseconds = 100)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://product.test/")
        };
        return new ProductInventoryClient(
            httpClient,
            Options.Create(new ProductServiceOptions
            {
                BaseUrl = "http://product.test",
                TimeoutMilliseconds = timeoutMilliseconds,
                SafeRetryCount = safeRetryCount,
                SafeRetryDelayMilliseconds = safeRetryDelayMilliseconds
            }),
            NullLogger<ProductInventoryClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed class StubHandler(
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
