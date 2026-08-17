namespace MicroShop.ServiceDefaults.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string Username { get; set; } = "microshop";

    public string Password { get; set; } = "change-me-rabbitmq";

    public bool UseInMemory { get; set; }
}
