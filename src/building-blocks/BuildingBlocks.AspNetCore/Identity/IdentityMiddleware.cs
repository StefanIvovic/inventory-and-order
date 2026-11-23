using BuildingBlocks.AspNetCore.Errors;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Identity;

internal sealed partial class IdentityMiddleware(RequestDelegate next, ILogger<IdentityMiddleware> logger)
{
    public const string InvalidIdentityCode = "invalid-identity";

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, IProblemDetailsService problemDetails)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!CurrentUser.TryCreate(context.User, out var user, out var reason))
            {
                LogRejected(logger, reason, ProblemDetailsSetup.TraceIdOf(context));
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Detail = "The caller's identity is invalid.",
                        Extensions = { ["code"] = InvalidIdentityCode },
                    },
                });
                return;
            }

            context.Features.Set(user);
            if (user.TenantId is not null)
            {
                tenantContext.Set(user.TenantId);
            }
        }

        await next(context);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected request identity: {Reason} (trace {TraceId})")]
    private static partial void LogRejected(ILogger logger, string reason, string traceId);
}
