using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Audit;

/// <summary>Запись журнала аудита: кто, когда и что сделал (ТЗ, 10.1).</summary>
public sealed class AuditEntry : Entity
{
    private AuditEntry() { }

    public AuditEntry(DateTime occurredAtUtc, Guid? userId, string action, string entityType, Guid entityId, string? details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        OccurredAtUtc = occurredAtUtc;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
    }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>null — действие системы (например, планировщика).</summary>
    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public string? Details { get; private set; }
}

/// <summary>Коды действий журнала.</summary>
public static class AuditActions
{
    public const string SessionLaunched = "session.launched";
    public const string SessionCancelled = "session.cancelled";
    public const string SessionClosedEarly = "session.closed_early";
    public const string SessionDraftDeleted = "session.draft_deleted";
    public const string SessionDecided = "session.decided";
    public const string SessionExtended = "session.extended";
    public const string SessionOverdue = "session.overdue";
    public const string RemindersSent = "session.reminders_sent";
    public const string ParticipantAdded = "participant.added";
    public const string ParticipantRemoved = "participant.removed";
    public const string LinkReissued = "participant.link_reissued";
    public const string InviteResent = "participant.invite_resent";
    public const string MatrixImported = "matrix.imported";
}
