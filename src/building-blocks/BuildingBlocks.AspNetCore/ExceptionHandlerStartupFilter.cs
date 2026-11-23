using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace BuildingBlocks.AspNetCore;

internal sealed class ExceptionHandlerStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseExceptionHandler();
            next(app);
        };
}
