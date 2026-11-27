using System.Text.RegularExpressions;

namespace Identity.Api.Tenants;

public sealed partial class Tenant
{
    public const int IdLength = 32;
    public const int SlugMaxLength = 63;
    public const int NameMaxLength = 200;
    public const string SlugPattern = "^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$";

    private Tenant()
    {
    }

    public string Id { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static bool IsValidSlug(string? slug) => slug is not null && SlugRegex().IsMatch(slug);

    public static Tenant Create(string slug, string name, DateTimeOffset createdAt)
    {
        if (!IsValidSlug(slug))
        {
            throw new ArgumentException("The slug is not valid.", nameof(slug));
        }

        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException("The name is not valid.", nameof(name));
        }

        return new Tenant { Id = Guid.NewGuid().ToString("N"), Slug = slug, Name = trimmed, NormalizedName = trimmed.ToUpperInvariant(), CreatedAt = createdAt };
    }

    [GeneratedRegex(SlugPattern, RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
