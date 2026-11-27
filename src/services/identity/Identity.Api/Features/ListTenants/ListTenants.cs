using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Errors;
using BuildingBlocks.Validation;
using FluentValidation;
using Identity.Api.Tenants;

namespace Identity.Api.Features.ListTenants;

public sealed record ListTenants(string? Q = null, int Size = ListTenants.DefaultSize, string? After = null, bool IncludeTotal = false)
{
    public const int DefaultSize = 25;
    public const int MaxSize = 100;
}

public sealed record TenantItem(string Id, string Slug, string Name, DateTimeOffset CreatedAt);

public sealed record TenantListActions(bool CanCreate);

public sealed record TenantList(
    IReadOnlyList<TenantItem> Items,
    string? NextCursor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total,
    TenantListActions Actions);

public sealed class ListTenantsValidator : CommandValidator<ListTenants>
{
    public ListTenantsValidator()
    {
        RuleFor(x => x.Size).InclusiveBetween(1, ListTenants.MaxSize);
        RuleFor(x => x.Q).MaximumLength(Tenant.NameMaxLength);
    }
}

internal sealed record TenantCursor(string Slug)
{
    public const string InvalidCode = "invalid-cursor";

    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static TenantCursor? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            if (JsonSerializer.Deserialize<TenantCursor>(Base64Url.DecodeFromChars(value)) is { Slug.Length: > 0 } cursor)
            {
                return cursor;
            }
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
        }

        throw new DomainException(InvalidCode, DomainErrorKind.Rule, "The page token is invalid.");
    }
}
