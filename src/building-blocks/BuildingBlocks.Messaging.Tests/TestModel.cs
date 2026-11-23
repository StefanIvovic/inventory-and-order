using System.Collections.Concurrent;
using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using BuildingBlocks.Core.Tenancy;
using BuildingBlocks.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.Attributes;

namespace BuildingBlocks.Messaging.Tests;

public sealed class Widget : ITenantOwned
{
    private Widget()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TenantId { get; private set; } = string.Empty;

    public static Widget Create(string name, string tenantId = "") => new() { Id = Guid.NewGuid(), Name = name, TenantId = tenantId };
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}

[MessageIdentity("test.widget-created.v1")]
public sealed record WidgetCreated(Guid WidgetId, string Name);

[MessageIdentity("test.widget-audited.v1")]
public sealed record WidgetAudited(Guid WidgetId);

public sealed record AddWidget(string Name, bool FailAfterAdd);

public sealed record CreateWidget(string Name);

public sealed record WidgetCreatedResponse(Guid WidgetId);

public sealed record Received(string Message, string? TenantId, Guid? InitiatedBy, string? CorrelationId, string? MessageType, int VisibleWidgets);

public sealed class Recorder
{
    private readonly ConcurrentQueue<Received> received = new();

    public bool CascadeAudit { get; set; }

    public IReadOnlyList<Received> All => received.ToArray();

    public int Count(string message) => received.Count(r => r.Message == message);

    public void Add(Received entry) => received.Enqueue(entry);
}

public static class TestPermissions
{
    public const string CreateWidget = "widgets.create";

    public static readonly Guid AllowedUser = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    public static readonly Guid DeniedUser = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
}

public sealed class StaticPermissionProvider : IPermissionProvider
{
    public Task<IReadOnlySet<string>> GetPermissionsAsync(ICurrentUser user, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(user.UserId == TestPermissions.AllowedUser
            ? new HashSet<string>(StringComparer.Ordinal) { TestPermissions.CreateWidget }
            : new HashSet<string>(StringComparer.Ordinal));
}

public static class AddWidgetHandler
{
    public static WidgetCreated Handle(AddWidget command, TestDbContext db)
    {
        var widget = Widget.Create(command.Name);
        db.Widgets.Add(widget);
        if (command.FailAfterAdd)
        {
            throw new InvalidOperationException("handler failed after adding the widget");
        }

        return new WidgetCreated(widget.Id, command.Name);
    }
}

public sealed class CreateWidgetHandler(IRequestContext context, TestDbContext db) : CommandHandler<TestDbContext>(context, db)
{
    public async Task<(WidgetCreatedResponse, OutgoingMessages)> Handle(CreateWidget command, CancellationToken cancellationToken)
    {
        await Permissions.EnsureAsync(TestPermissions.CreateWidget, cancellationToken);
        var widget = Widget.Create(command.Name);
        Db.Widgets.Add(widget);
        return (new WidgetCreatedResponse(widget.Id), new OutgoingMessages { new WidgetCreated(widget.Id, command.Name) });
    }
}

public sealed class WidgetCreatedRecorderHandler(IEventContext context, TestDbContext db) : MessageHandler<TestDbContext>(context, db)
{
    public async Task<OutgoingMessages> Handle(WidgetCreated message, Recorder recorder, Envelope envelope, CancellationToken cancellationToken)
    {
        var visible = await Db.Widgets.CountAsync(cancellationToken);
        recorder.Add(new Received(nameof(WidgetCreated), Tenant.TenantId, InitiatedByUserId, CorrelationId, envelope.MessageType, visible));
        var outgoing = new OutgoingMessages();
        if (recorder.CascadeAudit)
        {
            outgoing.Add(new WidgetAudited(message.WidgetId));
        }

        return outgoing;
    }
}

public sealed class WidgetAuditedRecorderHandler(IEventContext context, TestDbContext db) : MessageHandler<TestDbContext>(context, db)
{
    public async Task Handle(WidgetAudited message, Recorder recorder, Envelope envelope, CancellationToken cancellationToken)
    {
        var visible = await Db.Widgets.CountAsync(cancellationToken);
        recorder.Add(new Received(nameof(WidgetAudited), Tenant.TenantId, InitiatedByUserId, CorrelationId, envelope.MessageType, visible));
    }
}
