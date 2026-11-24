using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.AspNetCore.Errors;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Validation;

public sealed class ValidationProblemMapper : IExceptionProblemMapper
{
    public const string Code = "validation-failed";
    public const string Title = "One or more validation errors occurred.";

    public bool TryMap(Exception exception, HttpContext context, [NotNullWhen(true)] out MappedProblem? problem)
    {
        if (exception is not ValidationException validation)
        {
            problem = null;
            return false;
        }

        var errors = validation.Errors
            .Where(failure => failure is not null)
            .GroupBy(failure => FieldNames.ToClientName(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        problem = new MappedProblem(
            StatusCodes.Status400BadRequest,
            Code,
            null,
            new Dictionary<string, object?> { ["errors"] = errors },
            LogLevel.Information,
            Title);
        return true;
    }
}
