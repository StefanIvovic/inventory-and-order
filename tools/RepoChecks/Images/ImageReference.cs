namespace RepoChecks.Images;

public sealed record ImageReference(string Repository, string? Tag)
{
    public string ShortName => Repository[(Repository.LastIndexOf('/') + 1)..];

    public static ImageReference Parse(string value)
    {
        var lastSlash = value.LastIndexOf('/');
        var colon = value.IndexOf(':', lastSlash + 1);
        return colon < 0
            ? new ImageReference(value, null)
            : new ImageReference(value[..colon], value[(colon + 1)..]);
    }
}
