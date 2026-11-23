using BuildingBlocks.Core.Identity;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.AspNetCore.Identity;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private CurrentUser? User => accessor.HttpContext?.Features.Get<CurrentUser>();

    public bool IsAuthenticated => User is not null;

    public Guid UserId => Require().UserId;

    public string? TenantId => User?.TenantId;

    public UserType UserType => Require().UserType;

    private CurrentUser Require() =>
        User ?? throw new InvalidOperationException("The current request is not authenticated.");
}
