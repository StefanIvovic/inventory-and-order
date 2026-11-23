using BuildingBlocks.Core.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Errors;

internal sealed partial class BuildingBlocksExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<BuildingBlocksExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = ProblemDetailsSetup.TraceIdOf(httpContext);
        var problem = new ProblemDetails();

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

            default:
                problem.Status = StatusCodes.Status500InternalServerError;
                LogUnhandled(logger, exception, traceId);
                break;
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected with {Code} ({Status}) (trace {TraceId})")]
    private static partial void LogDomainError(ILogger logger, string code, int status, string traceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception (trace {TraceId})")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string traceId);
}
