using Microsoft.Extensions.Configuration;
using ReviewPlatform.Application.Common;

namespace ReviewPlatform.Infrastructure;

internal sealed class SurveyLinks(IConfiguration configuration) : ISurveyLinks
{
    private readonly string _baseUrl = (configuration["FrontendBaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

    public string Build(string token) => $"{_baseUrl}/survey/{Uri.EscapeDataString(token)}";
}
