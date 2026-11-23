# Contract: BuildingBlocks.Core public API

```csharp
namespace BuildingBlocks.Core.Identity;
public static class AppClaimTypes { public const string Subject = "sub"; public const string TenantId = "tenant_id"; public const string UserType = "user_type"; }
public enum UserType { Staff, Customer, Platform }
public interface ICurrentUser { bool IsAuthenticated { get; } Guid UserId { get; } string? TenantId { get; } UserType UserType { get; } }
public sealed record CurrentUser(Guid UserId, string? TenantId, UserType UserType)
{
    public static bool TryCreate(ClaimsPrincipal principal, out CurrentUser? user, out string? rejectionReason);
}

namespace BuildingBlocks.Core.Tenancy;
public interface ITenantContext { string? TenantId { get; } bool HasTenant { get; } }
public sealed class TenantContext : ITenantContext { public void Set(string tenantId); }

namespace BuildingBlocks.Core.Errors;
public enum DomainErrorKind { Conflict, Rule }
public class DomainException : Exception { public DomainException(string code, DomainErrorKind kind, string message); public string Code { get; } public DomainErrorKind Kind { get; } }
public sealed class NotFoundException : Exception { public const string ErrorCode = "not-found"; public NotFoundException(string resource); public string Resource { get; } public string Code { get; } }
```

Field rules are in [data-model.md](../data-model.md).
