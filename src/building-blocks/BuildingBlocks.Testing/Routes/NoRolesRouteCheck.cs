using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BuildingBlocks.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Testing;

public static class NoRolesRouteCheck
{
    private static readonly HashSet<string> MethodsWithBody = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH" };

    public static async Task<RouteCheckReport> RunAsync(
        IServiceProvider services,
        HttpClient client,
        string? audience = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(client);

        var checkedRoutes = new List<string>();
        var skipped = new List<string>();
        var failures = new List<string>();
        var endpoints = services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        foreach (var endpoint in endpoints.OrderBy(e => e.RoutePattern.RawText, StringComparer.Ordinal))
        {
            var category = RouteCategories.Of(endpoint);
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            foreach (var method in methods.Order(StringComparer.Ordinal))
            {
                var label = $"{method} {endpoint.RoutePattern.RawText}";
                if (category == RouteCategory.AllowListed)
                {
                    skipped.Add(label);
                    continue;
                }

                using var request = new HttpRequestMessage(new HttpMethod(method), PathOf(endpoint.RoutePattern));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(category, audience));
                if (MethodsWithBody.Contains(method))
                {
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                }

                using var response = await client.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    checkedRoutes.Add($"{label} ({category})");
                }
                else
                {
                    failures.Add($"{label} ({category}) returned {(int)response.StatusCode} instead of 403");
                }
            }
        }

        return new RouteCheckReport(checkedRoutes, skipped, failures);
    }

    public static string PathOf(RoutePattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var path = new StringBuilder();
        foreach (var segment in pattern.PathSegments)
        {
            path.Append('/');
            foreach (var part in segment.Parts)
            {
                path.Append(part switch
                {
                    RoutePatternLiteralPart literal => literal.Content,
                    RoutePatternSeparatorPart separator => separator.Content,
                    RoutePatternParameterPart parameter => ValueFor(parameter),
                    _ => string.Empty,
                });
            }
        }

        return path.Length == 0 ? "/" : path.ToString();
    }

    private static string ValueFor(RoutePatternParameterPart parameter)
    {
        var constraints = parameter.ParameterPolicies.Select(p => p.Content ?? string.Empty).ToList();
        if (constraints.Exists(c => c.StartsWith("guid", StringComparison.OrdinalIgnoreCase)))
        {
            return Guid.NewGuid().ToString();
        }

        if (constraints.Exists(c => c.StartsWith("int", StringComparison.OrdinalIgnoreCase) || c.StartsWith("long", StringComparison.OrdinalIgnoreCase)))
        {
            return "1";
        }

        return "x";
    }

    private static string TokenFor(RouteCategory category, string? audience) => category == RouteCategory.Platform
        ? TestTokens.Platform(Guid.NewGuid(), audience)
        : TestTokens.For(Guid.NewGuid(), $"tenant-{Guid.NewGuid():N}", audience: audience);
}
