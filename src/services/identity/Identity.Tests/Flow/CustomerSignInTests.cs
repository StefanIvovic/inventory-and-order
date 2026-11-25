using System.IdentityModel.Tokens.Jwt;
using Identity.Api.Pages.Account;
using Identity.Api.Users;

namespace Identity.Tests.Flow;

public sealed class CustomerSignInTests(ContainersFixture containers)
{
    [Fact]
    public async Task TheSameEmailSignsInToExactlyTheRequestedAccount()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        var globex = await app.CreateTenantAsync("globex");
        var atAcme = await app.CreateUserAsync("pat@mail.test", "Acme#Pass1", acme.Id, UserTypes.Customer);
        var atGlobex = await app.CreateUserAsync("pat@mail.test", "Globex#Pass1", globex.Id, UserTypes.Customer);
        var asStaff = await app.CreateUserAsync("pat@mail.test", "Staff#Pass1", acme.Id);

        Assert.Equal(atAcme.Id.ToString(), await SubjectAsync(app, "Pat@Mail.test", "Acme#Pass1", AuthorizeRequest.Storefront("acme")));
        Assert.Equal(atGlobex.Id.ToString(), await SubjectAsync(app, "pat@mail.test", "Globex#Pass1", AuthorizeRequest.Storefront("globex")));
        Assert.Equal(asStaff.Id.ToString(), await SubjectAsync(app, "pat@mail.test", "Staff#Pass1", new AuthorizeRequest()));
    }

    [Fact]
    public async Task AnotherTenantsPasswordIsTheGenericError()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        var globex = await app.CreateTenantAsync("globex");
        await app.CreateUserAsync("pat@mail.test", "Acme#Pass1", acme.Id, UserTypes.Customer);
        await app.CreateUserAsync("pat@mail.test", "Globex#Pass1", globex.Id, UserTypes.Customer);
        using var browser = app.CreateBrowser();
        var first = await browser.GetAsync(CodeFlow.AuthorizeUrl(AuthorizeRequest.Storefront("acme"), CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);

        var login = await CodeFlow.LoginAsync(browser, first.Headers.Location!.ToString(), "pat@mail.test", "Globex#Pass1");

        Assert.Contains(LoginModel.InvalidCredentials, await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomersAreLockedOutAfterFiveFailures()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        var acme = await app.CreateTenantAsync("acme");
        var customer = await app.CreateUserAsync("pat@mail.test", "Acme#Pass1", acme.Id, UserTypes.Customer);
        using var browser = app.CreateBrowser();
        var first = await browser.GetAsync(CodeFlow.AuthorizeUrl(AuthorizeRequest.Storefront("acme"), CodeFlow.NewPkce().Challenge), TestContext.Current.CancellationToken);
        var loginUrl = first.Headers.Location!.ToString();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await CodeFlow.LoginAsync(browser, loginUrl, "pat@mail.test", "Wrong#Pass1");
        }

        var locked = await CodeFlow.LoginAsync(browser, loginUrl, "pat@mail.test", "Acme#Pass1");

        Assert.Contains(LoginModel.LockedOut, await locked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        await app.WithUsersAsync(async users => Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(customer.Id.ToString()))!)));
    }

    private static async Task<string> SubjectAsync(IdentityApp app, string email, string password, AuthorizeRequest request)
    {
        var tokens = await CodeFlow.SignInAsync(app, email, password, request);
        return new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken).Subject;
    }
}
