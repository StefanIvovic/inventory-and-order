using System.Diagnostics;

namespace Gateway.Api;

public static class Problems
{
    public const string TraceIdKey = "traceId";
    public const string SessionRequired = "session-required";
    public const string SessionEnded = "session-ended";
    public const string AppHeaderRequired = "app-header-required";
    public const string CrossSiteRequest = "cross-site-request";
    public const string IdentityUnavailable = "identity-unavailable";
    public const string UpstreamUnavailable = "upstream-unavailable";
    public const string LogoutProofRequired = "logout-proof-required";

    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        [SessionRequired] = "A signed-in session is required.",
        [SessionEnded] = "The session has ended. Sign in again.",
        [AppHeaderRequired] = "The X-Bff-App header is missing or invalid.",
        [CrossSiteRequest] = "Cross-site requests are not allowed.",
        [IdentityUnavailable] = "The identity service is unavailable.",
        [UpstreamUnavailable] = "The upstream service is unavailable.",
        [LogoutProofRequired] = "The sign-out proof is missing or invalid.",
    };

    public static IServiceCollection AddGatewayProblems(this IServiceCollection services) =>
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions[TraceIdKey] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

    public static IResult Result(int status, string type) =>
        Results.Problem(statusCode: status, type: type, title: Titles[type]);

    public static Task WriteAsync(HttpContext context, int status, string type)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Result(status, type).ExecuteAsync(context);
    }
}
