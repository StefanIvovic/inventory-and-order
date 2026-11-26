using System.Data.Common;
using BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace BuildingBlocks.Lookups.Tests;

public sealed class LookupTestContext(DbContextOptions<LookupTestContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyLookups();
}

public sealed class CommandCounter : DbCommandInterceptor
{
    private int count;

    public int Count => count;

    public void Reset() => Interlocked.Exchange(ref count, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

public sealed class LookupTestDb
{
    private LookupTestDb(string connectionString) => ConnectionString = connectionString;

    public string ConnectionString { get; }

    public CommandCounter Commands { get; } = new();

    public static async Task<LookupTestDb> CreateAsync(PostgresFixture postgres)
    {
        var db = new LookupTestDb(await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken));
        await using var context = db.Context();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return db;
    }

    public LookupTestContext Context() =>
        new(new DbContextOptionsBuilder<LookupTestContext>().UseNpgsql(ConnectionString).AddInterceptors(Commands).Options);

    public async Task ExecuteAsync(string sql)
    {
        await using var context = Context();
        await context.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
    }
}
