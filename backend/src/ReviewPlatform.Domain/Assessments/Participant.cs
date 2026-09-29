using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Assessments;

public sealed class Participant : Entity
{
    private Participant() { }

    internal Participant(Guid sessionId, string fullName, string email, EvaluatorRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        SessionId = sessionId;
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Role = role;
        Status = ParticipantStatus.Pending;
    }

    public Guid SessionId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public EvaluatorRole Role { get; private set; }
    public string? TokenHash { get; private set; }
    public DateTime? TokenIssuedAtUtc { get; private set; }
    public ParticipantStatus Status { get; private set; }
    public DateTime? FirstOpenedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? LastReminderAtUtc { get; private set; }

    /// <summary>Участвует в сессии (не удалён).</summary>
    public bool IsActive => Status != ParticipantStatus.Removed;

    internal AccessToken IssueToken(DateTime nowUtc)
    {
        var token = AccessToken.Create();
        TokenHash = token.Hash;
        TokenIssuedAtUtc = nowUtc;
        return token;
    }

    internal void Remove()
    {
        Status = ParticipantStatus.Removed;
        TokenHash = null;
    }
}
