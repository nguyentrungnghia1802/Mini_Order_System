namespace MicroShop.Architecture.Tests;

public sealed class BootstrapConventionsTests
{
    [Fact]
    public void BaselineServiceNamesAreExplicitAndUnique()
    {
        var serviceNames = new[]
        {
            "product-service",
            "order-service",
            "notification-service",
            "gateway"
        };

        Assert.Equal(4, serviceNames.Length);
        Assert.Equal(serviceNames.Length, serviceNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("product-service", serviceNames);
        Assert.Contains("order-service", serviceNames);
        Assert.Contains("notification-service", serviceNames);
        Assert.Contains("gateway", serviceNames);
    }
}
