using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.AspNetCore.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SignedInAttribute : Attribute, IAuthorizeData
{
    public string? Policy
    {
        get => AuthorizationPolicies.SignedIn;
        set => throw new NotSupportedException("The signed-in policy cannot be changed.");
    }

    public string? Roles { get; set; }

    public string? AuthenticationSchemes { get; set; }
}
