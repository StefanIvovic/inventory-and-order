using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.AspNetCore.Errors;
using Identity.Api.Tenants;
using Identity.Api.Users;
using Npgsql;

namespace Identity.Api.Persistence;

public sealed class UniqueViolationMapper : IExceptionProblemMapper
{
    private static readonly Dictionary<string, (string Code, string Detail)> Constraints = new(StringComparer.Ordinal)
    {
        [IdentityDbContext.TenantSlugIndex] = (TenantErrors.SlugTaken, TenantErrors.SlugTakenError().Message),
        [IdentityDbContext.StaffEmailIndex] = (IdentityResultErrors.EmailTaken, IdentityResultErrors.EmailTakenError().Message),
    };

    public bool TryMap(Exception exception, HttpContext context, [NotNullWhen(true)] out MappedProblem? problem)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { } name }
                && Constraints.TryGetValue(name, out var mapped))
            {
                problem = new MappedProblem(
                    StatusCodes.Status409Conflict,
                    mapped.Code,
                    mapped.Detail,
                    new Dictionary<string, object?>(StringComparer.Ordinal),
                    LogLevel.Information);
                return true;
            }
        }

        problem = null;
        return false;
    }
}
