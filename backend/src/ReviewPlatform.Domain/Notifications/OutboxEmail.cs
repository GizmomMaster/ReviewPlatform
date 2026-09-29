using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Notifications;

public enum EmailType
{
    Invitation,
    LinkReissued,
    Reminder,
    SurveyCompleted,
    DeadlinePassed,
    DeadlineExtended,
    SessionCancelled,
}

public enum OutboxStatus
{
    Pending,
    Sent,

    /// <summary>Попытки исчерпаны, письмо больше не отправляется.</summary>
    Failed,
}

/// <summary>
/// Письмо в transactional outbox (ТЗ, 3.1): сохраняется в одной транзакции с бизнес-изменением,
/// фоновый отправщик доставляет его с повторами.
/// </summary>
public sealed class OutboxEmail : Entity
{
    public const int MaxSubjectLength = 300;
    public const int MaxErrorLength = 2000;

    private OutboxEmail() { }

    public OutboxEmail(EmailType type, string to, string subject, string body, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        Type = type;
        To = to;
        Subject = subject.Length > MaxSubjectLength ? subject[..MaxSubjectLength] : subject;
        Body = body;
        Status = OutboxStatus.Pending;
        CreatedAtUtc = nowUtc;
        NextAttemptAtUtc = nowUtc;
    }

    public EmailType Type { get; private set; }
    public string To { get; private set; } = null!;
    public string Subject { get; private set; } = null!;

    /// <summary>HTML письма.</summary>
    public string Body { get; private set; } = null!;

    public OutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }

    /// <summary>null — отправлять больше не нужно.</summary>
    public DateTime? NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    /// <summary>Резерв за отправщиком: до <paramref name="untilUtc"/> письмо не возьмёт другой экземпляр.</summary>
    public void Lease(DateTime untilUtc) => NextAttemptAtUtc = untilUtc;

    public void MarkSent(DateTime nowUtc)
    {
        Attempts++;
        Status = OutboxStatus.Sent;
        SentAtUtc = nowUtc;
        NextAttemptAtUtc = null;
    }

    /// <summary>Неудачная попытка: следующая — с экспоненциальной паузой, после <paramref name="maxAttempts"/> письмо помечается Failed.</summary>
    public void MarkAttemptFailed(string error, DateTime nowUtc, int maxAttempts)
    {
        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        if (Attempts >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            NextAttemptAtUtc = null;
        }
        else
        {
            NextAttemptAtUtc = nowUtc + RetryDelay(Attempts);
        }
    }

    /// <summary>1, 2, 4, 8… минут, но не больше часа.</summary>
    public static TimeSpan RetryDelay(int attempts) => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attempts - 1))));
}
