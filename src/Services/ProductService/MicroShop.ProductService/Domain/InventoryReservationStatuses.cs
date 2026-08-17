namespace MicroShop.ProductService.Domain;

public static class InventoryReservationStatuses
{
    public const string Reserved = "reserved";

    public const string Released = "released";

    public static bool IsKnown(string status)
    {
        return status is Reserved or Released;
    }
}
