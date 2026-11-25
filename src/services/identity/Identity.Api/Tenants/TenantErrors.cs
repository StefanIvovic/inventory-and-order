using BuildingBlocks.Core.Errors;

namespace Identity.Api.Tenants;

public static class TenantErrors
{
    public const string SlugTaken = "tenant-slug-taken";

    public static DomainException SlugTakenError() =>
        new(SlugTaken, DomainErrorKind.Conflict, "A tenant with this slug already exists.");
}
