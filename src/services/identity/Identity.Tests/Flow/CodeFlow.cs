using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Tests.Flow;

public sealed record TokenResult(HttpStatusCode Status, string? AccessToken, string? RefreshToken, string? IdToken, int ExpiresIn, string? Error);

public sealed record AuthorizeRequest(
    string Scopes = "openid offline_access identity",
    string RedirectUri = IdentityApp.RedirectUri,
    bool Pkce = true,
    string ClientId = IdentityApp.Client);

public static partial class CodeFlow
{
    public static (string Verifier, string Challenge) NewPkce()
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public static string AuthorizeUrl(AuthorizeRequest request, string? challenge)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = request.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = request.RedirectUri,
            ["scope"] = request.Scopes,
            ["state"] = "state-123",
        };
        if (request.Pkce && challenge is not null)
        {
            query["code_challenge"] = challenge;
            query["code_challenge_method"] = "S256";
        }

        return "/connect/authorize?" + string.Join('&', query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string loginUrl, string email, string password)
    {
        ArgumentNullException.ThrowIfNull(browser);
        var page = await browser.GetStringAsync(loginUrl, TestContext.Current.CancellationToken);
        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = AntiforgeryToken(page),
        };
        var returnUrl = ReturnUrlPattern().Match(page);
        if (returnUrl.Success)
        {
            form["ReturnUrl"] = WebUtility.HtmlDecode(returnUrl.Groups[1].Value);
        }

        return await browser.PostAsync(loginUrl, new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
    }

    public static string AntiforgeryToken(string page)
    {
        var match = AntiforgeryPattern().Match(page);
        Assert.True(match.Success, "The page has no antiforgery token.");
        return match.Groups[1].Value;
    }

    public static async Task<(string Code, string Verifier)> AuthorizeAsync(
        HttpClient browser,
        string email,
        string password,
        AuthorizeRequest? request = null)
    {
        ArgumentNullException.ThrowIfNull(browser);
        request ??= new AuthorizeRequest();
        var (verifier, challenge) = NewPkce();
        var authorize = AuthorizeUrl(request, challenge);

        var first = await browser.GetAsync(authorize, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var callback = first.Headers.Location!;
        if (callback.ToString().Contains("/account/login", StringComparison.Ordinal))
        {
            var posted = await LoginAsync(browser, callback.ToString(), email, password);
            Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
            var back = await browser.GetAsync(posted.Headers.Location!, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
            callback = back.Headers.Location!;
        }

        Assert.StartsWith(request.RedirectUri, callback.ToString(), StringComparison.Ordinal);
        var code = Query(callback, "code");
        Assert.NotNull(code);
        return (code, verifier);
    }

    public static Task<TokenResult> RedeemAsync(HttpClient client, string code, string verifier, string secret = IdentityApp.ClientSecret) =>
        PostTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = IdentityApp.RedirectUri,
            ["client_id"] = IdentityApp.Client,
            ["client_secret"] = secret,
            ["code_verifier"] = verifier,
        });

    public static Task<TokenResult> RefreshAsync(HttpClient client, string refreshToken) =>
        PostTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = IdentityApp.Client,
            ["client_secret"] = IdentityApp.ClientSecret,
        });

    public static async Task<TokenResult> SignInAsync(
        IdentityApp app,
        string email = IdentityApp.AdminEmail,
        string password = IdentityApp.AdminPassword,
        AuthorizeRequest? request = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        await app.WaitUntilMigratedAsync();
        using var browser = app.CreateBrowser();
        var (code, verifier) = await AuthorizeAsync(browser, email, password, request);
        var tokens = await RedeemAsync(browser, code, verifier);
        Assert.Equal(HttpStatusCode.OK, tokens.Status);
        return tokens;
    }

    public static string? Query(Uri uri, string name)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var query = uri.IsAbsoluteUri ? uri.Query : uri.OriginalString[(uri.OriginalString.IndexOf('?', StringComparison.Ordinal) + 1)..];
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair[0] == name)
            {
                return Uri.UnescapeDataString(pair.Length > 1 ? pair[1] : string.Empty);
            }
        }

        return null;
    }

    private static async Task<TokenResult> PostTokenAsync(HttpClient client, Dictionary<string, string> form)
    {
        using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = json.RootElement;
        return new TokenResult(
            response.StatusCode,
            Text(root, "access_token"),
            Text(root, "refresh_token"),
            Text(root, "id_token"),
            root.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 0,
            Text(root, "error"));
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetString() : null;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();

    [GeneratedRegex("name=\"ReturnUrl\" value=\"([^\"]*)\"")]
    private static partial Regex ReturnUrlPattern();
}
