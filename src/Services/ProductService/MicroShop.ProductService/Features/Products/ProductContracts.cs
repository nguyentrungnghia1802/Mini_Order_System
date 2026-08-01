namespace MicroShop.ProductService.Features.Products;

public sealed record CreateProductRequest(
    string? Name,
    string? Description,
    decimal UnitPrice,
    string? Currency,
    int InitialStock,
    bool IsActive = true);

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal UnitPrice,
    string Currency,
    int AvailableStock,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record ProductPageResponse(
    IReadOnlyList<ProductResponse> Items,
    int Page,
    int Limit,
    int Total,
    int TotalPages);

public static class ProductValidator
{
    public const string Currency = "VND";
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2_000;
    public const int MaxPageSize = 100;
    public const int MaxPageNumber = 100_000;
    public const int MaxSearchLength = 100;

    public static IReadOnlyDictionary<string, string[]> ValidateCreate(CreateProductRequest? request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (request is null)
        {
            AddError(errors, "request", "A product request is required.");
            return ToReadOnly(errors);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            AddError(errors, "name", "Name is required.");
        }
        else if (request.Name.Trim().Length > MaxNameLength)
        {
            AddError(errors, "name", $"Name cannot exceed {MaxNameLength} characters.");
        }

        if (request.Description?.Length > MaxDescriptionLength)
        {
            AddError(errors, "description", $"Description cannot exceed {MaxDescriptionLength} characters.");
        }

        if (request.UnitPrice < 0)
        {
            AddError(errors, "unitPrice", "Unit price cannot be negative.");
        }

        if (!string.Equals(request.Currency?.Trim(), Currency, StringComparison.OrdinalIgnoreCase))
        {
            AddError(errors, "currency", $"Currency must be {Currency}.");
        }

        if (request.InitialStock < 0)
        {
            AddError(errors, "initialStock", "Initial stock cannot be negative.");
        }

        return ToReadOnly(errors);
    }

    public static IReadOnlyDictionary<string, string[]> ValidateList(int page, int limit, string? search)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (page < 1 || page > MaxPageNumber)
        {
            AddError(errors, "page", $"Page must be between 1 and {MaxPageNumber}.");
        }

        if (limit is < 1 or > MaxPageSize)
        {
            AddError(errors, "limit", $"Limit must be between 1 and {MaxPageSize}.");
        }

        if (search is not null && search.Trim().Length > MaxSearchLength)
        {
            AddError(errors, "search", $"Search cannot exceed {MaxSearchLength} characters.");
        }

        return ToReadOnly(errors);
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }

    private static Dictionary<string, string[]> ToReadOnly(
        Dictionary<string, List<string>> errors)
    {
        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }
}
