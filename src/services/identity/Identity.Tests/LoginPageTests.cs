using System.Net;
using Identity.Api.Pages.Account;
using Identity.Tests.Flow;

namespace Identity.Tests;

public sealed class LoginPageTests(ContainersFixture containers)
{
    private const string LoginUrl = "/account/login";

    [Fact]
    public async Task UnknownEmailAndWrongPasswordLookTheSame()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();

        var unknown = await CodeFlow.LoginAsync(browser, LoginUrl, "nobody@platform.test", "Whatever#1");
        var wrong = await CodeFlow.LoginAsync(browser, LoginUrl, IdentityApp.AdminEmail, "Wrong#Pass1");

        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
        Assert.Contains(LoginModel.InvalidCredentials, await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains(LoginModel.InvalidCredentials, await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiveFailuresLockTheAccountUntilTheLockoutEnds()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await CodeFlow.LoginAsync(browser, LoginUrl, IdentityApp.AdminEmail, "Wrong#Pass1");
        }

        var locked = await CodeFlow.LoginAsync(browser, LoginUrl, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        Assert.Contains(LoginModel.LockedOut, await locked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        await app.WithUsersAsync(async users =>
        {
            var admin = (await users.FindByNameAsync(IdentityApp.AdminEmail))!;
            Assert.True(admin.LockoutEnd > DateTimeOffset.UtcNow.AddMinutes(9));
            await users.SetLockoutEndDateAsync(admin, DateTimeOffset.UtcNow.AddSeconds(-1));
        });

        var unlocked = await CodeFlow.LoginAsync(browser, LoginUrl, IdentityApp.AdminEmail, IdentityApp.AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, unlocked.StatusCode);
        await app.WithUsersAsync(async users =>
            Assert.Equal(0, (await users.FindByNameAsync(IdentityApp.AdminEmail))!.AccessFailedCount));
    }

    [Fact]
    public async Task PostWithoutAntiforgeryTokenIsRejected()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();

        var response = await browser.PostAsync(
            LoginUrl,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = IdentityApp.AdminEmail, ["Password"] = IdentityApp.AdminPassword }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(LoginUrl + "?ReturnUrl=https%3A%2F%2Fevil.test%2F")]
    [InlineData(LoginUrl)]
    public async Task WithoutLocalReturnUrlSignInLandsOnSignedInPage(string url)
    {
        await using var app = await IdentityApp.StartAsync(containers);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();

        var response = await CodeFlow.LoginAsync(browser, url, IdentityApp.AdminEmail, IdentityApp.AdminPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/signed-in", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task PageIsResponsiveAndServerRendered()
    {
        await using var app = await IdentityApp.StartAsync(containers);
        using var browser = app.CreateBrowser();

        var page = await browser.GetStringAsync(LoginUrl, TestContext.Current.CancellationToken);

        Assert.Contains("name=\"viewport\" content=\"width=device-width, initial-scale=1\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
    }
}
