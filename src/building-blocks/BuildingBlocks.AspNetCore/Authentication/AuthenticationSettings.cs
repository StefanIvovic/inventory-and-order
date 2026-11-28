namespace BuildingBlocks.AspNetCore.Authentication;

public sealed class AuthenticationSettings
{
    public const string SectionName = "Authentication";

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;

    public string? InternalAddress { get; set; }

    public static bool IsOrigin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
