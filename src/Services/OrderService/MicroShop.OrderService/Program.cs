using MassTransit;
using MicroShop.OrderService.Features.Orders;
using MicroShop.OrderService.Infrastructure.Database;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence;
using MicroShop.ServiceDefaults.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.AddMicroShopServiceDefaults(builder.Services);

var configuration = builder.Configuration;
var useInMemoryMessaging = builder.Environment.IsEnvironment("Testing")
    || ParseBool(
        configuration["RABBITMQ_USE_IN_MEMORY"] ?? configuration["RabbitMq:UseInMemory"],
        fallback: false);
builder.Services.AddOptions<RabbitMqOptions>()
    .Configure(options =>
    {
        options.Host = configuration["RABBITMQ_HOST"]
            ?? configuration["RabbitMq:Host"]
            ?? options.Host;
        options.Port = ParsePort(
            configuration["RABBITMQ_PORT"] ?? configuration["RabbitMq:Port"],
            options.Port);
        options.VirtualHost = configuration["RABBITMQ_VHOST"]
            ?? configuration["RabbitMq:VirtualHost"]
            ?? options.VirtualHost;
        options.Username = configuration["RABBITMQ_USER"]
            ?? configuration["RabbitMq:Username"]
            ?? options.Username;
        options.Password = configuration["RABBITMQ_PASSWORD"]
            ?? configuration["RabbitMq:Password"]
            ?? options.Password;
        options.UseInMemory = useInMemoryMessaging;
    })
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMQ host is required.")
    .Validate(options => options.Port is >= 1 and <= 65_535, "RabbitMQ port must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.VirtualHost), "RabbitMQ virtual host is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "RabbitMQ username is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMQ password is required.")
    .ValidateOnStart();
builder.Services.AddMassTransit(massTransit =>
{
    if (useInMemoryMessaging)
    {
        massTransit.UsingInMemory((context, bus) =>
        {
            bus.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(250)));
        });
        return;
    }

    massTransit.UsingRabbitMq((context, bus) =>
    {
        var rabbitMq = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        bus.Host(rabbitMq.Host, (ushort)rabbitMq.Port, rabbitMq.VirtualHost, host =>
        {
            host.Username(rabbitMq.Username);
            host.Password(rabbitMq.Password);
        });
        bus.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(250)));
    });
});
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
builder.Services.AddOptions<ProductServiceOptions>()
    .Configure(options =>
    {
        options.BaseUrl = configuration["PRODUCT_SERVICE_URL"]
            ?? configuration["ProductService:BaseUrl"]
            ?? options.BaseUrl;
        options.TimeoutMilliseconds = ParseInt(
            configuration["PRODUCT_SERVICE_TIMEOUT_MS"]
                ?? configuration["ProductService:TimeoutMilliseconds"],
            options.TimeoutMilliseconds);
        options.UseFakeClient = ParseBool(
            configuration["ORDER_PRODUCT_USE_FAKE"]
                ?? configuration["ProductService:UseFakeClient"],
            options.UseFakeClient);
    })
    .Validate(options =>
    {
        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }, "Product Service URL must be an absolute HTTP or HTTPS URL.")
    .Validate(options => options.TimeoutMilliseconds is >= 1 and <= 5_000,
        "Product Service timeout must be between 1 and 5000 milliseconds.")
    .ValidateOnStart();
builder.Services.AddHttpClient<ProductInventoryClient>((serviceProvider, httpClient) =>
{
    var productOptions = serviceProvider.GetRequiredService<IOptions<ProductServiceOptions>>().Value;
    httpClient.BaseAddress = new Uri(productOptions.BaseUrl.TrimEnd('/') + "/");
    httpClient.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton<FakeProductCatalogClient>();
builder.Services.AddScoped<IProductInventoryClient>(serviceProvider =>
{
    var productOptions = serviceProvider.GetRequiredService<IOptions<ProductServiceOptions>>().Value;
    return productOptions.UseFakeClient
        ? serviceProvider.GetRequiredService<FakeProductCatalogClient>()
        : serviceProvider.GetRequiredService<ProductInventoryClient>();
});
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
    message = "Order API is available under /api/v1/orders using the configured Product inventory client."
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

static int ParseInt(string? value, int fallback)
{
    return int.TryParse(value, out var parsed) ? parsed : fallback;
}

static bool ParseBool(string? value, bool fallback)
{
    return bool.TryParse(value, out var parsed) ? parsed : fallback;
}

public partial class Program
{
}
