using Microsoft.Extensions.Configuration;
using ReviewPlatform.Application.Common;

namespace ReviewPlatform.Infrastructure;

internal sealed class AppLinks(IConfiguration configuration) : IAppLinks
{
    private readonly string _baseUrl = (configuration["FrontendBaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

    public string Survey(string token) => $"{_baseUrl}/survey/{Uri.EscapeDataString(token)}";

    public string Session(Guid sessionId) => $"{_baseUrl}/admin/sessions/{sessionId}";

    public string Report(Guid sessionId) => $"{_baseUrl}/admin/sessions/{sessionId}/report";
}
