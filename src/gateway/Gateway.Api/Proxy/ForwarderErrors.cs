using Yarp.ReverseProxy.Forwarder;

namespace Gateway.Api.Proxy;

public sealed class ForwarderErrors(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await next(context);
        if (context.Features.Get<IForwarderErrorFeature>() is not null && !context.Response.HasStarted)
        {
            var status = context.Response.StatusCode is StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            await Problems.WriteAsync(context, status, Problems.UpstreamUnavailable);
        }
    }
}
