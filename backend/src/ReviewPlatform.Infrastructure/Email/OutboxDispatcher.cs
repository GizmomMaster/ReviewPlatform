using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReviewPlatform.Infrastructure.Email;

/// <summary>Фоновый отправщик outbox: пока есть письма — порция за порцией, иначе пауза.</summary>
internal sealed partial class OutboxDispatcher(IServiceScopeFactory scopes, IOptions<OutboxOptions> options, TimeProvider time, ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            LogDisabled();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                processed = await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDispatchFailed(ex);
            }

            if (processed < settings.BatchSize)
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), time, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatcher is disabled.")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatch failed.")]
    private partial void LogDispatchFailed(Exception exception);
}
