using BuildingBlocks.Testing;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;

namespace BuildingBlocks.Validation.Tests;

public sealed class MessageValidationTests
{
    [Fact]
    public async Task InvalidInlineMessageThrowsAndNeverReachesTheHandler()
    {
        await using var app = await TestApp.StartAsync();
        var bus = app.App.Services.GetRequiredService<IMessageBus>();

        await Assert.ThrowsAsync<ValidationException>(() => bus.InvokeAsync(new CreateThing("", 0, Guid.Empty, []), TestContext.Current.CancellationToken));

        Assert.Equal(0, app.Counter.Of(nameof(CreateThing)));
    }

    [Fact]
    public async Task MessageWithoutValidatorIsHandled()
    {
        await using var app = await TestApp.StartAsync();

        await app.App.Services.GetRequiredService<IMessageBus>().InvokeAsync(new PlainThing("x"), TestContext.Current.CancellationToken);

        Assert.Equal(1, app.Counter.Of(nameof(PlainThing)));
    }
}

public sealed class DeadLetterValidationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task InvalidDurableMessageIsDeadLetteredOnceWithoutRetry()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var connectionString = await postgres.CreateDatabaseAsync(cancellation);
        await using var app = await TestApp.StartAsync(connectionString);
        ProcessThingValidator.Reset();

        await app.App.Services.GetRequiredService<IMessageBus>().PublishAsync(new ProcessThing(""));

        var deadline = DateTime.UtcNow.AddSeconds(60);
        long deadLetters = 0;
        while (deadLetters == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(250, cancellation);
            deadLetters = await CountDeadLettersAsync(connectionString, cancellation);
        }

        await Task.Delay(TimeSpan.FromSeconds(3), cancellation);
        Assert.Equal(1, await CountDeadLettersAsync(connectionString, cancellation));
        Assert.Equal(1, ProcessThingValidator.Evaluations);
        Assert.Equal(0, app.Counter.Of(nameof(ProcessThing)));
    }

    private static async Task<long> CountDeadLettersAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM wolverine.wolverine_dead_letters", connection);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
