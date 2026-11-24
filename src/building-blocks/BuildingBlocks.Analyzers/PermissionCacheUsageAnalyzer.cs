using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BuildingBlocks.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PermissionCacheUsageAnalyzer : DiagnosticAnalyzer
{
    public const string ExemptAssemblyPrefix = "BuildingBlocks.";
    public const string PermissionCacheTypeName = "BuildingBlocks.Core.Permissions.IPermissionCache";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticIds.PermissionCacheUsageRule);

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var assemblyName = start.Compilation.AssemblyName ?? string.Empty;
            if (assemblyName.StartsWith(ExemptAssemblyPrefix, StringComparison.Ordinal))
            {
                return;
            }

            var cacheType = start.Compilation.GetTypeByMetadataName(PermissionCacheTypeName);
            if (cacheType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => AnalyzeName(node, cacheType),
                SyntaxKind.IdentifierName,
                SyntaxKind.GenericName);
        });
    }

    private static void AnalyzeName(SyntaxNodeAnalysisContext context, INamedTypeSymbol cacheType)
    {
        if (context.Node is IdentifierNameSyntax { IsVar: true })
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol;
        if (symbol is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, cacheType))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticIds.PermissionCacheUsageRule, context.Node.GetLocation()));
        }
    }
}
