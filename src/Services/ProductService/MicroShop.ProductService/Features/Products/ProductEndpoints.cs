using System.Globalization;
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
        group.MapPatch("/{productId:guid}", UpdateProductAsync)
            .WithName("UpdateProduct")
            .WithDescription("Updates mutable Product fields. The If-Match header must contain the current Product version.")
            .Produces<ProductResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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

        if (product is null)
        {
            return ProductProblems.NotFound(httpContext, productId);
        }

        SetVersionHeader(httpContext, product.Version);
        return Results.Ok(ToResponse(product));
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

        SetVersionHeader(httpContext, product.Version);
        return Results.Created($"/api/v1/products/{product.Id}", ToResponse(product));
    }

    private static async Task<IResult> UpdateProductAsync(
        Guid productId,
        UpdateProductRequest? request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpContext httpContext,
        ProductDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = ProductValidator.ValidateUpdate(request);
        if (validationErrors.Count > 0)
        {
            return ProductProblems.Validation(httpContext, validationErrors);
        }

        if (!TryParseVersion(ifMatch, out var expectedVersion))
        {
            return ProductProblems.Validation(
                httpContext,
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["ifMatch"] = ["If-Match must contain a quoted positive Product version."]
                });
        }

        var product = await dbContext.Products
            .SingleOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);
        if (product is null)
        {
            return ProductProblems.NotFound(httpContext, productId);
        }

        if (product.Version != expectedVersion)
        {
            return ProductProblems.ConcurrencyConflict(httpContext, product.Version);
        }

        var update = request!;
        if (update.Name is not null)
        {
            product.Name = update.Name.Trim();
        }

        if (update.Description is not null)
        {
            product.Description = string.IsNullOrWhiteSpace(update.Description)
                ? null
                : update.Description.Trim();
        }

        if (update.UnitPrice is not null)
        {
            product.UnitPrice = decimal.Round(update.UnitPrice.Value, 2, MidpointRounding.ToEven);
        }

        if (update.AvailableStock is not null)
        {
            product.AvailableStock = update.AvailableStock.Value;
        }

        if (update.IsActive is not null)
        {
            product.IsActive = update.IsActive.Value;
        }

        product.UpdatedAtUtc = DateTimeOffset.UtcNow;
        product.Version++;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentVersion = await dbContext.Products
                .AsNoTracking()
                .Where(candidate => candidate.Id == productId)
                .Select(candidate => (long?)candidate.Version)
                .SingleOrDefaultAsync(cancellationToken);

            return currentVersion is null
                ? ProductProblems.NotFound(httpContext, productId)
                : ProductProblems.ConcurrencyConflict(httpContext, currentVersion.Value);
        }

        SetVersionHeader(httpContext, product.Version);
        return Results.Ok(ToResponse(product));
    }

    private static bool TryParseVersion(string? ifMatch, out long version)
    {
        version = 0;
        var value = ifMatch?.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return false;
        }

        return long.TryParse(
                   value[1..^1],
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out version)
               && version > 0;
    }

    private static void SetVersionHeader(HttpContext httpContext, long version)
    {
        httpContext.Response.Headers.ETag = $"\"{version}\"";
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

    public static IResult ConcurrencyConflict(HttpContext httpContext, long currentVersion)
    {
        return Problem(
            httpContext,
            StatusCodes.Status409Conflict,
            "Product update conflict",
            "The product was modified by another request. Reload it and retry with the current version.",
            "PRODUCT_CONCURRENCY_CONFLICT",
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["version"] = [$"The current Product version is {currentVersion}."]
            });
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
