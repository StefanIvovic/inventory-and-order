using System.Diagnostics;
using System.Text.Json;

namespace BuildingBlocks.Testing.Tests;

public static class DockerCli
{
    public static async Task<(int ExitCode, string Output)> RunAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output.Trim());
    }

    public static async Task<JsonElement> InspectAsync(string containerId)
    {
        var (exitCode, output) = await RunAsync("inspect", containerId);
        Assert.Equal(0, exitCode);
        return JsonDocument.Parse(output).RootElement[0];
    }

    public static async Task<bool> ExistsAsync(string containerId)
    {
        var (_, output) = await RunAsync("ps", "-a", "-q", "--no-trunc", "--filter", $"id={containerId}");
        return output.Length > 0;
    }

    public static string ImageOf(JsonElement inspect) => inspect.GetProperty("Config").GetProperty("Image").GetString()!;

    public static IReadOnlyList<string> TmpfsOf(JsonElement inspect) => MountsOfType(inspect, "tmpfs");

    public static IReadOnlyList<string> VolumeMountsOf(JsonElement inspect) => MountsOfType(inspect, "volume");

    private static List<string> MountsOfType(JsonElement inspect, string type) =>
        inspect.GetProperty("Mounts").EnumerateArray()
            .Where(m => m.GetProperty("Type").GetString() == type)
            .Select(m => m.GetProperty("Destination").GetString()!)
            .ToList();
}
