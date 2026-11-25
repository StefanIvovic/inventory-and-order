using Gateway.Api.Sessions;

namespace Gateway.Api.Configuration;

public sealed class GatewaySettings
{
    public const string Section = "Gateway";
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public Uri? PublicOrigin { get; set; }

    public IdentityEndpointSettings Identity { get; set; } = new();

    public Dictionary<string, ClientSettings> Clients { get; } = new(StringComparer.Ordinal);

    public RefreshSettings Refresh { get; set; } = new();

    public KeySettings Keys { get; set; } = new();

    public bool IsHttps => PublicOrigin?.Scheme == Uri.UriSchemeHttps;

    public Uri Origin => PublicOrigin ?? throw new InvalidOperationException("Gateway:PublicOrigin is required.");

    public Uri IdentityAddress => Identity.InternalAddress ?? throw new InvalidOperationException("Gateway:Identity:InternalAddress is required.");

    public string SecretOf(BffApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return Clients.TryGetValue(app.ClientId, out var client) && !string.IsNullOrWhiteSpace(client.Secret)
            ? client.Secret
            : throw new InvalidOperationException($"Gateway:Clients:{app.ClientId}:Secret is required.");
    }

    public static GatewaySettings Read(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var settings = new GatewaySettings();
        configuration.GetSection(Section).Bind(settings);
        var errors = settings.Validate(environment).ToList();
        return errors.Count == 0 ? settings : throw new InvalidOperationException(string.Join(" ", errors));
    }

    public IEnumerable<string> Validate(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (PublicOrigin is null || !PublicOrigin.IsAbsoluteUri || PublicOrigin.AbsolutePath != "/" || PublicOrigin.Query.Length > 0
            || (PublicOrigin.Scheme != Uri.UriSchemeHttp && PublicOrigin.Scheme != Uri.UriSchemeHttps))
        {
            yield return "Gateway:PublicOrigin must be an absolute http(s) origin without a path.";
        }

        if (Identity.InternalAddress is null || !Identity.InternalAddress.IsAbsoluteUri)
        {
            yield return "Gateway:Identity:InternalAddress must be an absolute address.";
        }

        foreach (var app in BffApp.All)
        {
            if (!Clients.TryGetValue(app.ClientId, out var client) || string.IsNullOrWhiteSpace(client.Secret))
            {
                yield return $"Gateway:Clients:{app.ClientId}:Secret is required.";
            }
        }

        if (Refresh.Threshold <= TimeSpan.Zero || Refresh.Threshold >= AccessTokenLifetime)
        {
            yield return "Gateway:Refresh:Threshold must be positive and shorter than the access-token lifetime.";
        }

        if (string.IsNullOrWhiteSpace(Keys.Path) && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            yield return "Gateway:Keys:Path is required outside Development and Testing.";
        }
    }
}

public sealed class IdentityEndpointSettings
{
    public Uri? InternalAddress { get; set; }
}

public sealed class ClientSettings
{
    public string? Secret { get; set; }
}

public sealed class RefreshSettings
{
    public TimeSpan Threshold { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class KeySettings
{
    public string? Path { get; set; }
}
