using MicroShop.OrderService.Features.Orders;
using MicroShop.OrderService.Infrastructure.Database;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.AddMicroShopServiceDefaults(builder.Services);

var configuration = builder.Configuration;
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (!context.ProblemDetails.Extensions.ContainsKey("code"))
        {
            context.ProblemDetails.Extensions["code"] = "INTERNAL_ERROR";
        }

        if (!context.ProblemDetails.Extensions.ContainsKey("traceId"))
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        }
    };
});
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IProductCatalogClient, FakeProductCatalogClient>();
builder.Services.AddScoped<OrderApplicationService>();
builder.Services.AddOptions<OrderDatabaseOptions>()
    .Configure(options =>
    {
        options.Host = configuration["ORDER_DB_HOST"] ?? configuration["OrderDatabase:Host"] ?? options.Host;
        options.Port = ParsePort(configuration["ORDER_DB_PORT"] ?? configuration["OrderDatabase:Port"], options.Port);
        options.Database = configuration["ORDER_DB_NAME"] ?? configuration["OrderDatabase:Database"] ?? options.Database;
        options.Username = configuration["ORDER_DB_USER"] ?? configuration["OrderDatabase:Username"] ?? options.Username;
        options.Password = configuration["ORDER_DB_PASSWORD"] ?? configuration["OrderDatabase:Password"] ?? options.Password;
        options.ConnectionString = configuration["ORDER_DB_CONNECTION_STRING"] ?? configuration["OrderDatabase:ConnectionString"];
    })
    .Validate(options => options.Port is >= 1 and <= 65535, "Order database port must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Order database host is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Database), "Order database name is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Order database user is required.")
    .Validate(options => options.HasConnectionCredentials, "Order database password or connection string is required.")
    .ValidateOnStart();
builder.Services.AddDbContext<OrderDbContext>((serviceProvider, options) =>
{
    var database = serviceProvider.GetRequiredService<IOptions<OrderDatabaseOptions>>().Value;
    options.UseNpgsql(database.BuildConnectionString(), npgsqlOptions =>
        npgsqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName));
});
builder.Services.AddHealthChecks().AddDbContextCheck<OrderDbContext>("order-database");

var app = builder.Build();

app.UseExceptionHandler();
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.MapMicroShopHealth(app);
app.MapGet("/", () => Results.Ok(new
{
    service = "order-service",
    status = "running",
    message = "Order API is available under /api/v1/orders using the Phase 2 fake Product catalog."
}));
OrderEndpoints.MapOrderEndpoints(app);

if (!app.Environment.IsProduction())
{
    app.MapOpenApi("/openapi/v1.json");
}

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await database.Database.MigrateAsync();
    return;
}

app.Run();

static int ParsePort(string? value, int fallback)
{
    return int.TryParse(value, out var port) ? port : fallback;
}

public partial class Program
{
}
