using MassTransit;
using MicroShop.OrderService.Features.Orders;
using MicroShop.OrderService.Features.Reconciliation;
using MicroShop.OrderService.Infrastructure.Database;
using MicroShop.OrderService.Infrastructure.Messaging;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence;
using MicroShop.ServiceDefaults;
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
builder.Services.AddOptions<MassTransitHostOptions>()
    .Configure<IOptions<MicroShopHostOptions>>((options, hostOptions) =>
    {
        options.WaitUntilStarted = true;
        options.StartTimeout = hostOptions.Value.ShutdownTimeout;
        options.StopTimeout = hostOptions.Value.ShutdownTimeout;
    });
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
        options.SafeRetryCount = ParseInt(
            configuration["PRODUCT_SERVICE_SAFE_RETRY_COUNT"]
                ?? configuration["ProductService:SafeRetryCount"],
            options.SafeRetryCount);
        options.SafeRetryDelayMilliseconds = ParseInt(
            configuration["PRODUCT_SERVICE_SAFE_RETRY_DELAY_MS"]
                ?? configuration["ProductService:SafeRetryDelayMilliseconds"],
            options.SafeRetryDelayMilliseconds);
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
    .Validate(options => options.SafeRetryCount is >= 0 and <= 3,
        "Product Service safe retry count must be between 0 and 3.")
    .Validate(options => options.SafeRetryDelayMilliseconds is >= 10 and <= 2_000,
        "Product Service safe retry delay must be between 10 and 2000 milliseconds.")
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
builder.Services.AddScoped<OrderReconciliationService>();
var outboxEnabled = !builder.Environment.IsEnvironment("Testing")
    && ParseBool(
        configuration["ORDER_OUTBOX_ENABLED"] ?? configuration["OrderOutbox:Enabled"],
        fallback: true);
builder.Services.AddOptions<OutboxOptions>()
    .Configure(options =>
    {
        options.Enabled = outboxEnabled;
        options.MaxAttempts = ParseInt(
            configuration["ORDER_OUTBOX_MAX_ATTEMPTS"] ?? configuration["OrderOutbox:MaxAttempts"],
            options.MaxAttempts);
        options.PollInterval = ParseMilliseconds(
            configuration["ORDER_OUTBOX_POLL_INTERVAL_MS"] ?? configuration["OrderOutbox:PollIntervalMilliseconds"],
            options.PollInterval);
        options.LeaseDuration = ParseMilliseconds(
            configuration["ORDER_OUTBOX_LEASE_DURATION_MS"] ?? configuration["OrderOutbox:LeaseDurationMilliseconds"],
            options.LeaseDuration);
        options.RetryBaseDelay = ParseMilliseconds(
            configuration["ORDER_OUTBOX_RETRY_BASE_DELAY_MS"] ?? configuration["OrderOutbox:RetryBaseDelayMilliseconds"],
            options.RetryBaseDelay);
        options.RetryMaxDelay = ParseMilliseconds(
            configuration["ORDER_OUTBOX_RETRY_MAX_DELAY_MS"] ?? configuration["OrderOutbox:RetryMaxDelayMilliseconds"],
            options.RetryMaxDelay);
        options.MaxPendingMessages = ParseInt(
            configuration["ORDER_OUTBOX_MAX_PENDING_MESSAGES"] ?? configuration["OrderOutbox:MaxPendingMessages"],
            options.MaxPendingMessages);
        options.MaxPendingAge = ParseMilliseconds(
            configuration["ORDER_OUTBOX_MAX_PENDING_AGE_MS"] ?? configuration["OrderOutbox:MaxPendingAgeMilliseconds"],
            options.MaxPendingAge);
        options.BacklogLogInterval = ParseMilliseconds(
            configuration["ORDER_OUTBOX_BACKLOG_LOG_INTERVAL_MS"] ?? configuration["OrderOutbox:BacklogLogIntervalMilliseconds"],
            options.BacklogLogInterval);
        options.FailReadinessOnDeadLettered = ParseBool(
            configuration["ORDER_OUTBOX_FAIL_READINESS_ON_DEAD_LETTERED"]
                ?? configuration["OrderOutbox:FailReadinessOnDeadLettered"],
            options.FailReadinessOnDeadLettered);
    })
    .Validate(options => options.MaxAttempts is >= 1 and <= 100, "Order outbox max attempts must be between 1 and 100.")
    .Validate(options => options.PollInterval >= TimeSpan.FromMilliseconds(50), "Order outbox poll interval must be at least 50 milliseconds.")
    .Validate(options => options.LeaseDuration >= TimeSpan.FromSeconds(1), "Order outbox lease duration must be at least 1 second.")
    .Validate(options => options.RetryBaseDelay > TimeSpan.Zero, "Order outbox retry base delay must be positive.")
    .Validate(options => options.RetryMaxDelay >= options.RetryBaseDelay, "Order outbox retry max delay must not be lower than the base delay.")
    .Validate(options => options.MaxPendingMessages is >= 1 and <= 1_000_000, "Order outbox max pending messages must be between 1 and 1000000.")
    .Validate(options => options.MaxPendingAge > TimeSpan.Zero, "Order outbox max pending age must be positive.")
    .Validate(options => options.BacklogLogInterval >= TimeSpan.FromSeconds(1), "Order outbox backlog log interval must be at least 1 second.")
    .ValidateOnStart();
builder.Services.AddScoped<IOrderOutboxWriter, OrderOutboxWriter>();
builder.Services.AddScoped<IOrderConfirmedMessageTransport, MassTransitOrderConfirmedMessageTransport>();
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
var healthChecks = builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("order-database");
if (outboxEnabled)
{
    healthChecks.AddCheck<OrderOutboxHealthCheck>("order-outbox");
    builder.Services.AddHostedService<OutboxDispatcher>();
}

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
ReconciliationEndpoints.MapReconciliationEndpoints(app);

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

static TimeSpan ParseMilliseconds(string? value, TimeSpan fallback)
{
    return int.TryParse(value, out var milliseconds) && milliseconds > 0
        ? TimeSpan.FromMilliseconds(milliseconds)
        : fallback;
}

static bool ParseBool(string? value, bool fallback)
{
    return bool.TryParse(value, out var parsed) ? parsed : fallback;
}

public partial class Program
{
}
