using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace BuildingBlocks.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SaveChangesInHandlerAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> SaveMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal, "SaveChanges", "SaveChangesAsync");

    private static readonly ImmutableHashSet<string> HandlerMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Handle", "Handles", "Consume", "Consumes");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticIds.SaveChangesInHandlerRule);

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
            var dbContext = start.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
            if (dbContext is null)
            {
                return;
            }

            var known = new KnownTypes(
                dbContext,
                start.Compilation.GetTypeByMetadataName("BuildingBlocks.Core.Handlers.CommandHandler`1"),
                start.Compilation.GetTypeByMetadataName("BuildingBlocks.Core.Handlers.MessageHandler`1"),
                start.Compilation.GetTypeByMetadataName("Wolverine.Attributes.WolverineHandlerAttribute"),
                start.Compilation.GetTypeByMetadataName("Wolverine.IWolverineHandler"));

            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, known), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, KnownTypes known)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!SaveMethods.Contains(method.Name) || !InheritsFrom(method.ContainingType, known.DbContext))
        {
            return;
        }

        var enclosing = EnclosingOrdinaryMethod(context.ContainingSymbol);
        if (enclosing is null || !IsHandlerMethod(enclosing, known))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticIds.SaveChangesInHandlerRule, invocation.Syntax.GetLocation(), method.Name));
    }

    private static IMethodSymbol? EnclosingOrdinaryMethod(ISymbol? symbol) =>
        symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary } ordinary ? ordinary : null;

    private static bool IsHandlerMethod(IMethodSymbol method, KnownTypes known)
    {
        if (method.DeclaredAccessibility != Accessibility.Public || !HandlerMethods.Contains(method.Name))
        {
            return false;
        }

        var type = method.ContainingType;
        return type.Name.EndsWith("Handler", StringComparison.Ordinal)
            || type.Name.EndsWith("Consumer", StringComparison.Ordinal)
            || InheritsFrom(type, known.CommandHandler)
            || InheritsFrom(type, known.MessageHandler)
            || HasAttribute(type, known.HandlerAttribute)
            || Implements(type, known.HandlerInterface);
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol? attribute) =>
        attribute is not null && type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol? contract) =>
        contract is not null && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract));

    private sealed class KnownTypes(
        INamedTypeSymbol dbContext,
        INamedTypeSymbol? commandHandler,
        INamedTypeSymbol? messageHandler,
        INamedTypeSymbol? handlerAttribute,
        INamedTypeSymbol? handlerInterface)
    {
        public INamedTypeSymbol DbContext { get; } = dbContext;

        public INamedTypeSymbol? CommandHandler { get; } = commandHandler;

        public INamedTypeSymbol? MessageHandler { get; } = messageHandler;

        public INamedTypeSymbol? HandlerAttribute { get; } = handlerAttribute;

        public INamedTypeSymbol? HandlerInterface { get; } = handlerInterface;
    }
}
