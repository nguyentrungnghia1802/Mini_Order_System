using System.Net;
using System.Net.Http.Json;
using MicroShop.Contracts.Orders;
using MicroShop.NotificationService.Features.Messaging;
using MicroShop.NotificationService.Features.Notifications;
using MicroShop.NotificationService.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroShop.NotificationService.Tests;

public sealed class NotificationApiTests(NotificationDatabaseFixture fixture)
    : IClassFixture<NotificationDatabaseFixture>
{
    private readonly HttpClient client = fixture.CreateClient();

    [Fact]
    public async Task ListsNotificationsByEmailAndOrderWithStablePagination()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var firstMessage = CreateMessage(email);
        var secondMessage = CreateMessage(email);
        await PersistAsync(firstMessage);
        await PersistAsync(secondMessage);

        using var filteredResponse = await client.GetAsync(
            $"/api/v1/notifications?customerEmail={Uri.EscapeDataString(email.ToUpperInvariant())}&page=1&limit=1");
        var filteredPage = await filteredResponse.Content.ReadFromJsonAsync<NotificationPageResponse>();

        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.NotNull(filteredPage);
        Assert.Single(filteredPage.Items);
        Assert.Equal(2, filteredPage.Total);
        Assert.Equal(2, filteredPage.TotalPages);
        Assert.Equal(1, filteredPage.Page);
        Assert.Equal(1, filteredPage.Limit);
        Assert.Equal(email, filteredPage.Items[0].CustomerEmail);

        using var orderResponse = await client.GetAsync(
            $"/api/v1/notifications?orderId={secondMessage.OrderId}");
        var orderPage = await orderResponse.Content.ReadFromJsonAsync<NotificationPageResponse>();

        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);
        Assert.NotNull(orderPage);
        Assert.Single(orderPage.Items);
        Assert.Equal(secondMessage.OrderId, orderPage.Items[0].OrderId);

        using var openApiResponse = await client.GetAsync("/openapi/v1.json");
        var openApi = await openApiResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        Assert.Contains("/api/v1/notifications", openApi, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarksNotificationAsReadAndKeepsOperationIdempotent()
    {
        var message = CreateMessage($"{Guid.NewGuid():N}@example.com");
        await PersistAsync(message);

        await using var dbContext = fixture.CreateDbContext();
        var notificationId = await dbContext.Notifications
            .Where(notification => notification.SourceMessageId == message.MessageId)
            .Select(notification => notification.Id)
            .SingleAsync();

        using var firstReadResponse = await client.PostAsync(
            $"/api/v1/notifications/{notificationId}/read",
            content: null);
        var firstRead = await firstReadResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        using var secondReadResponse = await client.PostAsync(
            $"/api/v1/notifications/{notificationId}/read",
            content: null);

        Assert.Equal(HttpStatusCode.OK, firstReadResponse.StatusCode);
        Assert.True(firstRead!.IsRead);
        Assert.Equal(HttpStatusCode.OK, secondReadResponse.StatusCode);

        using var missingResponse = await client.PostAsync(
            $"/api/v1/notifications/{Guid.NewGuid()}/read",
            content: null);
        var missingProblem = await missingResponse.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal("NOTIFICATION_NOT_FOUND", missingProblem!.Code);
    }

    [Fact]
    public async Task RejectsInvalidNotificationQuery()
    {
        using var response = await client.GetAsync(
            "/api/v1/notifications?page=0&limit=101&customerEmail=not-an-email");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", problem!.Code);
    }

    private async Task PersistAsync(OrderConfirmedV1 message)
    {
        await using var dbContext = fixture.CreateDbContext();
        var handler = new OrderConfirmedNotificationHandler(
            dbContext,
            NullLogger<OrderConfirmedNotificationHandler>.Instance);
        await handler.HandleAsync(
            message,
            message.MessageId,
            "00-api-test",
            CancellationToken.None);
    }

    private static OrderConfirmedV1 CreateMessage(string email)
    {
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        return new OrderConfirmedV1(
            messageId,
            orderId,
            "Notification API test",
            email,
            250_000m,
            "VND",
            [new OrderConfirmedItemV1(productId, "Test product", 250_000m, 1, 250_000m)],
            DateTimeOffset.UtcNow);
    }

    private sealed record ProblemResponse(string Code);
}
