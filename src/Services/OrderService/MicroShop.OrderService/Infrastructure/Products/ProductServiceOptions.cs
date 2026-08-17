namespace MicroShop.OrderService.Infrastructure.Products;

public sealed class ProductServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5245";

    public int TimeoutMilliseconds { get; set; } = 5_000;

    public bool UseFakeClient { get; set; }

    public TimeSpan Timeout => TimeSpan.FromMilliseconds(TimeoutMilliseconds);
}
