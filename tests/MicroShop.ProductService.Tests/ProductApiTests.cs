using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroShop.ProductService.Features.Products;

namespace MicroShop.ProductService.Tests;

public sealed class ProductApiTests(ProductApiFixture fixture) : IClassFixture<ProductApiFixture>
{
    [Fact]
    public async Task CreateGetAndListProductRoundTrip()
    {
        var productName = $"Integration Product {Guid.NewGuid():N}";
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name = productName,
                description = "Created by the PostgreSQL integration test.",
                unitPrice = 123_456.78m,
                currency = "VND",
                initialStock = 7,
                isActive = true
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.Equal(productName, created.Name);
        Assert.Equal(123_456.78m, created.UnitPrice);
        Assert.Equal(7, created.AvailableStock);

        var detail = await fixture.Client.GetFromJsonAsync<ProductResponse>(response.Headers.Location);
        Assert.NotNull(detail);
        Assert.Equal(created.Id, detail.Id);

        var page = await fixture.Client.GetFromJsonAsync<ProductPageResponse>(
            $"/api/v1/products?search={Uri.EscapeDataString(productName)}&limit=100");
        Assert.NotNull(page);
        Assert.Contains(page.Items, product => product.Id == created.Id);
    }

    [Fact]
    public async Task InactiveProductIsHiddenByDefaultAndVisibleForOperatorListing()
    {
        var productName = $"Inactive Product {Guid.NewGuid():N}";
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name = productName,
                description = (string?)null,
                unitPrice = 10m,
                currency = "VND",
                initialStock = 0,
                isActive = false
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var shopperPage = await fixture.Client.GetFromJsonAsync<ProductPageResponse>(
            $"/api/v1/products?search={Uri.EscapeDataString(productName)}");
        Assert.NotNull(shopperPage);
        Assert.DoesNotContain(shopperPage.Items, product => product.Name == productName);

        var operatorPage = await fixture.Client.GetFromJsonAsync<ProductPageResponse>(
            $"/api/v1/products?search={Uri.EscapeDataString(productName)}&includeInactive=true");
        Assert.NotNull(operatorPage);
        Assert.Contains(operatorPage.Items, product => product.Name == productName && !product.IsActive);
    }

    [Fact]
    public async Task InvalidProductRequestReturnsProblemDetailsWithStableCode()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name = "Invalid product",
                description = (string?)null,
                unitPrice = -1m,
                currency = "VND",
                initialStock = -1,
                isActive = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.True(
            string.Equals("VALIDATION_ERROR", document.RootElement.GetProperty("code").GetString(), StringComparison.Ordinal),
            body);
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
        Assert.True(document.RootElement.GetProperty("errors").GetProperty("unitPrice").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ProductDatabaseRejectsNegativeStock()
    {
        Assert.True(await fixture.ProductTableHasNegativeStockConstraintAsync());
    }
}
