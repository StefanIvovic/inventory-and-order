using BuildingBlocks.Testing;

[assembly: AssemblyFixture(typeof(BuildingBlocks.Messaging.Tests.MessagingFixture))]

namespace BuildingBlocks.Messaging.Tests;

public sealed class MessagingFixture : IAsyncLifetime
{
    public PostgresFixture Postgres { get; } = new();

    public RabbitMqFixture RabbitMq { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await RabbitMq.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SequentialMessaging
{
    public const string Name = "sequential-messaging";
}
