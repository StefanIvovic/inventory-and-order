namespace Identity.Tests;

internal static class Eventually
{
    public static async Task AssertAsync(Func<Task<bool>> condition, string description)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, $"Not true within 10 seconds: {description}");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
