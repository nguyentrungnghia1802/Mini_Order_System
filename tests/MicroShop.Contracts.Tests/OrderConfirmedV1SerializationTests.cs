using System.Text.Json;
using MicroShop.Contracts.Orders;

namespace MicroShop.Contracts.Tests;

public sealed class OrderConfirmedV1SerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SerializesTheVersionedEventWithTheStableWireShape()
    {
        var messageId = Guid.Parse("c0a80101-0000-4000-8000-000000000001");
        var orderId = Guid.Parse("c0a80101-0000-4000-8000-000000000002");
        var productId = Guid.Parse("c0a80101-0000-4000-8000-000000000003");
        var occurredAtUtc = new DateTimeOffset(2026, 8, 18, 1, 2, 3, TimeSpan.Zero);
        var message = new OrderConfirmedV1(
            messageId,
            orderId,
            "Nguyen Van A",
            "a@example.com",
            1_250_000m,
            "VND",
            [new OrderConfirmedItemV1(productId, "Keyboard", 1_250_000m, 1, 1_250_000m)],
            occurredAtUtc);

        var json = JsonSerializer.Serialize(message, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(messageId, root.GetProperty("messageId").GetGuid());
        Assert.Equal(orderId, root.GetProperty("orderId").GetGuid());
        Assert.Equal("Nguyen Van A", root.GetProperty("customerName").GetString());
        Assert.Equal("a@example.com", root.GetProperty("customerEmail").GetString());
        Assert.Equal(1_250_000m, root.GetProperty("totalAmount").GetDecimal());
        Assert.Equal("VND", root.GetProperty("currency").GetString());
        Assert.Equal(occurredAtUtc, root.GetProperty("occurredAtUtc").GetDateTimeOffset());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());

        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(productId, item.GetProperty("productId").GetGuid());
        Assert.Equal("Keyboard", item.GetProperty("productName").GetString());
        Assert.Equal(1, item.GetProperty("quantity").GetInt32());
        Assert.Equal(1_250_000m, item.GetProperty("subtotal").GetDecimal());
    }

    [Fact]
    public void DeserializesTheDocumentedVersionOneFixture()
    {
        const string json = """
            {
              "messageId": "c0a80101-0000-4000-8000-000000000001",
              "orderId": "c0a80101-0000-4000-8000-000000000002",
              "customerName": "Nguyen Van A",
              "customerEmail": "a@example.com",
              "totalAmount": 1250000.00,
              "currency": "VND",
              "items": [
                {
                  "productId": "c0a80101-0000-4000-8000-000000000003",
                  "productName": "Keyboard",
                  "unitPrice": 1250000.00,
                  "quantity": 1,
                  "subtotal": 1250000.00
                }
              ],
              "occurredAtUtc": "2026-08-18T01:02:03Z",
              "schemaVersion": 1
            }
            """;

        var message = JsonSerializer.Deserialize<OrderConfirmedV1>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.Equal(Guid.Parse("c0a80101-0000-4000-8000-000000000001"), message.MessageId);
        Assert.Equal(Guid.Parse("c0a80101-0000-4000-8000-000000000002"), message.OrderId);
        Assert.Equal("a@example.com", message.CustomerEmail);
        Assert.Equal(1_250_000m, message.TotalAmount);
        Assert.Equal(1, message.SchemaVersion);
        var item = Assert.Single(message.Items);
        Assert.Equal("Keyboard", item.ProductName);
        Assert.Equal(1_250_000m, item.Subtotal);
    }
}
