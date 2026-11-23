using System.Text.Json;

namespace RepoChecks.Images;

public static class SdkRule
{
    public const string SdkKey = "DOTNET_SDK_IMAGE";

    public static IEnumerable<Finding> Check(VersionsFile versions, string? globalJson)
    {
        if (!versions.Entries.TryGetValue(SdkKey, out var entry))
        {
            yield break;
        }

        var sdkVersion = ReadSdkVersion(globalJson);
        if (sdkVersion is null)
        {
            yield return new Finding(ImageCheck.GlobalJsonPath, 1, "IP007", "global.json does not pin sdk.version");
            yield break;
        }

        var tag = entry.Image.Tag ?? string.Empty;
        if (tag != sdkVersion && !tag.StartsWith(sdkVersion + "-", StringComparison.Ordinal))
        {
            yield return new Finding(versions.Path, entry.Line, "IP007", $"{SdkKey} tag '{tag}' does not match global.json SDK {sdkVersion}");
        }
    }

    private static string? ReadSdkVersion(string? globalJson)
    {
        if (globalJson is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(globalJson);
        return document.RootElement.TryGetProperty("sdk", out var sdk) &&
               sdk.TryGetProperty("version", out var version) &&
               version.ValueKind == JsonValueKind.String
            ? version.GetString()
            : null;
    }
}
