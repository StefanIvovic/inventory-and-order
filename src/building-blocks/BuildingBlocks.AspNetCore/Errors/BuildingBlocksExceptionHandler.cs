using BuildingBlocks.Core.Errors;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Errors;

internal sealed partial class BuildingBlocksExceptionHandler(
    IProblemDetailsService problemDetails,
    IEnumerable<IExceptionProblemMapper> mappers,
    ILogger<BuildingBlocksExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = ProblemDetailsSetup.TraceIdOf(httpContext);
        var problem = new ProblemDetails();

        if (TryMapWithExtensions(exception, httpContext, problem, traceId))
        {
            return await WriteAsync(httpContext, problem);
        }

        switch (exception)
        {
            case DomainException domain:
                problem.Status = domain.Kind == DomainErrorKind.Conflict
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status422UnprocessableEntity;
                problem.Detail = domain.Message;
                problem.Extensions["code"] = domain.Code;
                LogDomainError(logger, domain.Code, problem.Status.Value, traceId);
                break;

            case NotFoundException notFound:
                problem.Status = StatusCodes.Status404NotFound;
                problem.Detail = notFound.Message;
                problem.Extensions["code"] = notFound.Code;
                LogDomainError(logger, notFound.Code, problem.Status.Value, traceId);
                break;

            case PermissionDeniedException denied:
                problem.Status = StatusCodes.Status403Forbidden;
                problem.Detail = "You do not have the permissions required for this operation.";
                problem.Extensions["code"] = denied.Code;
                problem.Extensions["missingPermissions"] = denied.Missing;
                LogPermissionDenied(
                    logger,
                    httpContext.Features.Get<CurrentUser>()?.UserId.ToString() ?? "anonymous",
                    string.Join(", ", denied.Missing),
                    traceId);
                break;

            default:
                problem.Status = StatusCodes.Status500InternalServerError;
                LogUnhandled(logger, exception, traceId);
                break;
        }

        return await WriteAsync(httpContext, problem);
    }

    private bool TryMapWithExtensions(Exception exception, HttpContext httpContext, ProblemDetails problem, string traceId)
    {
        foreach (var mapper in mappers)
        {
            if (!mapper.TryMap(exception, httpContext, out var mapped))
            {
                continue;
            }

            problem.Status = mapped.Status;
            problem.Detail = mapped.Detail;
            problem.Title = mapped.Title;
            if (mapped.Code is not null)
            {
                problem.Extensions["code"] = mapped.Code;
            }

            foreach (var (key, value) in mapped.Extensions)
            {
                problem.Extensions[key] = value;
            }

            LogMapped(logger, mapped.LogLevel, mapped.Code ?? "none", mapped.Status, traceId);
            return true;
        }

        return false;
    }

    private async ValueTask<bool> WriteAsync(HttpContext httpContext, ProblemDetails problem)
    {
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Message = "Request rejected with {Code} ({Status}) (trace {TraceId})")]
    private static partial void LogMapped(ILogger logger, LogLevel level, string code, int status, string traceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected with {Code} ({Status}) (trace {TraceId})")]
    private static partial void LogDomainError(ILogger logger, string code, int status, string traceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permission denied for user {UserId}: missing {MissingPermissions} (trace {TraceId})")]
    private static partial void LogPermissionDenied(ILogger logger, string userId, string missingPermissions, string traceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception (trace {TraceId})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string traceId);
}
