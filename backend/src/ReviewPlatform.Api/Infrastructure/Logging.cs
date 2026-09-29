using System.Text.RegularExpressions;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using ReviewPlatform.Infrastructure.Identity;

namespace ReviewPlatform.Api.Infrastructure;

/// <summary>
/// Serilog: в продакшене — JSON в stdout (одна строка на событие, для сборщика логов), в разработке — читаемый текст.
/// Уровни — секция Serilog:MinimumLevel, формат — Serilog:Format (json | text).
/// </summary>
internal static partial class Logging
{
    public static IServiceCollection AddApiLogging(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSerilog((provider, logger) =>
        {
            logger.ReadFrom.Configuration(configuration)
                .ReadFrom.Services(provider)
                .Enrich.FromLogContext();
            if (string.Equals(configuration["Serilog:Format"], "text", StringComparison.OrdinalIgnoreCase))
            {
                logger.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        });

    /// <summary>
    /// Одна запись на запрос. Токен респондента в пути — секрет (им открывается анкета), поэтому в логе он маскируется.
    /// Проверки /health не засоряют лог.
    /// </summary>
    public static IApplicationBuilder UseApiRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.GetMessageTemplateProperties = (http, path, elapsed, status) =>
            [
                new LogEventProperty("RequestMethod", new ScalarValue(http.Request.Method)),
                new LogEventProperty("RequestPath", new ScalarValue(MaskPath(path))),
                new LogEventProperty("StatusCode", new ScalarValue(status)),
                new LogEventProperty("Elapsed", new ScalarValue(elapsed)),
            ];
            options.GetLevel = (http, _, exception) =>
                exception is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
                : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information;
            options.EnrichDiagnosticContext = (diagnostics, http) =>
            {
                diagnostics.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString());
                if (http.User.FindFirst(ClaimNames.Subject)?.Value is { } userId)
                {
                    diagnostics.Set("UserId", userId);
                }
            };
        });

    public static string MaskPath(string path) => SurveyToken().Replace(path, "/surveys/***");

    [GeneratedRegex("/surveys/[^/]+", RegexOptions.IgnoreCase)]
    private static partial Regex SurveyToken();
}
