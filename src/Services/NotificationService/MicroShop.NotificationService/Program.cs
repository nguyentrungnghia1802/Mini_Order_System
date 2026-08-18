using MassTransit;
using MicroShop.NotificationService.Features.Messaging;
using MicroShop.NotificationService.Features.Notifications;
using MicroShop.NotificationService.Infrastructure.Database;
using MicroShop.NotificationService.Infrastructure.Messaging;
using MicroShop.NotificationService.Persistence;
using MicroShop.ServiceDefaults;
using MicroShop.ServiceDefaults.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.AddMicroShopServiceDefaults(builder.Services);

var configuration = builder.Configuration;
builder.Services.AddOptions<NotificationDatabaseOptions>()
    .Configure(options =>
    {
        options.Host = configuration["NOTIFICATION_DB_HOST"]
            ?? configuration["NotificationDatabase:Host"]
            ?? options.Host;
        options.Port = ParsePort(
            configuration["NOTIFICATION_DB_PORT"] ?? configuration["NotificationDatabase:Port"],
            options.Port);
        options.Database = configuration["NOTIFICATION_DB_NAME"]
            ?? configuration["NotificationDatabase:Database"]
            ?? options.Database;
        options.Username = configuration["NOTIFICATION_DB_USER"]
            ?? configuration["NotificationDatabase:Username"]
            ?? options.Username;
        options.Password = configuration["NOTIFICATION_DB_PASSWORD"]
            ?? configuration["NotificationDatabase:Password"]
            ?? options.Password;
        options.ConnectionString = configuration["NOTIFICATION_DB_CONNECTION_STRING"]
            ?? configuration["NotificationDatabase:ConnectionString"];
    })
    .Validate(options => options.Port is >= 1 and <= 65_535, "Notification database port must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Notification database host is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Database), "Notification database name is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Notification database user is required.")
    .Validate(options => options.HasConnectionCredentials, "Notification database password or connection string is required.")
    .ValidateOnStart();
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
builder.Services.AddDbContext<NotificationDbContext>((serviceProvider, options) =>
{
    var database = serviceProvider.GetRequiredService<IOptions<NotificationDatabaseOptions>>().Value;
    options.UseNpgsql(database.BuildConnectionString(), npgsqlOptions =>
        npgsqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName));
});
builder.Services.AddScoped<OrderConfirmedNotificationHandler>();
builder.Services.AddOptions<NotificationMessagingOptions>()
    .Configure(options =>
    {
        options.RetryCount = ParseInt(
            configuration["NOTIFICATION_CONSUMER_RETRY_COUNT"]
                ?? configuration["NotificationMessaging:RetryCount"],
            options.RetryCount);
        options.RetryDelay = ParseMilliseconds(
            configuration["NOTIFICATION_CONSUMER_RETRY_DELAY_MS"]
                ?? configuration["NotificationMessaging:RetryDelayMilliseconds"],
            options.RetryDelay);
    })
    .Validate(options => options.RetryCount is >= 1 and <= 20,
        "Notification consumer retry count must be between 1 and 20.")
    .Validate(options => options.RetryDelay > TimeSpan.Zero,
        "Notification consumer retry delay must be positive.")
    .ValidateOnStart();
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
    massTransit.AddConsumer<OrderConfirmedConsumer>();
    if (useInMemoryMessaging)
    {
        massTransit.UsingInMemory((context, bus) =>
        {
            var retryOptions = context.GetRequiredService<IOptions<NotificationMessagingOptions>>().Value;
            bus.UseMessageRetry(retry => retry.Interval(retryOptions.RetryCount, retryOptions.RetryDelay));
            bus.ConfigureEndpoints(context);
        });
        return;
    }

    massTransit.UsingRabbitMq((context, bus) =>
    {
        var retryOptions = context.GetRequiredService<IOptions<NotificationMessagingOptions>>().Value;
        var rabbitMq = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        bus.Host(rabbitMq.Host, (ushort)rabbitMq.Port, rabbitMq.VirtualHost, host =>
        {
            host.Username(rabbitMq.Username);
            host.Password(rabbitMq.Password);
        });
        bus.ReceiveEndpoint("microshop-notification-order-confirmed-v1", endpoint =>
        {
            endpoint.Durable = true;
            endpoint.AutoDelete = false;
            endpoint.PrefetchCount = 16;
            endpoint.UseMessageRetry(retry => retry.Interval(retryOptions.RetryCount, retryOptions.RetryDelay));
            endpoint.ConfigureConsumer<OrderConfirmedConsumer>(context);
        });
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>("notification-database");

var app = builder.Build();

app.UseExceptionHandler();
MicroShop.ServiceDefaults.ServiceDefaultsExtensions.MapMicroShopHealth(app);
app.MapGet("/", () => Results.Ok(new
{
    service = "notification-service",
    status = "running",
    message = "Notification API is available under /api/v1/notifications."
}));
NotificationEndpoints.MapNotificationEndpoints(app);

if (!app.Environment.IsProduction())
{
    app.MapOpenApi("/openapi/v1.json");
}

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
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
