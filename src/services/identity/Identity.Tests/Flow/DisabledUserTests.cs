using System.Net;
using Identity.Api.Pages.Account;
using Identity.Tests.Users;

namespace Identity.Tests.Flow;

public sealed class DisabledUserTests(ContainersFixture containers)
{
    [Fact]
    public async Task DisabledUserCannotRefreshAuthorizeOrSignIn()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var tenant = await app.CreateTenantAsync("acme");
        await UserSetup.CreateStaffAsync(app, tenant, "clerk@acme.test");
        using var browser = app.CreateBrowser();
        var (code, verifier) = await CodeFlow.AuthorizeAsync(browser, "clerk@acme.test", StaffUsersTests.StaffPassword);
        var tokens = await CodeFlow.RedeemAsync(browser, code, verifier);
        var clerk = await app.FindUserAsync("clerk@acme.test");
        using var client = app.CreateClient();

        await app.TrackAsync(() => client.PostAsync($"/users/{clerk.Id}/disable", null, IdentityApp.StaffToken(tenant.AdminUserId, tenant.Id)));

        Assert.Equal("invalid_grant", (await CodeFlow.RefreshAsync(client, tokens.RefreshToken!)).Error);
        var again = await browser.GetAsync(CodeFlow.AuthorizeUrl(new AuthorizeRequest(), CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("code=", again.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal);

        using var fresh = app.CreateBrowser();
        var login = await CodeFlow.LoginAsync(fresh, "/account/login", "clerk@acme.test", StaffUsersTests.StaffPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(LoginModel.InvalidCredentials, await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(0, (await app.FindUserAsync("clerk@acme.test")).AccessFailedCount);
    }
}
