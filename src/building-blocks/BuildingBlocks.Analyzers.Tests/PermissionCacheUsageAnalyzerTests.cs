namespace BuildingBlocks.Analyzers.Tests;

public sealed class PermissionCacheUsageAnalyzerTests
{
    private const string Usages = """
        using System;
        using BuildingBlocks.Core.Permissions;

        public sealed class RoleHandler(IPermissionCache cache)
        {
            private readonly IPermissionCache field = cache;

            public void Handle(IServiceProvider services, object boxed)
            {
                IPermissionCache local = field;
                var resolved = (IPermissionCache)services.GetService(typeof(IPermissionCache))!;
                var list = new System.Collections.Generic.List<IPermissionCache>();
                var cast = (IPermissionCache)boxed;
            }
        }
        """;

    [Fact]
    public async Task EveryReferenceInServiceCodeIsAnError()
    {
        var findings = await AnalyzerHarness.RunAsync(new PermissionCacheUsageAnalyzer(), Usages);

        Assert.All(findings, f => Assert.Equal(DiagnosticIds.PermissionCacheUsage, f.Id));
        Assert.Equal([4, 6, 10, 11, 11, 12, 13], findings.Select(f => f.Line));
    }

    [Fact]
    public async Task BuildingBlocksAssembliesAreExempt()
    {
        var findings = await AnalyzerHarness.RunAsync(new PermissionCacheUsageAnalyzer(), Usages, "BuildingBlocks.Messaging");

        Assert.Empty(findings);
    }

    [Fact]
    public async Task PublishingTheEventIsFine()
    {
        const string source = """
            using System;

            public sealed record PermissionAssignmentsChanged(string? TenantId, Guid UserId);

            public static class AssignRoleHandler
            {
                public static PermissionAssignmentsChanged Handle(Guid userId) => new("acme", userId);
            }
            """;

        Assert.Empty(await AnalyzerHarness.RunAsync(new PermissionCacheUsageAnalyzer(), source));
    }
}
