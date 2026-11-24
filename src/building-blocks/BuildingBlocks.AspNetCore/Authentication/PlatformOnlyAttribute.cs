using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.AspNetCore.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PlatformOnlyAttribute : Attribute, IAuthorizeData
{
    public string? Policy
    {
        get => AuthorizationPolicies.Platform;
        set => throw new NotSupportedException("The platform-only policy cannot be changed.");
    }

    public string? Roles { get; set; }

    public string? AuthenticationSchemes { get; set; }
}
