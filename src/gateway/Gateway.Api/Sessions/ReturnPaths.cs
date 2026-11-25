using System.Text.RegularExpressions;

namespace Gateway.Api.Sessions;

public static partial class ReturnPaths
{
    public const string SlugPattern = "^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$";

    public static bool IsValidSlug(string? slug) => slug is not null && SlugRegex().IsMatch(slug);

    public static string Resolve(string? returnUrl, string area)
    {
        ArgumentException.ThrowIfNullOrEmpty(area);

        if (string.IsNullOrEmpty(returnUrl)
            || !returnUrl.StartsWith(area, StringComparison.Ordinal)
            || returnUrl.Contains("//", StringComparison.Ordinal)
            || returnUrl.Contains('\\', StringComparison.Ordinal)
            || returnUrl.Any(char.IsControl)
            || returnUrl.Split('?', '#')[0].Split('/').Any(segment => segment is ".." or "."))
        {
            return area;
        }

        return returnUrl;
    }

    [GeneratedRegex(SlugPattern, RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}

public sealed class SlugConstraint : IRouteConstraint
{
    public const string Name = "slug";

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.TryGetValue(routeKey, out var value) && ReturnPaths.IsValidSlug(value as string);
    }
}
