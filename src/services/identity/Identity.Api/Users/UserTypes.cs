using BuildingBlocks.Lookups;

namespace Identity.Api.Users;

public static class UserTypes
{
    public const string TypeCode = "user-type";
    public const string Staff = "staff";
    public const string Customer = "customer";
    public const string Platform = "platform";

    public static readonly LookupValueDefinition StaffValue = new(Guid.Parse("0d78e82f-0268-4c57-8926-4f31edfe1b61"), Staff, "Staff");
    public static readonly LookupValueDefinition CustomerValue = new(Guid.Parse("d5f29ef5-6ab3-4fe7-bcf9-eb778b259dd5"), Customer, "Customer");
    public static readonly LookupValueDefinition PlatformValue = new(Guid.Parse("d02f19f1-99bf-4f89-afdc-9eaa9e4317fb"), Platform, "Platform");

    public static readonly LookupTypeDefinition Type = new(
        Guid.Parse("c1efb8ef-8803-4c4f-82df-c22666971c5c"), TypeCode, "User type", StaffValue, CustomerValue, PlatformValue);

    public static Guid IdOf(string code) =>
        Type.Values.SingleOrDefault(v => v.Code == code)?.Id
        ?? throw new ArgumentException($"'{code}' is not a user type.", nameof(code));

    public static string CodeOf(Guid id) =>
        Type.Values.SingleOrDefault(v => v.Id == id)?.Code
        ?? throw new ArgumentException($"'{id}' is not a user type id.", nameof(id));
}
