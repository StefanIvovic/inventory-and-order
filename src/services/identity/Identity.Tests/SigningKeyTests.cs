using System.Net;
using Identity.Api.Protocol;
using Identity.Tests.Flow;

namespace Identity.Tests;

public sealed class SigningKeyTests(ContainersFixture containers)
{
    [Fact]
    public async Task DirectoryKeysSurviveARestart()
    {
        var folder = Path.Combine(Path.GetTempPath(), "iao-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = await containers.Postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
            var overrides = new Dictionary<string, string?>
            {
                ["Identity:Keys:Mode"] = "Directory",
                ["Identity:Keys:Path"] = folder,
                ["Identity:Keys:Password"] = "test-keys-password",
            };

            string firstJwks;
            string accessToken;
            await using (var first = await IdentityApp.StartAsync(containers, database, overrides))
            {
                accessToken = (await CodeFlow.SignInAsync(first)).AccessToken!;
                using var client = first.CreateClient();
                firstJwks = await client.GetStringAsync("/.well-known/jwks", TestContext.Current.CancellationToken);
            }

            Assert.True(File.Exists(Path.Combine(folder, SigningKeys.SigningFile)));
            Assert.True(File.Exists(Path.Combine(folder, SigningKeys.EncryptionFile)));

            await using var second = await IdentityApp.StartAsync(containers, database, overrides);
            await second.WaitUntilMigratedAsync();
            using var secondClient = second.CreateClient();
            var secondJwks = await secondClient.GetStringAsync("/.well-known/jwks", TestContext.Current.CancellationToken);

            Assert.Equal(firstJwks, secondJwks);
            Assert.Equal(HttpStatusCode.Forbidden, (await secondClient.GetAsync("/roles", accessToken)).StatusCode);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CertificatesModeWithoutPathsStopsStartup()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var app = await IdentityApp.StartAsync(containers, overrides: new Dictionary<string, string?>
            {
                ["Identity:Keys:Mode"] = "Certificates",
                ["Identity:Keys:Password"] = "secret",
            });
        });

        Assert.Contains("Identity:Keys:SigningCertificatePath", error.ToString(), StringComparison.Ordinal);
    }
}
