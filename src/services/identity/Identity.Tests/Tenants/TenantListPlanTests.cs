using System.Net;
using System.Text.Json;
using Identity.Api.Persistence;
using Identity.Tests.Users;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Identity.Tests.Tenants;

public sealed class TenantListPlanTests(ContainersFixture containers)
{
    private const int Tenants = 20_000;
    private const int MaxBlocksPerPage = 300;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData("?q=t-1")]
    [InlineData("?q=name%201")]
    public async Task DeepPagesAreOneCommandAndCostAboutTheSameAsTheFirst(string filters)
    {
        await using var big = await BigListAsync();
        var deep = await big.CursorAtAsync(filters);

        var costs = new List<long>();
        foreach (var query in new[] { filters, Append(filters, $"after={deep}") })
        {
            big.Recorder.Clear();
            var response = await big.List.GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var command = Assert.Single(big.Recorder.Commands);
            var (plan, blocks) = await ExplainAsync(big.List.Database, command);
            var detail = $"{blocks} blocks{Environment.NewLine}{command.Text}{Environment.NewLine}{plan}";
            Assert.False(plan.Contains("Seq Scan", StringComparison.Ordinal), detail);
            Assert.True(blocks <= MaxBlocksPerPage, detail);
            costs.Add(blocks);
        }

        Assert.True(costs[1] <= (costs[0] * 2) + 20, $"first page {costs[0]} blocks, deep page {costs[1]} blocks");
    }

    [Theory]
    [InlineData("?q=name%2012345&includeTotal=true")]
    [InlineData("?q=t-12345&includeTotal=true")]
    public async Task ARareMatchIsFoundWithoutReadingTheList(string query)
    {
        await using var big = await BigListAsync();

        big.Recorder.Clear();
        var page = await big.List.PageAsync(query);
        Assert.Equal(["t-12345"], ListTenantsTests.Slugs(page));

        Assert.Equal(2, big.Recorder.Commands.Count);
        foreach (var command in big.Recorder.Commands)
        {
            var (plan, blocks) = await ExplainAsync(big.List.Database, command);
            var detail = $"{blocks} blocks{Environment.NewLine}{command.Text}{Environment.NewLine}{plan}";
            Assert.False(plan.Contains("Seq Scan", StringComparison.Ordinal), detail);
            Assert.True(blocks <= 50, detail);
        }
    }

    private static string Append(string query, string part) => query.Length == 0 ? "?" + part : query + "&" + part;

    private async Task<BigList> BigListAsync()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        var recorder = new CommandRecorder();
        var app = await IdentityApp.StartAsync(
            containers,
            database,
            testServices: services => services.ConfigureDbContext<IdentityDbContext>(options => options.AddInterceptors(recorder)));
        await app.WaitUntilMigratedAsync();
        await TenantSeed.InsertNumberedAsync(database, Tenants);
        await ExecuteAsync(database, "VACUUM ANALYZE tenants");
        var big = new BigList(new PlatformList(app, database, await app.PlatformTokenAsync()), recorder);
        Assert.Equal(HttpStatusCode.OK, (await big.List.GetAsync(string.Empty)).StatusCode);
        return big;
    }

    private static async Task<(string Plan, long Blocks)> ExplainAsync(string database, RecordedCommand command)
    {
        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(Cancellation);
        var plan = await ExplainAsync(connection, "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) ", command);
        var top = JsonDocument.Parse(plan).RootElement[0].GetProperty("Plan");
        var blocks = top.GetProperty("Shared Hit Blocks").GetInt64() + top.GetProperty("Shared Read Blocks").GetInt64();
        return (await ExplainAsync(connection, "EXPLAIN (ANALYZE, BUFFERS) ", command), blocks);
    }

    private static async Task<string> ExplainAsync(NpgsqlConnection connection, string prefix, RecordedCommand command)
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

    private static async Task ExecuteAsync(string database, string sql)
    {
        await using var connection = new NpgsqlConnection(database);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    private sealed record BigList(PlatformList List, CommandRecorder Recorder) : IAsyncDisposable
    {
        public async Task<string> CursorAtAsync(string query)
        {
            var page = await List.PageAsync(Append(query, "size=100"));
            for (var i = 0; i < 40; i++)
            {
                page = await List.PageAsync(Append(query, $"size=100&after={page.GetProperty("nextCursor").GetString()}"));
            }

            return page.GetProperty("nextCursor").GetString()!;
        }

        public ValueTask DisposeAsync() => List.DisposeAsync();
    }
}
