var builder = WebApplication.CreateBuilder(args);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.AddMicroShopServiceDefaults(builder.Services);

var app = builder.Build();

MicroShop.ServiceDefaults.ServiceDefaultsExtensions.MapMicroShopHealth(app);
app.MapGet("/", () => Results.Ok(new
{
    service = "product-service",
    status = "bootstrap",
    message = "Product API is introduced in Phase 1."
}));

app.Run();

public partial class Program
{
}
