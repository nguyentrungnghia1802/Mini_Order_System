using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroShop.OrderService.Domain;

namespace MicroShop.OrderService.Features.Orders;

public sealed class CreateOrderRequest
{
    public string? CustomerName { get; init; }

    public string? CustomerEmail { get; init; }

    public IReadOnlyList<CreateOrderItemRequest>? Items { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed class CreateOrderItemRequest
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed record OrderItemResponse(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public sealed record OrderResponse(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    string Status,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items,
    bool CanCancel,
    string? FailureCode,
    string? FailureDetail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    long Version);

public sealed record OrderPageResponse(
    IReadOnlyList<OrderResponse> Items,
    int Page,
    int Limit,
    int Total,
    int TotalPages);

public static class OrderValidator
{
    public const int MaxCustomerNameLength = 150;
    public const int MaxCustomerEmailLength = 320;
    public const int MaxItemCount = 20;
    public const int MaxQuantity = 100;
    public const int MaxPageSize = 100;
    public const int MaxPageNumber = 100_000;
    public const decimal MaxOrderTotal = 1_000_000_000m;

    public static IReadOnlyDictionary<string, string[]> ValidateCreate(CreateOrderRequest? request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (request is null)
        {
            AddError(errors, "request", "An order request is required.");
            return ToReadOnly(errors);
        }

        if (request.AdditionalFields is { Count: > 0 })
        {
            AddError(errors, "request", "Only customerName, customerEmail, and items are accepted.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            AddError(errors, "customerName", "Customer name is required.");
        }
        else if (request.CustomerName.Trim().Length > MaxCustomerNameLength)
        {
            AddError(errors, "customerName", $"Customer name cannot exceed {MaxCustomerNameLength} characters.");
        }

        var normalizedEmail = request.CustomerEmail?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            AddError(errors, "customerEmail", "Customer email is required.");
        }
        else if (normalizedEmail.Length > MaxCustomerEmailLength
                 || normalizedEmail.Any(char.IsWhiteSpace)
                 || !MailAddress.TryCreate(normalizedEmail, out var address)
                 || !string.Equals(address.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            AddError(errors, "customerEmail", "A valid customer email is required.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            AddError(errors, "items", "At least one order item is required.");
            return ToReadOnly(errors);
        }

        if (request.Items.Count > MaxItemCount)
        {
            AddError(errors, "items", $"An order cannot contain more than {MaxItemCount} distinct products.");
        }

        var seenProductIds = new HashSet<Guid>();
        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            var fieldPrefix = $"items[{index}]";

            if (item.AdditionalFields is { Count: > 0 })
            {
                AddError(errors, fieldPrefix, "Only productId and quantity are accepted.");
            }

            if (item.ProductId == Guid.Empty)
            {
                AddError(errors, $"{fieldPrefix}.productId", "Product ID is required.");
            }
            else if (!seenProductIds.Add(item.ProductId))
            {
                AddError(errors, $"{fieldPrefix}.productId", "Duplicate product IDs are not allowed.");
            }

            if (item.Quantity is < 1 or > MaxQuantity)
            {
                AddError(errors, $"{fieldPrefix}.quantity", $"Quantity must be between 1 and {MaxQuantity}.");
            }
        }

        return ToReadOnly(errors);
    }

    public static IReadOnlyDictionary<string, string[]> ValidateList(
        int page,
        int limit,
        string? status,
        string? customerEmail)
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

        if (!string.IsNullOrWhiteSpace(status) && !OrderStatuses.IsKnown(status.Trim()))
        {
            AddError(errors, "status", "Status is not recognized.");
        }

        if (customerEmail is not null)
        {
            var normalizedEmail = customerEmail.Trim();
            if (normalizedEmail.Length > MaxCustomerEmailLength
                || normalizedEmail.Any(char.IsWhiteSpace)
                || !MailAddress.TryCreate(normalizedEmail, out var address)
                || !string.Equals(address.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                AddError(errors, "customerEmail", "A valid customer email is required.");
            }
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
