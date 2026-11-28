using BuildingBlocks.AspNetCore;
using BuildingBlocks.AspNetCore.Authentication;
using BuildingBlocks.Messaging;
using BuildingBlocks.Validation;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Product.Api.Authorization;
using Product.Api.Health;
using Product.Api.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Product")
    ?? throw new InvalidOperationException("ConnectionStrings:Product is required.");
var rabbitMqUri = new Uri(builder.Configuration["RabbitMq:Uri"]
    ?? throw new InvalidOperationException("RabbitMq:Uri is required."));

builder.Services.AddBuildingBlocks();
builder.Services.AddBuildingBlocksPermissions<ProductPermissionProvider>();
builder.Services.AddDbContextWithWolverineIntegration<ProductDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<StartupTasks>();
builder.Services.AddBuildingBlocksAuthentication(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<ReadinessCheck>(ReadinessCheck.Tag, tags: [ReadinessCheck.Tag]);
builder.Services.AddWolverineHttp();

builder.Host.UseWolverine(options =>
{
    options.ApplicationAssembly = typeof(Program).Assembly;
    options.UseBuildingBlocksMessaging(new MessagingSettings("product", connectionString, rabbitMqUri));
    options.UseBuildingBlocksPermissionInvalidation();
    options.UseBuildingBlocksValidation();
#if DEBUG
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
#else
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
#endif
});

builder.Services.AddHostedService<StartupCompletion>();

var app = builder.Build();
app.UseBuildingBlocksAuthentication();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadinessCheck.Tag) }).AllowAnonymous();
app.MapWolverineEndpoints();

return await app.RunJasperFxCommands(args);

public partial class Program;
