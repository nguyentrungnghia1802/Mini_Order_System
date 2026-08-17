using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MicroShop.NotificationService.Tests;

public sealed class NotificationBootstrapTests
    : IClassFixture<NotificationDatabaseFixture>
{
    private readonly HttpClient client;

    public NotificationBootstrapTests(NotificationDatabaseFixture fixture)
    {
        client = fixture.CreateClient();
    }

    [Fact]
    public async Task NotificationHostExposesLiveAndReadyHealthWithInMemoryTestBus()
    {
        using var rootResponse = await client.GetAsync("/");
        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");

        Assert.Equal(System.Net.HttpStatusCode.OK, rootResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, readyResponse.StatusCode);
    }

}

public sealed class NotificationApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NOTIFICATION_DB_CONNECTION_STRING"] = connectionString,
                ["RABBITMQ_USE_IN_MEMORY"] = "true"
            });
        });
    }
}
