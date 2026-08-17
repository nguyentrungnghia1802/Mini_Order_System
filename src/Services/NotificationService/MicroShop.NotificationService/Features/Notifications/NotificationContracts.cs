using System.Net.Mail;

namespace MicroShop.NotificationService.Features.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    Guid OrderId,
    string CustomerEmail,
    string Subject,
    string Body,
    decimal TotalAmount,
    string Currency,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationPageResponse(
    IReadOnlyList<NotificationResponse> Items,
    int Page,
    int Limit,
    int Total,
    int TotalPages);

internal static class NotificationValidator
{
    public const int MaxPageSize = 100;
    public const int MaxPageNumber = 100_000;
    public const int MaxCustomerEmailLength = 320;

    public static IReadOnlyDictionary<string, string[]> ValidateList(
        int page,
        int limit,
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

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }
}
