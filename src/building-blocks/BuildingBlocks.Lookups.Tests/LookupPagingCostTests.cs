using System.Text.Json;
using BuildingBlocks.Testing;
using Npgsql;

namespace BuildingBlocks.Lookups.Tests;

public sealed class LookupPagingCostTests(PostgresFixture postgres)
{
    private const int Values = 20_000;
    private const int Size = 50;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeepPageIsOneIndexedCommandAndCostsAboutTheSameAsTheFirst()
    {
        var db = await LookupTestDb.CreateAsync(postgres);
        await using (var context = db.Context())
        {
            await LookupSeeder.SeedAsync(context, Catalogs.Many(Values), Cancellation);
        }

        await db.ExecuteAsync("VACUUM ANALYZE lookup_values");
        var first = await PageCostAsync(db, null);
        string? after = null;
        await using (var context = db.Context())
        {
            for (var page = 0; page < 300; page++)
            {
                after = (await context.ReadValuesAsync(new LookupValueQuery("color", Size: Size, After: after), Cancellation)).NextCursor;
            }
        }

        var deep = await PageCostAsync(db, after);

        Assert.False(deep.Plan.Contains("Seq Scan", StringComparison.Ordinal), deep.Plan);
        Assert.True(deep.Blocks <= (first.Blocks * 2) + 20, $"first page {first.Blocks} blocks, deep page {deep.Blocks} blocks\n{deep.Plan}");
    }

    private static async Task<(long Blocks, string Plan)> PageCostAsync(LookupTestDb db, string? after)
    {
        await using var context = db.Context();
        db.Commands.Reset();
        await context.ReadValuesAsync(new LookupValueQuery("color", Size: Size, After: after), Cancellation);
        Assert.Equal(1, db.Commands.Count);
        var command = db.Commands.Last!.Value;

        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync(Cancellation);
        var json = await ExplainAsync(connection, "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) ", command);
        var top = JsonDocument.Parse(json).RootElement[0].GetProperty("Plan");
        var blocks = top.GetProperty("Shared Hit Blocks").GetInt64() + top.GetProperty("Shared Read Blocks").GetInt64();
        return (blocks, await ExplainAsync(connection, "EXPLAIN ", command));
    }

    private static async Task<string> ExplainAsync(NpgsqlConnection connection, string prefix, (string Text, IReadOnlyList<(string Name, object Value)> Parameters) command)
    {
        await using var explain = new NpgsqlCommand(prefix + command.Text, connection);
        foreach (var (name, value) in command.Parameters)
        {
            explain.Parameters.Add(new NpgsqlParameter(name, value));
        }

        var lines = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync(Cancellation);
        while (await reader.ReadAsync(Cancellation))
        {
            lines.Add(reader.GetString(0));
        }

        return string.Join('\n', lines);
    }
}
