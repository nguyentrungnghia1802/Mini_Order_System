using MicroShop.ProductService.Features.Products;
using MicroShop.ProductService.Infrastructure.Database;
using MicroShop.ProductService.Persistence;
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
builder.Services.AddOptions<ProductDatabaseOptions>()
    .Configure(options =>
    {
        options.Host = configuration["PRODUCT_DB_HOST"] ?? configuration["ProductDatabase:Host"] ?? options.Host;
        options.Port = ParsePort(configuration["PRODUCT_DB_PORT"] ?? configuration["ProductDatabase:Port"], options.Port);
        options.Database = configuration["PRODUCT_DB_NAME"] ?? configuration["ProductDatabase:Database"] ?? options.Database;
        options.Username = configuration["PRODUCT_DB_USER"] ?? configuration["ProductDatabase:Username"] ?? options.Username;
        options.Password = configuration["PRODUCT_DB_PASSWORD"] ?? configuration["ProductDatabase:Password"] ?? options.Password;
        options.ConnectionString = configuration["PRODUCT_DB_CONNECTION_STRING"] ?? configuration["ProductDatabase:ConnectionString"];
    })
    .Validate(options => options.Port is >= 1 and <= 65535, "Product database port must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Product database host is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Database), "Product database name is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Product database user is required.")
    .Validate(options => options.HasConnectionCredentials, "Product database password or connection string is required.")
    .ValidateOnStart();
builder.Services.AddDbContext<ProductDbContext>((serviceProvider, options) =>
{
    var database = serviceProvider.GetRequiredService<IOptions<ProductDatabaseOptions>>().Value;
    options.UseNpgsql(database.BuildConnectionString(), npgsqlOptions =>
        npgsqlOptions.MigrationsAssembly(typeof(ProductDbContext).Assembly.FullName));
});
builder.Services.AddHealthChecks().AddDbContextCheck<ProductDbContext>("product-database");

var app = builder.Build();

app.UseExceptionHandler();
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.MapMicroShopHealth(app);
app.MapGet("/", () => Results.Ok(new
{
    service = "product-service",
    status = "running",
    message = "Product API is available under /api/v1/products."
}));
ProductEndpoints.MapProductEndpoints(app);

if (!app.Environment.IsProduction())
{
    app.MapOpenApi("/openapi/v1.json");
}

if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await database.Database.MigrateAsync();
    await ProductSeed.SeedAsync(database);
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
