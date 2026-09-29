using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Audit;
using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Application.Notifications;

// Задачи планировщика. Каждая сессия обрабатывается и сохраняется отдельно: конфликт версии
// (параллельно действует руководитель или респондент) пропускает сессию до следующего тика.
// Повторный тик ничего не делает повторно: просрочка меняет статус, напоминание — время выпуска ссылки.

/// <summary>Переводит опросы с прошедшим дедлайном в Overdue и пишет владельцу.</summary>
/// <returns>Сколько сессий переведено.</returns>
public sealed record ProcessOverdueSessionsCommand : IRequest<int>;

internal sealed class ProcessOverdueSessionsHandler(
    IAppDbContext db, SessionNotifier notifier, IAuditLog audit, TimeProvider time, ILogger<ProcessOverdueSessionsHandler> logger)
    : IRequestHandler<ProcessOverdueSessionsCommand, int>
{
    public async Task<int> Handle(ProcessOverdueSessionsCommand request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var ids = await db.AssessmentSessions
            .Where(s => s.Status == SessionStatus.InProgress && s.DeadlineAtUtc < now)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            var session = await db.AssessmentSessions.Include(s => s.Participants).SingleAsync(s => s.Id == id, cancellationToken);
            if (!session.MarkOverdue(now))
            {
                continue;
            }

            await notifier.DeadlinePassedAsync(session, cancellationToken);
            audit.Record(AuditActions.SessionOverdue, nameof(AssessmentSession), session.Id,
                $"Не отправили анкету: {session.PendingParticipants.Count()}");
            if (await JobSave.TrySaveAsync(db, logger, session.Id, cancellationToken))
            {
                processed++;
            }
        }

        db.ChangeTracker.Clear();
        return processed;
    }
}

/// <summary>Напоминания за N дней до дедлайна (AssessmentPolicy.ReminderDaysBeforeDeadline).</summary>
/// <returns>Сколько напоминаний поставлено в очередь.</returns>
public sealed record SendRemindersCommand : IRequest<int>;

internal sealed class SendRemindersHandler(
    IAppDbContext db, SessionNotifier notifier, IAuditLog audit, NotificationPolicy policy, TimeProvider time, ILogger<SendRemindersHandler> logger)
    : IRequestHandler<SendRemindersCommand, int>
{
    public async Task<int> Handle(SendRemindersCommand request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var days = policy.ReminderDays;
        if (days.Count == 0)
        {
            return 0;
        }

        var horizon = now.AddDays(days.Max());
        var ids = await db.AssessmentSessions
            .Where(s => s.Status == SessionStatus.InProgress && s.DeadlineAtUtc > now && s.DeadlineAtUtc <= horizon)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            var session = await db.AssessmentSessions.Include(s => s.Participants).SingleAsync(s => s.Id == id, cancellationToken);
            var tokens = session.Remind([.. days], now);
            if (tokens.Count == 0)
            {
                continue;
            }

            await notifier.SurveyLinksAsync(session, EmailType.Reminder, tokens, cancellationToken);
            audit.Record(AuditActions.RemindersSent, nameof(AssessmentSession), session.Id,
                string.Join(", ", session.Participants.Where(p => tokens.ContainsKey(p.Id)).Select(AuditTexts.Participant)));
            if (await JobSave.TrySaveAsync(db, logger, session.Id, cancellationToken))
            {
                sent += tokens.Count;
            }
        }

        db.ChangeTracker.Clear();
        return sent;
    }
}

internal static partial class JobSave
{
    public static async Task<bool> TrySaveAsync(IAppDbContext db, ILogger logger, Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            LogConcurrentChange(logger, sessionId);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} changed concurrently, will retry on next tick.")]
    private static partial void LogConcurrentChange(ILogger logger, Guid sessionId);
}
