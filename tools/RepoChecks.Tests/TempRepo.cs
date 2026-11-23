using RepoChecks;
using RepoChecks.Images;

namespace RepoChecks.Tests;

public sealed class TempRepo : IDisposable
{
    public TempRepo()
    {
        Root = Path.Combine(Path.GetTempPath(), "iao-repochecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public static string Image(string repository, string tag) => repository + ":" + tag;

    public static TempRepo WithValidBaseline()
    {
        var repo = new TempRepo();
        repo.Write("global.json", """{ "sdk": { "version": "10.0.401", "rollForward": "disable" } }""");
        repo.Write("deploy/versions.env", string.Join('\n',
            $"DOTNET_SDK_IMAGE={Image("mcr.microsoft.com/dotnet/sdk", "10.0.401-alpine3.24")}",
            $"DOTNET_ASPNET_IMAGE={Image("mcr.microsoft.com/dotnet/aspnet", "10.0.15-alpine3.24")}",
            $"POSTGRES_IMAGE={Image("postgres", "18.7-alpine3.24")}",
            $"RABBITMQ_IMAGE={Image("rabbitmq", "4.4.1-management-alpine")}",
            ""));
        return repo;
    }

    public TempRepo Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return this;
    }

    public IReadOnlyList<Finding> Run() => ImageCheck.Run(Root);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
