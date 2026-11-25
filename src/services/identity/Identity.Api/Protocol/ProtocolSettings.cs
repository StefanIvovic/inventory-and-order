namespace Identity.Api.Protocol;

public sealed class ProtocolSettings
{
    public const string SectionName = "Identity";

    public string Issuer { get; set; } = string.Empty;

    public bool RequireHttps { get; set; } = true;

    public KeySettings Keys { get; set; } = new();

    public Dictionary<string, ClientSettings> Clients { get; set; } = new(StringComparer.Ordinal);

    public PlatformAdminSettings PlatformAdmin { get; set; } = new();

    public DemoTenantSettings DemoTenant { get; set; } = new();

    public ForwardedHeadersSettings ForwardedHeaders { get; set; } = new();

    public static ProtocolSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var settings = configuration.GetSection(SectionName).Get<ProtocolSettings>() ?? new ProtocolSettings();
        if (!Uri.TryCreate(settings.Issuer, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Identity:Issuer must be an absolute URI.");
        }

        settings.ForwardedHeaders.Validate();

        return settings;
    }
}

public enum KeyMode
{
    Ephemeral,
    Directory,
    Certificates,
}

public sealed class KeySettings
{
    public KeyMode? Mode { get; set; }

    public string? Path { get; set; }

    public string? Password { get; set; }

    public string? SigningCertificatePath { get; set; }

    public string? EncryptionCertificatePath { get; set; }
}

public sealed class ClientSettings
{
    public string? Secret { get; set; }

    public string? RedirectUri { get; set; }

    public string? PostLogoutRedirectUri { get; set; }
}

public sealed class PlatformAdminSettings
{
    public string? Email { get; set; }

    public string? Password { get; set; }
}

public sealed class DemoTenantSettings
{
    public string? Slug { get; set; }

    public string? Name { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Slug) && !string.IsNullOrWhiteSpace(Name)
        && !string.IsNullOrWhiteSpace(AdminEmail) && !string.IsNullOrWhiteSpace(AdminPassword);
}

public sealed class ForwardedHeadersSettings
{
    public List<string> KnownNetworks { get; set; } = [];

    public List<string> AllowedHosts { get; set; } = [];

    public bool IsEnabled => KnownNetworks.Count > 0;

    public IEnumerable<System.Net.IPNetwork> Networks() => KnownNetworks.Select(System.Net.IPNetwork.Parse);

    public IEnumerable<string> HostPatterns() =>
        AllowedHosts.Where(host => !string.IsNullOrWhiteSpace(host)).Select(host => new Microsoft.AspNetCore.Http.HostString(host.Trim()).Host);

    public void Validate()
    {
        foreach (var network in KnownNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out _))
            {
                throw new InvalidOperationException($"Identity:ForwardedHeaders:KnownNetworks contains '{network}', which is not a CIDR range.");
            }
        }

        if (IsEnabled && AllowedHosts.TrueForAll(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Identity:ForwardedHeaders:AllowedHosts is required when known networks are set.");
        }
    }
}
