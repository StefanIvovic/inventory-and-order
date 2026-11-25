using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Permissions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace BuildingBlocks.Validation.Tests;

public sealed record GuardedThing(string Name, int Quantity, bool Allowed);

public sealed class GuardedThingValidator : CommandValidator<GuardedThing>
{
    public GuardedThingValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class GuardedThingHandler(ICommandValidation<GuardedThing> validation, Counter counter)
{
    public async Task<ThingCreated> Handle(GuardedThing command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Allowed)
        {
            throw new PermissionDeniedException(["things.create"]);
        }

        await validation.EnsureValidAsync(command, cancellationToken);
        counter.Hit(nameof(GuardedThing));
        return new ThingCreated(Guid.NewGuid());
    }
}

public sealed class CommandValidationTests
{
    [Fact]
    public async Task PermissionCheckWinsOverAnInvalidBody()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsJsonAsync("/guarded", new GuardedThing("", 0, false), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, app.Counter.Of(nameof(GuardedThing)));
    }

    [Fact]
    public async Task PermittedInvalidCommandGetsEveryFieldError()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsJsonAsync("/guarded", new GuardedThing("", 0, true), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.Equal(["name", "quantity"], body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(0, app.Counter.Of(nameof(GuardedThing)));
    }

    [Fact]
    public async Task PermittedValidCommandReachesTheWork()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsJsonAsync("/guarded", new GuardedThing("ok", 1, true), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, app.Counter.Of(nameof(GuardedThing)));
    }

    [Fact]
    public async Task CommandValidatorsAreNotRegisteredForTheAutomaticMiddleware()
    {
        await using var app = await TestApp.StartAsync();

        Assert.Null(app.App.Services.GetService<IValidator<GuardedThing>>());
        Assert.NotNull(app.App.Services.GetService<IValidator<CreateThing>>());
    }

    [Fact]
    public async Task InvokingAnInvalidCommandAsAMessageAlsoThrows()
    {
        await using var app = await TestApp.StartAsync();
        var bus = app.App.Services.GetRequiredService<IMessageBus>();

        await Assert.ThrowsAsync<ValidationException>(() => bus.InvokeAsync<ThingCreated>(new GuardedThing("", 0, true), TestContext.Current.CancellationToken));
    }
}
