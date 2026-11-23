using RabbitMQ.Client;

namespace BuildingBlocks.Testing.Tests;

public sealed class RabbitMqFixtureTests(RabbitMqFixture rabbit) : IClassFixture<RabbitMqFixture>
{
    [Fact]
    public async Task ClientOpensConnectionAndChannel()
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(connection.IsOpen);
        Assert.True(channel.IsOpen);
    }

    [Fact]
    public async Task ContainerUsesPinnedImage()
    {
        var inspect = await DockerCli.InspectAsync(rabbit.ContainerId);

        Assert.Equal(ImageVersions.Get(RabbitMqFixture.ImageKey), DockerCli.ImageOf(inspect));
    }

    [Fact]
    public void HostPortIsRandomNotDefault()
    {
        Assert.NotEqual(RabbitMqFixture.ContainerPort, rabbit.HostPort);
    }

    [Fact]
    public async Task DataIsInMemoryWithoutVolumes()
    {
        var inspect = await DockerCli.InspectAsync(rabbit.ContainerId);

        Assert.Contains(RabbitMqFixture.DataPath, DockerCli.TmpfsOf(inspect));
        Assert.Empty(DockerCli.VolumeMountsOf(inspect));
    }

    [Fact]
    public async Task DisposeRemovesContainer()
    {
        var fixture = new RabbitMqFixture();
        await fixture.InitializeAsync();
        var id = fixture.ContainerId;
        Assert.True(await DockerCli.ExistsAsync(id));

        await fixture.DisposeAsync();

        Assert.False(await DockerCli.ExistsAsync(id));
    }
}
