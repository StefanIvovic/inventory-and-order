using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.AspNetCore.Errors;

public interface IExceptionProblemMapper
{
    bool TryMap(Exception exception, HttpContext context, [NotNullWhen(true)] out MappedProblem? problem);
}
