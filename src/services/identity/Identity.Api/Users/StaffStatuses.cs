using BuildingBlocks.Lookups;

namespace Identity.Api.Users;

public static class StaffStatuses
{
    public const string TypeCode = "staff-status";
    public const string Active = "active";
    public const string Disabled = "disabled";

    public static readonly LookupTypeDefinition Type = new(
        Guid.Parse("f0e65c08-7e0e-44e5-9ec7-4ffab1e4e776"),
        TypeCode,
        "Staff status",
        new LookupValueDefinition(Guid.Parse("099ae122-94b8-4e41-a626-95a67773506b"), Active, "Active"),
        new LookupValueDefinition(Guid.Parse("e5c6932f-281a-4d67-a212-f73f1ccbc66e"), Disabled, "Disabled"));
}
