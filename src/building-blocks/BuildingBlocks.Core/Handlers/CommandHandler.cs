using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using BuildingBlocks.Core.Tenancy;

namespace BuildingBlocks.Core.Handlers;

public abstract class CommandHandler<TDbContext>
    where TDbContext : class
{
    protected CommandHandler(IRequestContext context, TDbContext db)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(db);
        Context = context;
        Db = db;
    }

    protected IRequestContext Context { get; }

    protected TDbContext Db { get; }

    protected ICurrentUser User => Context.User;

    protected IPermissionManager Permissions => Context.Permissions;

    protected ITenantContext Tenant => Context.Tenant;

    protected TimeProvider Clock => Context.Clock;
}
