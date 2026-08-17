using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Tests;

public sealed class OrderApiTests(OrderDatabaseFixture fixture) : IClassFixture<OrderDatabaseFixture>
{
    [Fact]
    public async Task CreateReturnsConfirmedOrderWithFakeProductSnapshots()
    {
        var request = new CreateOrderRequestDto(
            "Nguyen Van A",
            "order-api-success@example.com",
            [
                new CreateOrderItemRequestDto(FakeProductCatalog.MechanicalKeyboardId, 2),
                new CreateOrderItemRequestDto(FakeProductCatalog.WirelessMouseId, 1)
            ]);

        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/orders", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var persisted = await response.Content.ReadFromJsonAsync<OrderResponseDto>();

        Assert.NotNull(persisted);
        Assert.Equal(OrderStatuses.Confirmed, persisted.Status);
        Assert.Equal("order-api-success@example.com", persisted.CustomerEmail);
        Assert.Equal(2_850_000m, persisted.TotalAmount);
        Assert.True(persisted.CanCancel);
        Assert.Equal(2, persisted.Version);
        Assert.Collection(
            persisted.Items,
            item =>
            {
                Assert.Equal("Mechanical Keyboard", item.ProductName);
                Assert.Equal(1_200_000m, item.UnitPrice);
                Assert.Equal(2, item.Quantity);
            },
            item =>
            {
                Assert.Equal("Wireless Mouse", item.ProductName);
                Assert.Equal(450_000m, item.UnitPrice);
                Assert.Equal(1, item.Quantity);
            });

        await using var dbContext = fixture.CreateDbContext();
        var order = await dbContext.Orders
            .Include(candidate => candidate.StateHistory)
            .SingleAsync(candidate => candidate.Id == persisted.Id);
        Assert.Equal(2, order.StateHistory.Count);
        Assert.Equal(OrderStatuses.Confirmed, order.StateHistory.Last().ToStatus);
    }

    [Fact]
    public async Task CreateRejectsBrowserAuthoritativeFields()
    {
        const string json = """
            {
              "customerName": "Browser User",
              "customerEmail": "browser-fields@example.com",
              "unitPrice": 1,
              "items": [
                {
                  "productId": "11111111-1111-1111-1111-111111111111",
                  "quantity": 1,
                  "productName": "Browser supplied name"
                }
              ]
            }
            """;

        using var response = await fixture.Client.PostAsync(
            "/api/v1/orders",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CreateRejectsDuplicateAndOutOfRangeItems()
    {
        var request = new CreateOrderRequestDto(
            "Invalid Order",
            "invalid-items@example.com",
            [
                new CreateOrderItemRequestDto(FakeProductCatalog.MechanicalKeyboardId, 101),
                new CreateOrderItemRequestDto(FakeProductCatalog.MechanicalKeyboardId, 1)
            ]);

        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadProblemCodeAsync(response));
        var problem = await ReadProblemAsync(response);
        Assert.Contains("items[0].quantity", problem.Errors.Keys);
        Assert.Contains("items[1].productId", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("PRODUCT_NOT_FOUND", "55555555-5555-5555-5555-555555555555")]
    [InlineData("PRODUCT_INACTIVE", "44444444-4444-4444-4444-444444444444")]
    [InlineData("INSUFFICIENT_STOCK", "33333333-3333-3333-3333-333333333333")]
    public async Task BusinessFakeProductFailuresPersistRejectedOrder(string expectedCode, string productId)
    {
        var request = new CreateOrderRequestDto(
            "Rejected Order",
            $"{expectedCode.ToLowerInvariant()}@example.com",
            [new CreateOrderItemRequestDto(Guid.Parse(productId), 1)]);

        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/orders", request);
        Assert.Equal(
            expectedCode is "PRODUCT_NOT_FOUND" ? HttpStatusCode.NotFound : HttpStatusCode.Conflict,
            response.StatusCode);
        Assert.Equal(expectedCode, await ReadProblemCodeAsync(response));

        var problem = await ReadProblemAsync(response);
        Assert.NotNull(problem.OrderId);

        await using var dbContext = fixture.CreateDbContext();
        var order = await dbContext.Orders
            .Include(candidate => candidate.StateHistory)
            .SingleAsync(candidate => candidate.Id == problem.OrderId);
        Assert.Equal(OrderStatuses.Rejected, order.Status);
        Assert.Equal(expectedCode, order.FailureCode);
        Assert.Equal(2, order.StateHistory.Count);
    }

    [Fact]
    public async Task ListAndDetailSupportPaginationAndStableOrderResponse()
    {
        var first = await CreateSuccessAsync("list-one@example.com");
        var second = await CreateSuccessAsync("list-two@example.com");

        using var listResponse = await fixture.Client.GetAsync(
            "/api/v1/orders?customerEmail=list-two@example.com&page=1&limit=1");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<OrderPageResponseDto>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(second.Id, page.Items[0].Id);

        using var detailResponse = await fixture.Client.GetAsync($"/api/v1/orders/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<OrderResponseDto>();
        Assert.NotNull(detail);
        Assert.Equal(first.Id, detail.Id);
        Assert.Equal("list-one@example.com", detail.CustomerEmail);
        Assert.Single(detail.Items);
    }

    [Fact]
    public async Task ListRejectsUnknownStatus()
    {
        using var response = await fixture.Client.GetAsync(
            "/api/v1/orders?status=not-a-real-status");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadProblemCodeAsync(response));
    }

    private async Task<OrderResponseDto> CreateSuccessAsync(string email)
    {
        var request = new CreateOrderRequestDto(
            "List User",
            email,
            [new CreateOrderItemRequestDto(FakeProductCatalog.WirelessMouseId, 1)]);
        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/orders", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponseDto>())!;
    }

    private static async Task<string> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await ReadProblemAsync(response);
        return problem.Code;
    }

    private static async Task<ProblemResponseDto> ReadProblemAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var errors = root.TryGetProperty("errors", out var errorsElement)
            ? errorsElement.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.GetRawText(),
                StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var orderId = root.TryGetProperty("orderId", out var orderIdElement)
            ? orderIdElement.GetGuid()
            : (Guid?)null;
        return new ProblemResponseDto(
            root.GetProperty("code").GetString()!,
            orderId,
            errors);
    }

    private sealed record CreateOrderRequestDto(
        string CustomerName,
        string CustomerEmail,
        IReadOnlyList<CreateOrderItemRequestDto> Items);

    private sealed record CreateOrderItemRequestDto(Guid ProductId, int Quantity);

    private sealed record OrderItemResponseDto(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal Subtotal);

    private sealed record OrderResponseDto(
        Guid Id,
        string CustomerName,
        string CustomerEmail,
        string Status,
        string Currency,
        decimal TotalAmount,
        IReadOnlyList<OrderItemResponseDto> Items,
        bool CanCancel,
        string? FailureCode,
        string? FailureDetail,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset? ConfirmedAtUtc,
        DateTimeOffset? CancelledAtUtc,
        long Version);

    private sealed record OrderPageResponseDto(
        IReadOnlyList<OrderResponseDto> Items,
        int Page,
        int Limit,
        int Total,
        int TotalPages);

    private sealed record ProblemResponseDto(
        string Code,
        Guid? OrderId,
        IReadOnlyDictionary<string, string> Errors);
}
