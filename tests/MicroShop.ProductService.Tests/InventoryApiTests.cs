using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroShop.ProductService.Features.Inventory;
using MicroShop.ProductService.Features.Products;

namespace MicroShop.ProductService.Tests;

public sealed class InventoryApiTests(ProductApiFixture fixture) : IClassFixture<ProductApiFixture>
{
    [Fact]
    public async Task ReserveReturnsAuthoritativeSnapshotsAndDecrementsStock()
    {
        var product = await CreateProductAsync("Reservation Product", 12.346m, 4, isActive: true);
        var orderId = Guid.NewGuid();

        using var response = await ReserveAsync(
            orderId,
            new ReserveItem(product.Id, 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<InventoryReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal("reserved", reservation.Status);
        Assert.Equal(24.70m, reservation.TotalAmount);
        Assert.False(reservation.IdempotentReplay);
        var item = Assert.Single(reservation.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal("Reservation Product", item.ProductName);
        Assert.Equal(12.35m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(24.70m, item.Subtotal);

        var current = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{product.Id}");
        Assert.NotNull(current);
        Assert.Equal(2, current.AvailableStock);
        Assert.Equal(product.Version + 1, current.Version);
    }

    [Fact]
    public async Task IdenticalReservationReplayReturnsExistingReservationWithoutDecrementingAgain()
    {
        var first = await CreateProductAsync("Replay Product A", 10m, 3, isActive: true);
        var second = await CreateProductAsync("Replay Product B", 20m, 3, isActive: true);
        var orderId = Guid.NewGuid();

        using var createdResponse = await ReserveAsync(
            orderId,
            new ReserveItem(second.Id, 1),
            new ReserveItem(first.Id, 2));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<InventoryReservationResponse>();
        Assert.NotNull(created);

        using var replayResponse = await ReserveAsync(
            orderId,
            new ReserveItem(first.Id, 2),
            new ReserveItem(second.Id, 1));
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<InventoryReservationResponse>();
        Assert.NotNull(replay);
        Assert.Equal(created.ReservationId, replay.ReservationId);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(created.TotalAmount, replay.TotalAmount);

        var firstCurrent = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{first.Id}");
        var secondCurrent = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{second.Id}");
        Assert.NotNull(firstCurrent);
        Assert.NotNull(secondCurrent);
        Assert.Equal(1, firstCurrent.AvailableStock);
        Assert.Equal(2, secondCurrent.AvailableStock);
    }

    [Fact]
    public async Task ReusingOrderIdWithDifferentItemsReturnsMismatchWithoutChangingStock()
    {
        var first = await CreateProductAsync("Mismatch Product A", 10m, 3, isActive: true);
        var second = await CreateProductAsync("Mismatch Product B", 20m, 3, isActive: true);
        var orderId = Guid.NewGuid();

        using var createdResponse = await ReserveAsync(
            orderId,
            new ReserveItem(first.Id, 1));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);

        using var mismatchResponse = await ReserveAsync(
            orderId,
            new ReserveItem(second.Id, 1));
        Assert.Equal(HttpStatusCode.Conflict, mismatchResponse.StatusCode);
        Assert.Equal("RESERVATION_REQUEST_MISMATCH", await ReadProblemCodeAsync(mismatchResponse));

        var firstCurrent = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{first.Id}");
        var secondCurrent = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{second.Id}");
        Assert.NotNull(firstCurrent);
        Assert.NotNull(secondCurrent);
        Assert.Equal(2, firstCurrent.AvailableStock);
        Assert.Equal(3, secondCurrent.AvailableStock);
    }

    [Theory]
    [InlineData("not-found")]
    [InlineData("inactive")]
    [InlineData("insufficient")]
    public async Task FailedReservationDoesNotPartiallyDecrementStock(string failure)
    {
        var validProduct = await CreateProductAsync(
            $"Atomic failure valid {failure}",
            10m,
            3,
            isActive: true);
        var failingProduct = failure switch
        {
            "not-found" => new ProductResponse(
                Guid.NewGuid(),
                string.Empty,
                null,
                0,
                "VND",
                0,
                false,
                default,
                default,
                0),
            "inactive" => await CreateProductAsync(
                $"Atomic failure inactive {failure}",
                10m,
                3,
                isActive: false),
            "insufficient" => await CreateProductAsync(
                $"Atomic failure stock {failure}",
                10m,
                0,
                isActive: true),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };

        using var response = await ReserveAsync(
            Guid.NewGuid(),
            new ReserveItem(validProduct.Id, 1),
            new ReserveItem(failingProduct.Id, 1));

        var expectedStatus = failure == "not-found"
            ? HttpStatusCode.NotFound
            : HttpStatusCode.Conflict;
        var expectedCode = failure switch
        {
            "not-found" => "PRODUCT_NOT_FOUND",
            "inactive" => "PRODUCT_INACTIVE",
            "insufficient" => "INSUFFICIENT_STOCK",
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedCode, await ReadProblemCodeAsync(response));

        var current = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{validProduct.Id}");
        Assert.NotNull(current);
        Assert.Equal(validProduct.AvailableStock, current.AvailableStock);
    }

    [Fact]
    public async Task ReleaseRestoresStockExactlyOnceAndRepeatedReleaseIsIdempotent()
    {
        var product = await CreateProductAsync("Release Product", 15m, 5, isActive: true);
        var orderId = Guid.NewGuid();

        using var reserveResponse = await ReserveAsync(
            orderId,
            new ReserveItem(product.Id, 3));
        Assert.Equal(HttpStatusCode.Created, reserveResponse.StatusCode);

        using var releaseResponse = await fixture.Client.PostAsync(
            $"/internal/v1/inventory/reservations/{orderId}/release",
            content: null);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        var released = await releaseResponse.Content.ReadFromJsonAsync<InventoryReleaseResponse>();
        Assert.NotNull(released);
        Assert.Equal("released", released.Status);
        Assert.False(released.IdempotentReplay);
        Assert.NotNull(released.ReleasedAtUtc);

        using var replayResponse = await fixture.Client.PostAsync(
            $"/internal/v1/inventory/reservations/{orderId}/release",
            content: null);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<InventoryReleaseResponse>();
        Assert.NotNull(replay);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(released.ReleasedAtUtc, replay.ReleasedAtUtc);

        var current = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{product.Id}");
        Assert.NotNull(current);
        Assert.Equal(5, current.AvailableStock);
        Assert.Equal(product.Version + 2, current.Version);
    }

    [Fact]
    public async Task ConcurrentLastStockReservationsAllowOnlyOneSuccess()
    {
        var product = await CreateProductAsync("Concurrent Reservation Product", 10m, 1, isActive: true);

        var responses = await Task.WhenAll(
            ReserveAsync(Guid.NewGuid(), new ReserveItem(product.Id, 1)),
            ReserveAsync(Guid.NewGuid(), new ReserveItem(product.Id, 1)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        var conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("INSUFFICIENT_STOCK", await ReadProblemCodeAsync(conflict));

        foreach (var response in responses)
        {
            response.Dispose();
        }

        var current = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{product.Id}");
        Assert.NotNull(current);
        Assert.Equal(0, current.AvailableStock);
    }

    private async Task<ProductResponse> CreateProductAsync(
        string name,
        decimal unitPrice,
        int initialStock,
        bool isActive)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name,
                description = "Inventory integration test product.",
                unitPrice,
                currency = "VND",
                initialStock,
                isActive
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        return product;
    }

    private Task<HttpResponseMessage> ReserveAsync(
        Guid orderId,
        params ReserveItem[] items)
    {
        return fixture.Client.PostAsJsonAsync(
            "/internal/v1/inventory/reservations",
            new
            {
                orderId,
                items = items.Select(item => new
                {
                    productId = item.ProductId,
                    quantity = item.Quantity
                })
            });
    }

    private static async Task<string> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("code").GetString()!;
    }

    private sealed record ReserveItem(Guid ProductId, int Quantity);
}
