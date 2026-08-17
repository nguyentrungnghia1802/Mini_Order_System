namespace MicroShop.OrderService.Infrastructure.Products;

public static class FakeProductCatalog
{
    public static readonly Guid MechanicalKeyboardId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid WirelessMouseId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid UsbCHubId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Guid ArchivedHeadsetId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static IReadOnlyDictionary<Guid, FakeProduct> Products { get; } =
        new Dictionary<Guid, FakeProduct>
        {
            [MechanicalKeyboardId] = new(
                MechanicalKeyboardId,
                "Mechanical Keyboard",
                1_200_000m,
                10,
                true),
            [WirelessMouseId] = new(
                WirelessMouseId,
                "Wireless Mouse",
                450_000m,
                20,
                true),
            [UsbCHubId] = new(
                UsbCHubId,
                "USB-C Hub",
                800_000m,
                0,
                true),
            [ArchivedHeadsetId] = new(
                ArchivedHeadsetId,
                "Archived Headset",
                600_000m,
                5,
                false)
        };
}

public sealed record FakeProduct(
    Guid Id,
    string Name,
    decimal UnitPrice,
    int AvailableStock,
    bool IsActive);
