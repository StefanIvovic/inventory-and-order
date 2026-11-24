using System.Net;
using BuildingBlocks.Core.Errors;
using BuildingBlocks.Core.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Tests.Errors;

public sealed class DevelopmentEnvironmentTests
{
    [Theory]
    [InlineData("/denied", HttpStatusCode.Forbidden)]
    [InlineData("/conflict", HttpStatusCode.Conflict)]
    [InlineData("/missing", HttpStatusCode.NotFound)]
    public async Task MappedErrorsKeepTheirStatusInDevelopment(string path, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddBuildingBlocks();
        await using var app = builder.Build();
        app.UseBuildingBlocks();
        app.MapGet("/denied", IResult () => throw new PermissionDeniedException(["sample.view"]));
        app.MapGet("/conflict", IResult () => throw new DomainException("sample.conflict", DomainErrorKind.Conflict, "Conflict."));
        app.MapGet("/missing", IResult () => throw new NotFoundException("sample"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
