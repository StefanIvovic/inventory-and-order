using BuildingBlocks.Core.Handlers;
using BuildingBlocks.Core.Identity;
using BuildingBlocks.Core.Permissions;
using BuildingBlocks.Core.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    public const string MessageStorageSchema = "wolverine";

    public static WolverineOptions UseBuildingBlocksMessaging(this WolverineOptions options, MessagingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        options.ServiceName = settings.ServiceName;
        options.PersistMessagesWithPostgresql(settings.PostgresConnectionString, MessageStorageSchema);
        options.UseEntityFrameworkCoreTransactions();
        options.Policies.AutoApplyTransactions();
        options.Policies.UseDurableInboxOnAllListeners();
        options.Policies.UseDurableOutboxOnAllSendingEndpoints();
        options.Policies.UseDurableLocalQueues();
        options.UseRabbitMq(settings.RabbitMqUri).AutoProvision();

        options.Policies.PropagateIncomingHeaderToOutgoing(MessageHeaders.UserId);
        options.MetadataRules.Add(new IdentityEnvelopeRule(new HttpContextAccessor()));
        options.Policies.AddMiddleware(typeof(TenantContextMiddleware));
        options.CodeGeneration.AlwaysUseServiceLocationFor<ITenantContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IEventContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IRequestContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<ICurrentUser>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IPermissionManager>();

        options.Services.AddHttpContextAccessor();
        options.Services.TryAddSingleton(TimeProvider.System);
        options.Services.TryAddScoped<IEventContext, WolverineEventContext>();

        return options;
    }
}
