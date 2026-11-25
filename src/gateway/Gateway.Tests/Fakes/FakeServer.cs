using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gateway.Tests.Fakes;

public sealed record RecordedRequest(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Headers, IReadOnlyDictionary<string, string> Form);

public abstract class FakeServer : IAsyncDisposable
{
    private WebApplication? app;

    public Uri Address { get; private set; } = new("http://127.0.0.1/");

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var built = builder.Build();
        built.Use(async (context, next) =>
        {
            await RecordAsync(context);
            await next(context);
        });
        Map(built);
        await built.StartAsync();
        var address = built.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Address = new Uri(address.EndsWith('/') ? address : address + "/");
        app = built;
    }

    public IReadOnlyList<RecordedRequest> RequestsTo(string path) => Requests.Where(request => request.Path == path).ToList();

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    protected abstract void Map(WebApplication app);

    protected static IResult Echo(HttpContext context)
    {
        var headers = context.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        return Results.Json(new EchoBody(context.Request.Method, context.Request.Path.Value ?? string.Empty, context.Request.QueryString.Value ?? string.Empty, headers));
    }

    private async Task RecordAsync(HttpContext context)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Request.HasFormContentType)
        {
            context.Request.EnableBuffering();
            foreach (var (key, value) in await context.Request.ReadFormAsync())
            {
                form[key] = value.ToString();
            }

            context.Request.Body.Position = 0;
        }

        Requests.Enqueue(new RecordedRequest(
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            context.Request.QueryString.Value ?? string.Empty,
            context.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            form));
    }
}

public sealed record EchoBody(string Method, string Path, string Query, Dictionary<string, string> Headers);

public sealed class SpaStub : FakeServer
{
    protected override void Map(WebApplication app) => app.MapFallback("{**path}", Echo);
}
