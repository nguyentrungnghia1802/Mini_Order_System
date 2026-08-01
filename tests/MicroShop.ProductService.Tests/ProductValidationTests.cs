using MicroShop.ProductService.Features.Products;

namespace MicroShop.ProductService.Tests;

public sealed class ProductValidationTests
{
    [Fact]
    public void CreateRequestRejectsNegativePriceStockAndInvalidText()
    {
        var request = new CreateProductRequest(
            " ",
            new string('x', ProductValidator.MaxDescriptionLength + 1),
            -1m,
            "USD",
            -2);

        var errors = ProductValidator.ValidateCreate(request);

        Assert.Contains("name", errors.Keys);
        Assert.Contains("description", errors.Keys);
        Assert.Contains("unitPrice", errors.Keys);
        Assert.Contains("currency", errors.Keys);
        Assert.Contains("initialStock", errors.Keys);
    }

    [Fact]
    public void ListRequestRejectsUnboundedPaginationAndSearch()
    {
        var errors = ProductValidator.ValidateList(
            ProductValidator.MaxPageNumber + 1,
            ProductValidator.MaxPageSize + 1,
            new string('x', ProductValidator.MaxSearchLength + 1));

        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void ValidCreateRequestHasNoValidationErrors()
    {
        var request = new CreateProductRequest(
            "Mechanical Keyboard",
            "Demo product",
            1_200_000m,
            "vnd",
            10);

        var errors = ProductValidator.ValidateCreate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateRequestRejectsInvalidMutableFields()
    {
        var request = new UpdateProductRequest
        {
            Name = " ",
            Description = new string('x', ProductValidator.MaxDescriptionLength + 1),
            UnitPrice = -1m,
            AvailableStock = -2
        };

        var errors = ProductValidator.ValidateUpdate(request);

        Assert.Contains("name", errors.Keys);
        Assert.Contains("description", errors.Keys);
        Assert.Contains("unitPrice", errors.Keys);
        Assert.Contains("availableStock", errors.Keys);
    }
}
