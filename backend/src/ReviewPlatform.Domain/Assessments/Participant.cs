using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Assessments;

public sealed class Participant : Entity
{
    private readonly List<SurveyAnswer> _answers = [];

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

    /// <summary>Токен оптимистической блокировки (xmin): отправка анкеты и напоминание не перезапишут друг друга.</summary>
    public uint Version { get; private set; }

    /// <summary>Ответы; загружаются только когда нужны (анкета, отчёт).</summary>
    public IReadOnlyCollection<SurveyAnswer> Answers => _answers;

    /// <summary>Участвует в сессии (не удалён).</summary>
    public bool IsActive => Status != ParticipantStatus.Removed;

    internal void MarkOpened(DateTime nowUtc)
    {
        FirstOpenedAtUtc ??= nowUtc;
        if (Status == ParticipantStatus.Pending)
        {
            Status = ParticipantStatus.InProgress;
        }
    }

    internal void SaveAnswers(IEnumerable<AnswerInput> inputs, DateTime nowUtc)
    {
        foreach (var input in inputs)
        {
            var answer = _answers.SingleOrDefault(a => a.SessionIndicatorId == input.IndicatorId);
            if (answer is null)
            {
                answer = new SurveyAnswer(Id, input.IndicatorId);
                _answers.Add(answer);
            }

            answer.Set(input, nowUtc);
        }

        MarkOpened(nowUtc);
    }

    internal void MarkSubmitted(DateTime nowUtc)
    {
        Status = ParticipantStatus.Submitted;
        SubmittedAtUtc = nowUtc;
    }

    internal AccessToken IssueToken(DateTime nowUtc)
    {
        var token = AccessToken.Create();
        TokenHash = token.Hash;
        TokenIssuedAtUtc = nowUtc;
        return token;
    }

    internal AccessToken IssueReminderToken(DateTime nowUtc)
    {
        LastReminderAtUtc = nowUtc;
        return IssueToken(nowUtc);
    }

    internal void Remove()
    {
        Status = ParticipantStatus.Removed;
        TokenHash = null;
    }
}
