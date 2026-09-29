using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewPlatform.Application.Notifications;

namespace ReviewPlatform.Infrastructure.Scheduling;

public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>false — сроки не отслеживаются (тесты вызывают задачи напрямую).</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 60;
}

/// <summary>Периодический тик: просрочка опросов, затем напоминания. Задачи идемпотентны — повторный тик безопасен.</summary>
internal sealed partial class DeadlineScheduler(IServiceScopeFactory scopes, IOptions<SchedulerOptions> options, TimeProvider time, ILogger<DeadlineScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            LogDisabled();
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds), time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var overdue = await sender.Send(new ProcessOverdueSessionsCommand(), stoppingToken);
                var reminders = await sender.Send(new SendRemindersCommand(), stoppingToken);
                if (overdue + reminders > 0)
                {
                    LogTick(overdue, reminders);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTickFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deadline scheduler is disabled.")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduler tick: {Overdue} sessions overdue, {Reminders} reminders queued.")]
    private partial void LogTick(int overdue, int reminders);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduler tick failed.")]
    private partial void LogTickFailed(Exception exception);
}
