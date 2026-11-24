using BuildingBlocks.AspNetCore;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Messaging;
using Identity.Api.Authorization;
using Identity.Api.Health;
using Identity.Api.Persistence;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("ConnectionStrings:Identity is required.");
var rabbitMqUri = new Uri(builder.Configuration["RabbitMq:Uri"]
    ?? throw new InvalidOperationException("RabbitMq:Uri is required."));

builder.Services.AddBuildingBlocks();
builder.Services.AddBuildingBlocksAuthentication(builder.Configuration);
builder.Services.AddBuildingBlocksPermissions<IdentityPermissionProvider>();
builder.Services.AddDbContextWithWolverineIntegration<IdentityDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<MigrationRunner>();
builder.Services.AddHealthChecks().AddCheck<ReadinessCheck>(ReadinessCheck.Tag, tags: [ReadinessCheck.Tag]);
builder.Services.AddWolverineHttp();

builder.Host.UseWolverine(options =>
{
    options.ApplicationAssembly = typeof(Program).Assembly;
    options.UseBuildingBlocksMessaging(new MessagingSettings("identity", connectionString, rabbitMqUri));
#if DEBUG
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
#else
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
#endif
});

var app = builder.Build();
app.UseBuildingBlocksAuthentication();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadinessCheck.Tag) }).AllowAnonymous();
app.MapWolverineEndpoints();

return await app.RunJasperFxCommands(args);

public partial class Program;
