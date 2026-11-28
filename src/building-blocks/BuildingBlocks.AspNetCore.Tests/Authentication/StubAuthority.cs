using System.Security.Cryptography;
using BuildingBlocks.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.AspNetCore.Tests.Authentication;

public sealed class StubAuthority : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly List<RsaSecurityKey> keys = [];
    private readonly Lock gate = new();
    private readonly List<(string? Proto, string? Host)> forwarded = [];

    private StubAuthority(WebApplication app, string address)
    {
        this.app = app;
        Address = address;
    }

    public string Address { get; }

    public IReadOnlyList<(string? Proto, string? Host)> Forwarded
    {
        get
        {
            lock (gate)
            {
                return [.. forwarded];
            }
        }
    }

    public static async Task<StubAuthority> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        StubAuthority? stub = null;

        app.MapGet("/.well-known/openid-configuration", (HttpContext context) =>
        {
            stub!.Record(context);
            var proto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? context.Request.Scheme;
            var host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? context.Request.Host.Value;
            return Results.Json(new Dictionary<string, object>
            {
                ["issuer"] = TestTokens.Issuer,
                ["jwks_uri"] = $"{proto}://{host}/jwks",
            });
        });
        app.MapGet("/jwks", (HttpContext context) =>
        {
            stub!.Record(context);
            return Results.Json(new Dictionary<string, object> { ["keys"] = stub.Jwks() });
        });

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        stub = new StubAuthority(app, address);
        return stub;
    }

    public void Publish(RsaSecurityKey key)
    {
        lock (gate)
        {
            keys.Add(key);
        }
    }

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    private void Record(HttpContext context)
    {
        lock (gate)
        {
            forwarded.Add((context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault(), context.Request.Headers["X-Forwarded-Host"].FirstOrDefault()));
        }
    }

    private List<Dictionary<string, string>> Jwks()
    {
        lock (gate)
        {
            return keys.Select(key =>
            {
                var parameters = key.Rsa.ExportParameters(false);
                return new Dictionary<string, string>
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = SecurityAlgorithms.RsaSha256,
                    ["kid"] = key.KeyId,
                    ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
                    ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!),
                };
            }).ToList();
        }
    }
}
