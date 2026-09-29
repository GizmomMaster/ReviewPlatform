namespace ReviewPlatform.Application.Common;

/// <summary>Журнал аудита. Запись добавляется в текущий контекст и сохраняется вместе с действием.</summary>
public interface IAuditLog
{
    void Record(string action, string entityType, Guid entityId, string? details = null);
}

internal sealed class AuditLog(IAppDbContext db, ICurrentUser currentUser, TimeProvider time) : IAuditLog
{
    public void Record(string action, string entityType, Guid entityId, string? details = null) =>
        db.AuditEntries.Add(new Domain.Audit.AuditEntry(
            time.GetUtcNow().UtcDateTime,
            currentUser.IsAuthenticated ? currentUser.UserId : null,
            action,
            entityType,
            entityId,
            details));
}
