using Testcontainers.RabbitMq;
using Xunit;

namespace BuildingBlocks.Testing;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    public const string ImageKey = "RABBITMQ_IMAGE";
    public const ushort ContainerPort = 5672;
    public const string DataPath = "/var/lib/rabbitmq";

    private readonly RabbitMqContainer container;

    public RabbitMqFixture()
    {
        TestInfrastructure.EnsureConfigured();
        container = new RabbitMqBuilder(ImageVersions.Get(ImageKey))
            .WithTmpfsMount(DataPath)
            .Build();
    }

    public string ConnectionString => container.GetConnectionString();

    public ushort HostPort => container.GetMappedPublicPort(ContainerPort);

    public string ContainerId => container.Id;

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
