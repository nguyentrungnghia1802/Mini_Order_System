var builder = WebApplication.CreateBuilder(args);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.AddMicroShopServiceDefaults(builder.Services);
MicroShop.Gateway.BootstrapConfiguration.AddYarp(builder.Services, builder.Configuration);

var app = builder.Build();

MicroShop.ServiceDefaults.ServiceDefaultsExtensions.MapMicroShopHealth(app);
app.MapGet("/", () => Results.Ok(new
{
    service = "gateway",
    status = "bootstrap",
    message = "YARP gateway skeleton; public routes are added in Phase 4."
}));
app.MapReverseProxy();

app.Run();

public partial class Program
{
}
