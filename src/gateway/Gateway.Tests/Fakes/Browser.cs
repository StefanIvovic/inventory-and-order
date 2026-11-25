using System.Net;
using System.Text.RegularExpressions;

namespace Gateway.Tests.Fakes;

public sealed record BrowserResponse(HttpResponseMessage Response, Uri Uri, string Body)
{
    public HttpStatusCode Status => Response.StatusCode;
}

public sealed partial class Browser(HttpClient gateway, Uri origin) : IDisposable
{
    private const int MaxHops = 20;

    public CookieContainer Cookies { get; } = new();

    public List<string> SetCookieHeaders { get; } = [];

    public List<BrowserResponse> History { get; } = [];

    public Uri Origin => origin;

    public Task<BrowserResponse> GetAsync(string pathOrUrl, Action<HttpRequestMessage>? configure = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, Resolve(pathOrUrl)), configure);

    public async Task<BrowserResponse> SendAsync(HttpRequestMessage request, Action<HttpRequestMessage>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.RequestUri = Resolve(request.RequestUri?.OriginalString ?? "/");
        configure?.Invoke(request);
        var cookie = Cookies.GetCookieHeader(request.RequestUri!);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var response = await gateway.SendAsync(request, TestContext.Current.CancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var header in setCookies)
            {
                SetCookieHeaders.Add(header);
                Cookies.SetCookies(request.RequestUri!, header);
            }
        }

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = new BrowserResponse(response, request.RequestUri!, body);
        History.Add(result);
        return result;
    }

    public async Task<BrowserResponse> NavigateAsync(string pathOrUrl, Action<HttpRequestMessage>? configure = null)
    {
        var current = await GetAsync(pathOrUrl, configure);
        for (var hop = 0; hop < MaxHops; hop++)
        {
            if (current.Status is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect)
            {
                var location = current.Response.Headers.Location!;
                current = await GetAsync(location.IsAbsoluteUri ? location.ToString() : new Uri(current.Uri, location).ToString());
                continue;
            }

            var form = FormRegex().Match(current.Body);
            if (current.Status == HttpStatusCode.OK && form.Success)
            {
                var fields = InputRegex().Matches(form.Groups["inputs"].Value)
                    .ToDictionary(match => WebUtility.HtmlDecode(match.Groups["name"].Value), match => WebUtility.HtmlDecode(match.Groups["value"].Value));
                var post = new HttpRequestMessage(HttpMethod.Post, Resolve(WebUtility.HtmlDecode(form.Groups["action"].Value)))
                {
                    Content = new FormUrlEncodedContent(fields),
                };
                current = await SendAsync(post);
                continue;
            }

            return current;
        }

        throw new InvalidOperationException("Too many redirects.");
    }

    public IEnumerable<string> CookieNames() => Cookies.GetAllCookies().Where(cookie => !cookie.Expired).Select(cookie => cookie.Name);

    public string? CookieValue(string name) => Cookies.GetAllCookies().FirstOrDefault(cookie => cookie.Name == name && !cookie.Expired)?.Value;

    public void Dispose() => gateway.Dispose();

    private Uri Resolve(string pathOrUrl) =>
        Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            ? absolute
            : new Uri(origin, pathOrUrl);

    [GeneratedRegex("<form method=\"post\" action=\"(?<action>[^\"]+)\">(?<inputs>.*?)</form>", RegexOptions.Singleline)]
    private static partial Regex FormRegex();

    [GeneratedRegex("<input type=\"hidden\" name=\"(?<name>[^\"]+)\" value=\"(?<value>[^\"]*)\" />")]
    private static partial Regex InputRegex();
}
