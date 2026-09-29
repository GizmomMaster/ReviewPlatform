namespace ReviewPlatform.Domain.Assessments;

public enum SessionType
{
    /// <summary>Индикаторы текущего и следующего грейда.</summary>
    Transition,

    /// <summary>Только индикаторы текущего грейда.</summary>
    Confirmation,
}

public enum SessionStatus
{
    Draft,
    InProgress,
    Overdue,
    AwaitingDecision,
    Closed,
    Cancelled,
}

public enum ParticipantStatus
{
    Pending,
    InProgress,
    Submitted,
    Removed,
}

public enum LevelKind
{
    Current,
    Target,
}
