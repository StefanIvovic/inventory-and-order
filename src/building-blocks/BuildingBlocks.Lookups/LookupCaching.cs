using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace BuildingBlocks.Lookups;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class LookupCachingAttribute : Attribute;

public static class LookupCaching
{
    public const int MaxAgeSeconds = 300;

    public static IApplicationBuilder UseLookupCaching(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(static (context, next) => Applies(context) ? CacheAsync(context, next) : next(context));
    }

    public static TBuilder WithLookupCaching<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new LookupCachingAttribute());
    }

    private static bool Applies(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<LookupCachingAttribute>() is not null
        && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method));

    private static async Task CacheAsync(HttpContext context, RequestDelegate next)
    {
        var response = context.Response;
        var original = response.Body;
        await using var buffer = new MemoryStream();
        response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            response.Body = original;
        }

        if (response.StatusCode == StatusCodes.Status200OK)
        {
            var tag = Tag(buffer);
            var headers = response.GetTypedHeaders();
            headers.ETag = tag;
            headers.CacheControl = new CacheControlHeaderValue { Private = true, MaxAge = TimeSpan.FromSeconds(MaxAgeSeconds) };
            response.Headers.Append(HeaderNames.Vary, "Cookie, Authorization");
            if (Matches(context.Request, tag))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                response.ContentLength = null;
                response.Headers.ContentType = default;
                return;
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(original, context.RequestAborted);
    }

    private static EntityTagHeaderValue Tag(MemoryStream buffer)
    {
        var hash = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        return new EntityTagHeaderValue($"\"{Base64Url.EncodeToString(hash)[..22]}\"");
    }

    private static bool Matches(HttpRequest request, EntityTagHeaderValue tag) =>
        request.GetTypedHeaders().IfNoneMatch is { Count: > 0 } candidates
        && candidates.Any(candidate => candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(tag, useStrongComparison: false));
}
