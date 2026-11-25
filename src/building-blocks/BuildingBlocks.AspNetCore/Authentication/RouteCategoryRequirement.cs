using BuildingBlocks.Core.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.AspNetCore.Authentication;

internal sealed class RouteCategoryRequirement(RouteCategory category)
    : AuthorizationHandler<RouteCategoryRequirement>, IAuthorizationRequirement
{
    public RouteCategory Category { get; } = category;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RouteCategoryRequirement requirement)
    {
        if (CurrentUser.TryCreate(context.User, out var user, out _) && Allows(requirement.Category, user))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool Allows(RouteCategory category, CurrentUser user) => category switch
    {
        RouteCategory.Platform => user.UserType == UserType.Platform && user.TenantId is null,
        RouteCategory.Tenant => user.UserType != UserType.Platform && user.TenantId is not null,
        RouteCategory.SignedIn => true,
        _ => false,
    };
}
