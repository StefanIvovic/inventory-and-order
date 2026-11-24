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
            var wanted = new OpenIddictScopeDescriptor { Name = name, Resources = { name } };
            var existing = await manager.FindByNameAsync(name, cancellationToken);
            if (existing is null)
            {
                await manager.CreateAsync(wanted, cancellationToken);
                continue;
            }

            var current = new OpenIddictScopeDescriptor();
            await manager.PopulateAsync(current, existing, cancellationToken);
            if (!current.Resources.SetEquals(wanted.Resources))
            {
                await manager.UpdateAsync(existing, wanted, cancellationToken);
            }
        }
    }
}
