using MicroShop.ProductService.Persistence;
using MicroShop.ProductService.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.ProductService.Features.Products;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/products")
            .WithTags("Products");

        group.MapGet("", ListProductsAsync)
            .WithName("ListProducts")
            .Produces<ProductPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/{productId:guid}", GetProductAsync)
            .WithName("GetProduct")
            .Produces<ProductResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("", CreateProductAsync)
            .WithName("CreateProduct")
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> ListProductsAsync(
        HttpContext httpContext,
        ProductDbContext dbContext,
        int page = 1,
        int limit = 20,
        bool includeInactive = false,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ProductValidator.ValidateList(page, limit, search);
        if (validationErrors.Count > 0)
        {
            return ProductProblems.Validation(httpContext, validationErrors);
        }

        var query = dbContext.Products.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(product => product.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(product => EF.Functions.ILike(product.Name, $"%{normalizedSearch}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        var products = await query
            .OrderByDescending(product => product.IsActive)
            .ThenBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var response = new ProductPageResponse(
            products.Select(ToResponse).ToArray(),
            page,
            limit,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit));

        return Results.Ok(response);
    }

    private static async Task<IResult> GetProductAsync(
        Guid productId,
        HttpContext httpContext,
        ProductDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);

        return product is null
            ? ProductProblems.NotFound(httpContext, productId)
            : Results.Ok(ToResponse(product));
    }

    private static async Task<IResult> CreateProductAsync(
        CreateProductRequest? request,
        HttpContext httpContext,
        ProductDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = ProductValidator.ValidateCreate(request);
        if (validationErrors.Count > 0)
        {
            return ProductProblems.Validation(httpContext, validationErrors);
        }

        var now = DateTimeOffset.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request!.Name!.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            UnitPrice = decimal.Round(request.UnitPrice, 2, MidpointRounding.ToEven),
            Currency = ProductValidator.Currency,
            AvailableStock = request.InitialStock,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Version = 1
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/products/{product.Id}", ToResponse(product));
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.UnitPrice,
            product.Currency,
            product.AvailableStock,
            product.IsActive,
            product.CreatedAtUtc,
            product.UpdatedAtUtc,
            product.Version);
    }
}

internal static class ProductProblems
{
    public static IResult Validation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return Problem(
            httpContext,
            StatusCodes.Status400BadRequest,
            "Validation error",
            "The product request is invalid.",
            "VALIDATION_ERROR",
            errors);
    }

    public static IResult NotFound(HttpContext httpContext, Guid productId)
    {
        return Problem(
            httpContext,
            StatusCodes.Status404NotFound,
            "Product not found",
            $"Product '{productId}' was not found.",
            "PRODUCT_NOT_FOUND",
            new Dictionary<string, string[]>(StringComparer.Ordinal));
    }

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string code,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            instance: httpContext.Request.Path,
            type: $"https://microshop.local/problems/{code.ToLowerInvariant().Replace('_', '-')}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier,
                ["errors"] = errors
            });
    }
}
