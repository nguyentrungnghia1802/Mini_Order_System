using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroShop.ProductService.Features.Inventory;

public sealed class ReserveInventoryRequest
{
    public Guid OrderId { get; init; }

    public IReadOnlyList<ReserveInventoryItemRequest>? Items { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed class ReserveInventoryItemRequest
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalFields { get; init; }
}

public sealed record InventoryReservationItemResponse(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public sealed record InventoryReservationResponse(
    Guid ReservationId,
    Guid OrderId,
    string Status,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<InventoryReservationItemResponse> Items,
    bool IdempotentReplay);

public sealed record InventoryReservationQueryResponse(
    Guid ReservationId,
    Guid OrderId,
    string Status,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<InventoryReservationItemResponse> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReleasedAtUtc);

public sealed record InventoryReleaseResponse(
    Guid OrderId,
    Guid ReservationId,
    string Status,
    bool IdempotentReplay,
    DateTimeOffset? ReleasedAtUtc);

public static class InventoryValidator
{
    public const int MaxItemCount = 20;
    public const int MaxQuantity = 100;

    public static IReadOnlyDictionary<string, string[]> ValidateReserve(ReserveInventoryRequest? request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (request is null)
        {
            AddError(errors, "request", "A reservation request is required.");
            return ToReadOnly(errors);
        }

        if (request.AdditionalFields is { Count: > 0 })
        {
            AddError(errors, "request", "Only orderId and items are accepted.");
        }

        if (request.OrderId == Guid.Empty)
        {
            AddError(errors, "orderId", "Order ID is required.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            AddError(errors, "items", "At least one reservation item is required.");
            return ToReadOnly(errors);
        }

        if (request.Items.Count > MaxItemCount)
        {
            AddError(errors, "items", $"A reservation cannot contain more than {MaxItemCount} products.");
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

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }

    private static Dictionary<string, string[]> ToReadOnly(Dictionary<string, List<string>> errors)
    {
        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }
}
