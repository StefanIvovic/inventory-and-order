namespace BuildingBlocks.Messaging.Tests;

public static class Waiting
{
    public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout, string because)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out after {timeout.TotalSeconds}s waiting until {because}.");
            }

            await Task.Delay(200);
        }
    }
}
