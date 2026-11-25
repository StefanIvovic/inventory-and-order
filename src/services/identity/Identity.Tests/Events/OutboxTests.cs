using System.Net;
using BuildingBlocks.Messaging;
using Identity.Contracts;
using Identity.Tests.Tenants;
using Identity.Tests.Users;
using Wolverine.Tracking;

namespace Identity.Tests.Events;

public sealed class OutboxTests(ContainersFixture containers)
{
    [Fact]
    public async Task TenantEventsCarryTheNewTenantAndTheInitiatingUser()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var platformAdmin = await app.FindUserAsync(IdentityApp.AdminEmail);
        CreatedTenant? tenant = null;

        var session = await app.TrackAsync(async () => tenant = await app.CreateTenantAsync("acme"));

        foreach (var envelope in session.Sent.Envelopes().Where(e => e.Message is TenantCreated or UserRegistered))
        {
            Assert.Equal(tenant!.Id, envelope.TenantId);
            Assert.Equal(platformAdmin.Id.ToString(), envelope.Headers[MessageHeaders.UserId]);
            Assert.False(string.IsNullOrEmpty(envelope.CorrelationId));
        }
    }

    [Fact]
    public async Task UserDisabledCarriesTheTenantAndTheAdministrator()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        var clerk = await UserSetup.CreateStaffAsync(app, tenant, "clerk@acme.test");
        using var client = app.CreateClient();

        var session = await app.TrackAsync(() => client.PostAsync($"/users/{clerk}/disable", null, IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id)));

        var envelope = Assert.Single(session.Sent.Envelopes(), e => e.Message is UserDisabled);
        Assert.Equal(tenant.Id, envelope.TenantId);
        Assert.Equal(tenant.AdminUserId.ToString(), envelope.Headers[MessageHeaders.UserId]);
    }

    [Fact]
    public async Task RacingCreationsPublishOnlyForTheOneThatCommitted()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var client = app.CreateClient();
        var token = await app.PlatformTokenAsync();
        HttpResponseMessage[] responses = [];

        var session = await app.TrackAsync(async () =>
            responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => client.PostAsync("/tenants", CreateTenantTests.Body("acme", $"admin{i}@acme.test"), token))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(session.Sent.MessagesOf<TenantCreated>());
        Assert.Single(session.Sent.MessagesOf<UserRegistered>());
    }
}
