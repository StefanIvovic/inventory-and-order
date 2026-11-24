using Microsoft.CodeAnalysis;

namespace BuildingBlocks.Analyzers;

public static class DiagnosticIds
{
    public const string PermissionCacheUsage = "IAO1001";
    public const string SaveChangesInHandler = "IAO1002";

    private const string Category = "BuildingBlocks.Correctness";
    private const string HelpLink = "https://github.com/StefanIvovic/inventory-and-order/blob/master/specs/009-building-blocks-analyzers/quickstart.md";

    public static readonly DiagnosticDescriptor PermissionCacheUsageRule = new(
        PermissionCacheUsage,
        "Do not use IPermissionCache outside BuildingBlocks",
        "IPermissionCache must not be used here: return PermissionAssignmentsChanged from the handler that changed the assignments, so every instance invalidates after commit",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Direct invalidation runs before commit and only on the local instance. Permission changes must publish PermissionAssignmentsChanged.",
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor SaveChangesInHandlerRule = new(
        SaveChangesInHandler,
        "Do not save inside a Wolverine handler",
        "{0} must not be called in a Wolverine handler: the transaction middleware saves and commits after the handler returns",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Wolverine's transaction middleware owns SaveChanges and the commit. Saving inside a handler breaks the transaction boundary.",
        helpLinkUri: HelpLink);
}
