namespace BuildingBlocks.AspNetCore;

public sealed class PermissionOptions
{
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(60);
}
