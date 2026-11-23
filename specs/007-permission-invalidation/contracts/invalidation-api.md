# Contract: Permission Invalidation API

```csharp
namespace BuildingBlocks.Messaging;

[MessageIdentity("building-blocks.permission-assignments-changed.v1")]
public sealed record PermissionAssignmentsChanged(string? TenantId, Guid UserId);

public static class PermissionInvalidationExtensions
{
    public static WolverineOptions UseBuildingBlocksPermissionInvalidation(this WolverineOptions options);
}
```

## Service usage (the only supported pattern)

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseBuildingBlocksMessaging(settings);
    opts.UseBuildingBlocksPermissionInvalidation();
});

public sealed class AssignRoleHandler(IRequestContext context, InventoryDbContext db)
    : CommandHandler<InventoryDbContext>(context, db)
{
    public async Task<PermissionAssignmentsChanged> Handle(AssignRole command, CancellationToken ct)
    {
        await Permissions.EnsureAsync(InventoryPermissions.ManageAccess, ct);
        Db.RoleAssignments.Add(RoleAssignment.Create(command.UserId, command.Role));
        return new PermissionAssignmentsChanged(Tenant.TenantId, command.UserId);
    }
}
```
