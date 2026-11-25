using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Lookups;

public static class LookupSeeder
{
    private const string UniqueViolation = "23505";

    public static async Task SeedAsync(DbContext db, LookupCatalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(catalog);

        try
        {
            await SeedOnceAsync(db, catalog, cancellationToken);
        }
        catch (DbUpdateException first) when (IsUniqueViolation(first))
        {
            db.ChangeTracker.Clear();
            try
            {
                await SeedOnceAsync(db, catalog, cancellationToken);
            }
            catch (DbUpdateException second) when (IsUniqueViolation(second))
            {
                throw new LookupSeedException(
                    "A lookup row in the database uses a defined code under a different id. Fix or remove it.", second);
            }
        }
        finally
        {
            db.ChangeTracker.Clear();
        }

        await EnsureCompleteAsync(db, catalog, cancellationToken);
    }

    private static async Task SeedOnceAsync(DbContext db, LookupCatalog catalog, CancellationToken cancellationToken)
    {
        var typeIds = catalog.Types.Select(t => t.Id).ToList();
        var valueIds = catalog.Types.SelectMany(t => t.Values).Select(v => v.Id).ToList();
        var types = await db.Set<LookupType>().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        var values = await db.Set<LookupValue>().Where(v => valueIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);

        foreach (var type in catalog.Types)
        {
            if (types.TryGetValue(type.Id, out var existingType))
            {
                existingType.Apply(type);
            }
            else
            {
                db.Add(LookupType.From(type));
            }

            foreach (var value in type.Values)
            {
                if (values.TryGetValue(value.Id, out var existingValue))
                {
                    existingValue.Apply(type.Id, value);
                }
                else
                {
                    db.Add(LookupValue.From(type.Id, value));
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureCompleteAsync(DbContext db, LookupCatalog catalog, CancellationToken cancellationToken)
    {
        var ids = catalog.Ids;
        var found = await db.Set<LookupType>().Where(t => ids.Contains(t.Id)).Select(t => t.Id)
            .Concat(db.Set<LookupValue>().Where(v => ids.Contains(v.Id)).Select(v => v.Id))
            .ToListAsync(cancellationToken);
        var missing = ids.Except(found).ToList();
        if (missing.Count > 0)
        {
            throw new LookupSeedException("Lookup rows are missing after seeding: " + string.Join(", ", missing));
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is DbException { SqlState: UniqueViolation };
}
