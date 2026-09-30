using ReviewPlatform.Api.Infrastructure;

namespace ReviewPlatform.Api.Tests;

public sealed class LoggingTests
{
    [Theory]
    [InlineData("/api/surveys/abc-DEF_123", "/api/surveys/***")]
    [InlineData("/api/surveys/abc/submit", "/api/surveys/***/submit")]
    [InlineData("/survey/abc", "/survey/abc")]
    [InlineData("/api/assessment-sessions/1/report", "/api/assessment-sessions/1/report")]
    public void MaskPath_HidesRespondentToken(string path, string expected) => Assert.Equal(expected, Logging.MaskPath(path));
}
