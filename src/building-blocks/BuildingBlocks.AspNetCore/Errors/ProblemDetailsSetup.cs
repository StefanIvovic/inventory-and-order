using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.AspNetCore.Errors;

internal static class ProblemDetailsSetup
{
    public const string TraceIdKey = "traceId";

    public static string TraceIdOf(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;

    public static void Configure(ProblemDetailsOptions options) =>
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions[TraceIdKey] = TraceIdOf(context.HttpContext);
}
