using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MicroShop.NotificationService.Tests;

public sealed class NotificationBootstrapTests
    : IClassFixture<NotificationApiFactory>
{
    private readonly HttpClient client;

    public NotificationBootstrapTests(NotificationApiFactory factory)
    {
        client = factory.CreateClient();
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

public sealed class NotificationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RABBITMQ_USE_IN_MEMORY"] = "true"
            });
        });
    }
}
