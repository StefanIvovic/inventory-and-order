using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.AspNetCore.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Tests.Errors;

public sealed class TeapotException : Exception
{
    public TeapotException()
        : base("short and stout")
    {
    }
}

public sealed class TeapotMapper : IExceptionProblemMapper
{
    public bool TryMap(Exception exception, HttpContext context, [NotNullWhen(true)] out MappedProblem? problem)
    {
        problem = exception is TeapotException
            ? new MappedProblem(418, "teapot", "I am a teapot.", new Dictionary<string, object?> { ["brewed"] = true }, LogLevel.Warning)
            : null;
        return problem is not null;
    }
}

public sealed class LateTeapotMapper : IExceptionProblemMapper
{
    public bool TryMap(Exception exception, HttpContext context, [NotNullWhen(true)] out MappedProblem? problem)
    {
        problem = exception is TeapotException
            ? new MappedProblem(419, "late", null, new Dictionary<string, object?>(), LogLevel.Error)
            : null;
        return problem is not null;
    }
}
