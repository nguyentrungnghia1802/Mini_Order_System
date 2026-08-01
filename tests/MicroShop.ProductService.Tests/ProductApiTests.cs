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
    public async Task PatchUpdatesMutableFieldsAndReturnsNewVersion()
    {
        var productName = $"Patch Product {Guid.NewGuid():N}";
        var created = await CreateProductAsync(productName, isActive: true);

        using var request = CreatePatchRequest(
            created.Id,
            created.Version,
            new
            {
                name = $"{productName} Updated",
                description = "Updated description",
                unitPrice = 987.655m,
                availableStock = 14,
                isActive = true
            });

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"\"{created.Version + 1}\"", response.Headers.ETag?.ToString());
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal($"{productName} Updated", updated.Name);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(987.66m, updated.UnitPrice);
        Assert.Equal(14, updated.AvailableStock);
        Assert.Equal(created.Version + 1, updated.Version);
    }

    [Fact]
    public async Task PatchCanDeactivateAndReactivateProduct()
    {
        var productName = $"Lifecycle Product {Guid.NewGuid():N}";
        var created = await CreateProductAsync(productName, isActive: true);

        using var deactivateRequest = CreatePatchRequest(
            created.Id,
            created.Version,
            new { isActive = false });
        using var deactivateResponse = await fixture.Client.SendAsync(deactivateRequest);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);

        var shopperPage = await fixture.Client.GetFromJsonAsync<ProductPageResponse>(
            $"/api/v1/products?search={Uri.EscapeDataString(productName)}");
        Assert.NotNull(shopperPage);
        Assert.DoesNotContain(shopperPage.Items, product => product.Id == created.Id);

        using var activateRequest = CreatePatchRequest(
            created.Id,
            deactivated.Version,
            new { isActive = true });
        using var activateResponse = await fixture.Client.SendAsync(activateRequest);

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activated = await activateResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(activated);
        Assert.True(activated.IsActive);

        shopperPage = await fixture.Client.GetFromJsonAsync<ProductPageResponse>(
            $"/api/v1/products?search={Uri.EscapeDataString(productName)}");
        Assert.NotNull(shopperPage);
        Assert.Contains(shopperPage.Items, product => product.Id == created.Id);
    }

    [Fact]
    public async Task ConcurrentPatchesWithSameVersionProduceOneSuccessAndOneConflict()
    {
        var productName = $"Concurrent Product {Guid.NewGuid():N}";
        var created = await CreateProductAsync(productName, isActive: true);

        using var firstRequest = CreatePatchRequest(
            created.Id,
            created.Version,
            new { name = $"{productName} First" });
        using var secondRequest = CreatePatchRequest(
            created.Id,
            created.Version,
            new { name = $"{productName} Second" });

        var responses = await Task.WhenAll(
            fixture.Client.SendAsync(firstRequest),
            fixture.Client.SendAsync(secondRequest));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        var conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);
        var body = await conflict.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            "PRODUCT_CONCURRENCY_CONFLICT",
            document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            created.Version + 1,
            document.RootElement.GetProperty("errors").GetProperty("version")[0]
                .GetString() is { } versionMessage
                ? ParseVersionFromMessage(versionMessage)
                : 0);

        foreach (var response in responses)
        {
            response.Dispose();
        }

        var detail = await fixture.Client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/products/{created.Id}");
        Assert.NotNull(detail);
        Assert.Equal(created.Version + 1, detail.Version);
    }

    [Fact]
    public async Task ProductDatabaseRejectsNegativeStock()
    {
        Assert.True(await fixture.ProductTableHasNegativeStockConstraintAsync());
    }

    private async Task<ProductResponse> CreateProductAsync(string name, bool isActive)
    {
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name,
                description = "Integration test product.",
                unitPrice = 100m,
                currency = "VND",
                initialStock = 5,
                isActive
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        return product;
    }

    private static HttpRequestMessage CreatePatchRequest(Guid productId, long version, object body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/products/{productId}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return request;
    }

    private static long ParseVersionFromMessage(string message)
    {
        const string prefix = "The current Product version is ";
        return long.Parse(
            message[prefix.Length..].TrimEnd('.'),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
