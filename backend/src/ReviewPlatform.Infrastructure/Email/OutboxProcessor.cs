using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewPlatform.Domain.Notifications;
using ReviewPlatform.Infrastructure.Persistence;

namespace ReviewPlatform.Infrastructure.Email;

/// <summary>Одна порция outbox: резервирует готовые к отправке письма и отправляет их.</summary>
internal sealed partial class OutboxProcessor(
    AppDbContext db, IEmailSender sender, IOptions<OutboxOptions> options, TimeProvider time, ILogger<OutboxProcessor> logger)
{
    /// <returns>Сколько писем взято в работу.</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var batch = await ClaimAsync(cancellationToken);
        foreach (var email in batch)
        {
            try
            {
                await sender.SendAsync(email.To, email.Subject, email.Body, cancellationToken);
                email.MarkSent(time.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                email.MarkAttemptFailed(ex.Message, time.GetUtcNow().UtcDateTime, options.Value.MaxAttempts);
                LogAttemptFailed(ex, email.Id, email.Type, email.Attempts);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        db.ChangeTracker.Clear();
        return batch.Count;
    }

    /// <summary>
    /// Короткая транзакция: SKIP LOCKED не даёт двум экземплярам взять одно письмо,
    /// а сдвиг следующей попытки на время резерва скрывает его от других, пока идёт отправка.
    /// </summary>
    private async Task<List<OutboxEmail>> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var pending = nameof(OutboxStatus.Pending);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.EmailOutbox
            .FromSql($"""
                SELECT * FROM email_outbox
                WHERE status = {pending} AND next_attempt_at_utc <= {now}
                ORDER BY next_attempt_at_utc
                LIMIT {options.Value.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var email in batch)
        {
            email.Lease(now.AddSeconds(options.Value.LeaseSeconds));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batch;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email {EmailId} ({Type}) attempt {Attempt} failed.")]
    private partial void LogAttemptFailed(Exception exception, Guid emailId, EmailType type, int attempt);
}
