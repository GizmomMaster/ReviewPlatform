using System.Threading.RateLimiting;

namespace ReviewPlatform.Api.Infrastructure;

internal static class RateLimits
{
    public const string Survey = "survey";
    public const string Login = "login";

    public static IServiceCollection AddApiRateLimits(this IServiceCollection services, IConfiguration configuration) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Анкета: автосохранение раз в ~1 с при наборе комментария — лимит с запасом
            options.AddPolicy(Survey, http => PerIp(http, configuration.GetValue("RateLimits:SurveyPerMinute", 120)));
            // Вход: защита от перебора паролей в дополнение к блокировке учётной записи
            options.AddPolicy(Login, http => PerIp(http, configuration.GetValue("RateLimits:LoginPerMinute", 10)));
        });

    private static RateLimitPartition<string> PerIp(HttpContext http, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) });
}
