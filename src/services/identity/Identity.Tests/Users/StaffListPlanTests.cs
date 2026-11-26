using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using Identity.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Identity.Tests.Users;

public sealed class StaffListPlanTests(ContainersFixture containers)
{
    private const int Staff = 50_000;
    private const int MaxBlocksPerPage = 1200;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData("?q=u-4")]
    [InlineData("?status=disabled")]
    [InlineData("?role=identity.viewer")]
    [InlineData("?q=u-3&status=active&role=identity.viewer")]
    public async Task DeepPagesAreOneCommandAndCostAboutTheSameAsTheFirst(string filters)
    {
        await using var big = await BigTenantAsync();
        var deep = await big.CursorAtAsync(filters);

        var costs = new List<long>();
        foreach (var query in new[] { filters, Append(filters, $"after={deep}") })
        {
            big.Recorder.Clear();
            var response = await big.Tenant.GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var command = Assert.Single(big.Recorder.Commands);
            var (plan, blocks) = await ExplainAsync(big.Tenant.Database, command);
            var detail = $"{blocks} blocks{Environment.NewLine}{command.Text}{Environment.NewLine}{plan}";
            Assert.False(plan.Contains("Seq Scan", StringComparison.Ordinal), detail);
            Assert.True(blocks <= MaxBlocksPerPage, detail);
            costs.Add(blocks);
        }

        Assert.True(costs[1] <= (costs[0] * 2) + 50, $"first page {costs[0]} blocks, deep page {costs[1]} blocks");
    }

    private static string Append(string query, string part) => query.Length == 0 ? "?" + part : query + "&" + part;

    private async Task<BigTenant> BigTenantAsync()
    {
        var database = await containers.Postgres.CreateDatabaseAsync(Cancellation);
        var recorder = new CommandRecorder();
        var app = await IdentityApp.StartAsync(
            containers,
            database,
            testServices: services => services.ConfigureDbContext<IdentityDbContext>(options => options.AddInterceptors(recorder)));
        var tenant = await app.CreateTenantAsync("acme");
        var globex = await app.CreateTenantAsync("globex");
        await StaffSeed.InsertAsync(database, tenant.Id, "acme.test", Staff, disabledEvery: 10, viewerEvery: 10);
        await StaffSeed.InsertAsync(database, globex.Id, "globex.test", Staff, disabledEvery: 10, viewerEvery: 10);
        await ExecuteAsync(database, "VACUUM ANALYZE identity_users");
        await ExecuteAsync(database, "VACUUM ANALYZE role_assignments");
        var big = new BigTenant(new StaffTenant(app, tenant, database), recorder);
        Assert.Equal(HttpStatusCode.OK, (await big.Tenant.GetAsync(string.Empty)).StatusCode);
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

    private sealed record BigTenant(StaffTenant Tenant, CommandRecorder Recorder) : IAsyncDisposable
    {
        public async Task<string> CursorAtAsync(string query)
        {
            var page = await Tenant.PageAsync(Append(query, "size=100"));
            for (var i = 0; i < 4; i++)
            {
                page = await Tenant.PageAsync(Append(query, $"size=100&after={page.GetProperty("nextCursor").GetString()}"));
            }

            return page.GetProperty("nextCursor").GetString()!;
        }

        public ValueTask DisposeAsync() => Tenant.DisposeAsync();
    }
}

internal sealed record RecordedCommand(string Text, IReadOnlyList<(string Name, object Value)> Parameters);

internal sealed class CommandRecorder : DbCommandInterceptor
{
    private readonly ConcurrentQueue<RecordedCommand> commands = new();

    public IReadOnlyList<RecordedCommand> Commands => [.. commands.Where(c => !c.Text.Contains("__EFMigrationsHistory", StringComparison.Ordinal))];

    public void Clear() => commands.Clear();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        commands.Enqueue(new RecordedCommand(
            command.CommandText,
            [.. command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value ?? DBNull.Value))]));
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
