using OpenIddict.Abstractions;

namespace Identity.Api.Protocol;

public static class Scopes
{
    public static IReadOnlyList<string> Api { get; } = ["identity", "product", "inventory", "order", "payment"];

    public static async Task RegisterAsync(IOpenIddictScopeManager manager, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manager);

        foreach (var name in Api)
        {
            if (await manager.FindByNameAsync(name, cancellationToken) is null)
            {
                await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = name, Resources = { name } }, cancellationToken);
            }
        }
    }
}
