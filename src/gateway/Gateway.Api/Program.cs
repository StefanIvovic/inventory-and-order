using Gateway.Api;
using Gateway.Api.Bff;
using Gateway.Api.Configuration;
using Gateway.Api.Proxy;
using Gateway.Api.Sessions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);
var settings = GatewaySettings.Read(builder.Configuration, builder.Environment);

builder.Services.AddSingleton(settings);
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddGatewayProblems();
builder.Services.AddHealthChecks();

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("gateway");
if (!string.IsNullOrWhiteSpace(settings.Keys.Path))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(settings.Keys.Path));
}

builder.Services.AddTransient<IdentityBackchannelHandler>();
builder.Services.AddHttpClient(IdentityBackchannelHandler.ClientName).AddHttpMessageHandler<IdentityBackchannelHandler>();
builder.Services.AddBffSessions(settings);
builder.Services.AddSingleton<TokenRefresher>();
builder.Services.Configure<RouteOptions>(options => options.SetParameterPolicy<SlugConstraint>(SlugConstraint.Name));
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context => ProxyTransforms.Apply(context, settings));

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapBffEndpoints();
app.MapReverseProxy(proxy =>
{
    proxy.UseMiddleware<ForwarderErrors>();
    proxy.UseMiddleware<ApiSessionMiddleware>();
});

await app.RunAsync();

public partial class Program;
