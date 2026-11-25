using System.Collections.Concurrent;
using BuildingBlocks.AspNetCore;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Http;
using Wolverine.Postgresql;

namespace BuildingBlocks.Validation.Tests;

public sealed record ThingLine(string Sku);

public sealed record CreateThing(string Name, int Quantity, Guid WarehouseId, ThingLine[] Lines);

public sealed record ThingCreated(Guid Id);

public sealed record ManualThing(string Value);

public sealed record PlainThing(string Value);

public sealed record ProcessThing(string Name);

public sealed class Counter
{
    private readonly ConcurrentDictionary<string, int> counts = new();

    public int Of(string name) => counts.TryGetValue(name, out var count) ? count : 0;

    public void Hit(string name) => counts.AddOrUpdate(name, 1, (_, count) => count + 1);
}

public sealed class CreateThingValidator : AbstractValidator<CreateThing>
{
    public CreateThingValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.WarehouseId).NotEmpty().Must(id => id != Guid.Empty).WithMessage("'Warehouse Id' must be a known warehouse.");
        RuleForEach(x => x.Lines).ChildRules(line => line.RuleFor(l => l.Sku).NotEmpty());
    }
}

public sealed class CreateThingSecondValidator : AbstractValidator<CreateThing>
{
    public CreateThingSecondValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
    }
}

public sealed class ProcessThingValidator : AbstractValidator<ProcessThing>
{
    private static int evaluations;

    public static int Evaluations => Volatile.Read(ref evaluations);

    public static void Reset() => Interlocked.Exchange(ref evaluations, 0);

    public ProcessThingValidator()
    {
        RuleFor(x => x.Name).Must(name =>
        {
            Interlocked.Increment(ref evaluations);
            return !string.IsNullOrEmpty(name);
        }).WithMessage("'Name' must not be empty.");
    }
}

public static class CreateThingHandler
{
    public static ThingCreated Handle(CreateThing command, Counter counter)
    {
        counter.Hit(nameof(CreateThing));
        return new ThingCreated(Guid.NewGuid());
    }
}

public static class ManualThingHandler
{
    public const string SecretValue = "VALUE-SECRET-42";

    public static ThingCreated Handle(ManualThing command) =>
        throw new ValidationException(
        [
            new ValidationFailure("Code", "'Code' is not in the expected format.") { AttemptedValue = SecretValue },
            new ValidationFailure("Address.PostalCode", "'Postal Code' must not be empty."),
        ]);
}

public static class PlainThingHandler
{
    public static void Handle(PlainThing command, Counter counter) => counter.Hit(nameof(PlainThing));
}

public static class ProcessThingHandler
{
    public static void Handle(ProcessThing command, Counter counter) => counter.Hit(nameof(ProcessThing));
}

public sealed class TestApp : IAsyncDisposable
{
    private TestApp(WebApplication app, CapturingLoggerProvider logs)
    {
        App = app;
        Logs = logs;
        Client = app.GetTestClient();
        Counter = app.Services.GetRequiredService<Counter>();
    }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public Counter Counter { get; }

    public CapturingLoggerProvider Logs { get; }

    public static async Task<TestApp> StartAsync(string? durableConnectionString = null)
    {
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddBuildingBlocks();
        builder.Services.AddSingleton<Counter>();
        builder.Services.AddWolverineHttp();
        builder.Host.UseWolverine(options =>
        {
            options.ApplicationAssembly = typeof(TestApp).Assembly;
            options.UseBuildingBlocksValidation();
            if (durableConnectionString is not null)
            {
                options.PersistMessagesWithPostgresql(durableConnectionString, "wolverine");
                options.Durability.Mode = DurabilityMode.Solo;
                options.LocalQueue("things").UseDurableInbox();
                options.PublishMessage<ProcessThing>().ToLocalQueue("things");
            }
        });

        var app = builder.Build();
        app.UseBuildingBlocks();
        app.MapPostToWolverine<CreateThing, ThingCreated>("/things");
        app.MapPostToWolverine<ManualThing, ThingCreated>("/manual");
        app.MapPostToWolverine<GuardedThing, ThingCreated>("/guarded");
        await app.StartAsync();
        return new TestApp(app, logs);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
    }
}
