using System.Net.Http.Json;
using BuildingBlocks.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Wolverine;

namespace BuildingBlocks.Messaging.Tests;

[Collection(SequentialMessaging.Name)]
public sealed class PermissionInvalidationTests(MessagingFixture infra)
{
    private const string Adjust = "inventory.adjust";
    private static readonly Guid U1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid U2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Platform = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly ServiceHostOptions Instance = new(DurabilityMode.Balanced, DbPermissions: true, PermissionInvalidation: true);

    [Fact]
    public async Task CommittedChangeInvalidatesEveryInstanceOnlyForThatUser()
    {
        var service = TestServiceHost.UniqueName("inv");
        var database = await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var a = await StartAsync(service, database);
        await using var b = await StartAsync(service, database);
        await using var other = await StartAsync(TestServiceHost.UniqueName("other"), await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken));

        foreach (var host in new[] { a, b, other })
        {
            await PermissionsAsync(host, U1, "acme");
        }

        await PermissionsAsync(a, U2, "acme");
        await PermissionsAsync(b, U2, "acme");

        await Bus(a).InvokeForTenantAsync("acme", new GrantAccess(U1, Adjust, FailAfterAdd: false), TestContext.Current.CancellationToken);

        await Waiting.UntilAsync(() => Contains(a, U1, Adjust) && Contains(b, U1, Adjust), TimeSpan.FromSeconds(60), "both instances serve the new permission");
        Assert.Equal(2, a.ProviderCalls.For(U1));
        Assert.Equal(2, b.ProviderCalls.For(U1));

        await PermissionsAsync(a, U2, "acme");
        await PermissionsAsync(b, U2, "acme");
        Assert.Equal(1, a.ProviderCalls.For(U2));
        Assert.Equal(1, b.ProviderCalls.For(U2));

        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Empty(await PermissionsAsync(other, U1, "acme"));
        Assert.Equal(1, other.ProviderCalls.For(U1));

        await using var c = await StartAsync(service, database);
        Assert.Contains(Adjust, await PermissionsAsync(c, U1, "acme"));
    }

    [Fact]
    public async Task PlatformUserWithoutTenantIsInvalidated()
    {
        var service = TestServiceHost.UniqueName("inv");
        var database = await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var a = await StartAsync(service, database);
        await using var b = await StartAsync(service, database);

        await PermissionsAsync(b, Platform, null);
        Assert.Equal(1, b.ProviderCalls.For(Platform));

        await Bus(a).PublishAsync(new PermissionAssignmentsChanged(null, Platform));

        await Waiting.UntilAsync(() =>
        {
            PermissionsAsync(b, Platform, null).GetAwaiter().GetResult();
            return b.ProviderCalls.For(Platform) == 2;
        }, TimeSpan.FromSeconds(60), "the platform user entry is invalidated on the other instance");
    }

    [Fact]
    public async Task FailedChangeInvalidatesNothing()
    {
        var service = TestServiceHost.UniqueName("inv");
        var database = await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var a = await StartAsync(service, database);
        await using var b = await StartAsync(service, database);
        await PermissionsAsync(a, U1, "acme");
        await PermissionsAsync(b, U1, "acme");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Bus(a).InvokeForTenantAsync("acme", new GrantAccess(U1, Adjust, FailAfterAdd: true), TestContext.Current.CancellationToken));
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Empty(await PermissionsAsync(a, U1, "acme"));
        Assert.Empty(await PermissionsAsync(b, U1, "acme"));
        Assert.Equal(1, a.ProviderCalls.For(U1));
        Assert.Equal(1, b.ProviderCalls.For(U1));
        Assert.Equal(0, await CountGrantsAsync(a));
    }

    [Fact]
    public async Task StoppedInstanceLeavesNoQueueBehind()
    {
        var service = TestServiceHost.UniqueName("inv");
        var database = await infra.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var a = await StartAsync(service, database);
        var b = await StartAsync(service, database);
        var queue = b.App.Services.GetRequiredService<PermissionInvalidationEndpoint>().InstanceQueue;
        var cancellation = TestContext.Current.CancellationToken;

        await using var connection = await new ConnectionFactory { Uri = new Uri(infra.RabbitMq.ConnectionString) }.CreateConnectionAsync(cancellation);
        Assert.Equal(405, PassiveDeclareReplyCode(connection, queue));

        await b.DisposeAsync();

        await Waiting.UntilAsync(() => PassiveDeclareReplyCode(connection, queue) == 404, TimeSpan.FromSeconds(30), "the stopped instance queue is deleted");
    }

    private static int PassiveDeclareReplyCode(IConnection connection, string queue)
    {
        try
        {
            using var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            channel.QueueDeclarePassiveAsync(queue).GetAwaiter().GetResult();
            return 200;
        }
        catch (OperationInterruptedException exception)
        {
            return exception.ShutdownReason?.ReplyCode ?? 0;
        }
    }

    private async Task<TestServiceHost> StartAsync(string service, string database) =>
        await TestServiceHost.StartAsync(service, database, new Uri(infra.RabbitMq.ConnectionString), _ => { }, Instance);

    private static IMessageBus Bus(TestServiceHost host) => host.App.Services.GetRequiredService<IMessageBus>();

    private static bool Contains(TestServiceHost host, Guid user, string permission) =>
        PermissionsAsync(host, user, "acme").GetAwaiter().GetResult().Contains(permission);

    private static async Task<string[]> PermissionsAsync(TestServiceHost host, Guid user, string? tenant)
    {
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/permissions");
        request.Headers.Add(TestAuthHandler.UserHeader, user.ToString());
        if (tenant is not null)
        {
            request.Headers.Add(TestAuthHandler.TenantHeader, tenant);
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<string[]>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<int> CountGrantsAsync(TestServiceHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<TestDbContext>>();
        await using var db = new TestDbContext(options, new TenantContext());
        return await db.PermissionGrants.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
    }
}
