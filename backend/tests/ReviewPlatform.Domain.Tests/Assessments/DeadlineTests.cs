using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Assessments;

public sealed class DeadlineTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Deadline = Now.AddDays(14);
    private static readonly int[] ReminderDays = [3, 1];
    private static readonly Guid GradeId = Guid.NewGuid();

    private static readonly GradeRoleRule[] Rules =
    [
        new(GradeId, EvaluatorRole.Self, 1, 1),
        new(GradeId, EvaluatorRole.Peer, 1, 2),
    ];

    private static readonly IndicatorSnapshot[] Indicators =
        [new(Guid.NewGuid(), "Коммуникация", 1, "E3", LevelKind.Current, "Аргументирует", 1)];

    private static AssessmentSession Launched()
    {
        var session = new AssessmentSession(Guid.NewGuid(), Guid.NewGuid(), GradeId, Guid.NewGuid(),
            new SessionPlan(SessionType.Confirmation, null), Deadline, Now);
        session.AddParticipant("Сам Себе", "self@x.ru", EvaluatorRole.Self, Rules, Now);
        session.AddParticipant("Коллега", "peer@x.ru", EvaluatorRole.Peer, Rules, Now);
        session.Launch(Rules, Indicators, Now);
        return session;
    }

    private static void Submit(AssessmentSession session, EvaluatorRole role, DateTime at) =>
        Assert.Empty(session.Submit(session.Participants.Single(p => p.Role == role),
            [new AnswerInput(session.Indicators.Single().Id, 2, false, null)], at));

    // ---------- Просрочка ----------

    [Fact]
    public void MarkOverdue_BeforeDeadline_DoesNothing()
    {
        var session = Launched();

        Assert.False(session.MarkOverdue(Deadline));
        Assert.Equal(SessionStatus.InProgress, session.Status);
    }

    [Fact]
    public void MarkOverdue_AfterDeadline_ChangesStatusOnce()
    {
        var session = Launched();

        Assert.True(session.MarkOverdue(Deadline.AddSeconds(1)));
        Assert.False(session.MarkOverdue(Deadline.AddHours(1)));
        Assert.Equal(SessionStatus.Overdue, session.Status);
        Assert.False(session.AcceptsAnswers(Deadline.AddHours(1)));
    }

    [Fact]
    public void MarkOverdue_CompletedSession_DoesNothing()
    {
        var session = Launched();
        Submit(session, EvaluatorRole.Self, Now);
        Submit(session, EvaluatorRole.Peer, Now);

        Assert.False(session.MarkOverdue(Deadline.AddDays(1)));
        Assert.Equal(SessionStatus.AwaitingDecision, session.Status);
    }

    // ---------- Продление ----------

    [Fact]
    public void Extend_Overdue_ReopensAndIssuesTokensOnlyToPending()
    {
        var session = Launched();
        Submit(session, EvaluatorRole.Self, Now);
        var peer = session.Participants.Single(p => p.Role == EvaluatorRole.Peer);
        var oldHash = peer.TokenHash;
        var later = Deadline.AddDays(1);
        session.MarkOverdue(later);

        var tokens = session.Extend(later.AddDays(7), later);

        Assert.Equal(SessionStatus.InProgress, session.Status);
        Assert.Equal(later.AddDays(7), session.DeadlineAtUtc);
        Assert.Equal([peer.Id], tokens.Keys);
        Assert.NotEqual(oldHash, peer.TokenHash);
        Assert.Equal(tokens[peer.Id].Hash, peer.TokenHash);
        Assert.True(session.AcceptsAnswers(later));
    }

    [Fact]
    public void Extend_DeadlineInPast_Throws()
    {
        var session = Launched();

        Assert.Throws<DomainException>(() => session.Extend(Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void Extend_AwaitingDecision_Throws()
    {
        var session = Launched();
        Submit(session, EvaluatorRole.Self, Now);
        Submit(session, EvaluatorRole.Peer, Now);

        Assert.Throws<DomainException>(() => session.Extend(Deadline.AddDays(7), Now));
    }

    [Fact]
    public void CloseEarly_FromOverdue_MovesToAwaitingDecision()
    {
        var session = Launched();
        Submit(session, EvaluatorRole.Peer, Now);
        session.MarkOverdue(Deadline.AddDays(1));

        session.CloseEarly(Deadline.AddDays(1));

        Assert.Equal(SessionStatus.AwaitingDecision, session.Status);
    }

    // ---------- Напоминания ----------

    [Fact]
    public void Remind_BeforeFirstPoint_SendsNothing()
    {
        var session = Launched();

        Assert.Empty(session.Remind(ReminderDays, Deadline.AddDays(-3).AddMinutes(-1)));
    }

    [Fact]
    public void Remind_AtPoint_RemindsPendingOnce_AndReissuesToken()
    {
        var session = Launched();
        Submit(session, EvaluatorRole.Self, Now);
        var peer = session.Participants.Single(p => p.Role == EvaluatorRole.Peer);
        var at = Deadline.AddDays(-3);

        var tokens = session.Remind(ReminderDays, at);

        Assert.Equal([peer.Id], tokens.Keys);
        Assert.Equal(tokens[peer.Id].Hash, peer.TokenHash);
        Assert.Equal(at, peer.LastReminderAtUtc);
        Assert.Empty(session.Remind(ReminderDays, at.AddMinutes(1)));
        Assert.Empty(session.Remind(ReminderDays, Deadline.AddDays(-1).AddMinutes(-1)));
    }

    [Fact]
    public void Remind_EachPointOnce()
    {
        var session = Launched();

        Assert.Equal(2, session.Remind(ReminderDays, Deadline.AddDays(-3)).Count);
        Assert.Equal(2, session.Remind(ReminderDays, Deadline.AddDays(-1)).Count);
        Assert.Empty(session.Remind(ReminderDays, Deadline.AddHours(-1)));
    }

    [Fact]
    public void Remind_ScheduleMissedByDowntime_SendsSingleReminder()
    {
        var session = Launched();

        Assert.Equal(2, session.Remind(ReminderDays, Deadline.AddHours(-12)).Count);
        Assert.Empty(session.Remind(ReminderDays, Deadline.AddHours(-11)));
    }

    [Fact]
    public void Remind_AfterDeadlineOrWhenOverdue_SendsNothing()
    {
        var session = Launched();

        Assert.Empty(session.Remind(ReminderDays, Deadline.AddMinutes(1)));
        session.MarkOverdue(Deadline.AddMinutes(1));
        Assert.Empty(session.Remind(ReminderDays, Deadline.AddMinutes(2)));
    }

    [Fact]
    public void Remind_SkipsRemovedAndSubmitted()
    {
        var session = Launched();
        var peer = session.Participants.Single(p => p.Role == EvaluatorRole.Peer);
        session.RemoveParticipant(peer.Id);
        Submit(session, EvaluatorRole.Self, Now);

        Assert.Equal(SessionStatus.AwaitingDecision, session.Status);
        Assert.Empty(session.Remind(ReminderDays, Deadline.AddDays(-1)));
    }

    [Theory]
    [InlineData(-4.0, null, false)]    // ещё рано
    [InlineData(-3.0, -14.0, true)]    // точка D-3, ссылка выдана при запуске
    [InlineData(-2.5, -3.2, false)]    // ссылку выдали меньше суток назад — ждём
    [InlineData(-2.0, -3.2, true)]     // сутки прошли — напоминаем за D-3
    [InlineData(-2.0, -2.9, false)]    // ссылка выдана уже после точки D-3
    [InlineData(-1.0, -2.9, true)]     // точка D-1
    [InlineData(0.5, -14.0, false)]    // дедлайн прошёл
    public void ReminderSchedule_IsDue(double nowOffsetDays, double? lastLinkOffsetDays, bool expected)
    {
        DateTime? lastLink = lastLinkOffsetDays is { } offset ? Deadline.AddDays(offset) : Deadline.AddDays(-14);

        Assert.Equal(expected, ReminderSchedule.IsDue(Deadline, ReminderDays, lastLink, Deadline.AddDays(nowOffsetDays)));
    }

    [Fact]
    public void ReminderSchedule_NoLinkIssued_IsNotDue() =>
        Assert.False(ReminderSchedule.IsDue(Deadline, ReminderDays, null, Deadline.AddDays(-1)));
}
