var builder = WebApplication.CreateBuilder(args);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.AddMicroShopServiceDefaults(builder.Services);

var app = builder.Build();

MicroShop.ServiceDefaults.ServiceDefaultsExtensions.MapMicroShopHealth(app);
app.MapGet("/", () => Results.Ok(new
{
    service = "notification-service",
    status = "bootstrap",
    message = "Notification consumer/API is introduced in Phase 5."
}));

app.Run();

public partial class Program
{
}
