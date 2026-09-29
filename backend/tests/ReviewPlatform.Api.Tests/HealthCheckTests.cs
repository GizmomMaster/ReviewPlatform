namespace ReviewPlatform.Api.Tests;

public sealed class HealthCheckTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_WithDatabase_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal("Healthy", body);
    }
}
