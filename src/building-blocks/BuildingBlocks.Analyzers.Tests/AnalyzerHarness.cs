using System.Collections.Immutable;
using BuildingBlocks.Core.Permissions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace BuildingBlocks.Analyzers.Tests;

public sealed record Finding(string Id, int Line);

public static class AnalyzerHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var extra = new[]
        {
            typeof(DbContext).Assembly.Location,
            typeof(IPermissionCache).Assembly.Location,
            typeof(IWolverineHandler).Assembly.Location,
        };

        return platform.Concat(extra)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    });

    public static async Task<IReadOnlyList<Finding>> RunAsync(DiagnosticAnalyzer analyzer, string source, string assemblyName = "Inventory.Api")
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [tree],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(compileErrors.Count == 0, "Test source does not compile: " + string.Join(Environment.NewLine, compileErrors));

        var diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        return diagnostics
            .Select(d => new Finding(d.Id, d.Location.GetLineSpan().StartLinePosition.Line + 1))
            .OrderBy(f => f.Line)
            .ToList();
    }
}
