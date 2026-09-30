using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Assessments;

public sealed class SurveyTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid GradeId = Guid.NewGuid();

    private static readonly GradeRoleRule[] Rules =
    [
        new(GradeId, EvaluatorRole.Self, 1, 1),
        new(GradeId, EvaluatorRole.Peer, 1, 2),
    ];

    private static (AssessmentSession Session, Participant Self, Participant Peer, Guid[] Indicators) Launched()
    {
        var session = new AssessmentSession(Guid.NewGuid(), Guid.NewGuid(), GradeId, Guid.NewGuid(),
            new SessionPlan(SessionType.Confirmation, null), Now.AddDays(7), Now);
        var (self, _) = session.AddParticipant("Я", "self@x.ru", EvaluatorRole.Self, Rules, Now);
        var (peer, _) = session.AddParticipant("Коллега", "peer@x.ru", EvaluatorRole.Peer, Rules, Now);
        session.Launch(Rules,
        [
            new(Guid.NewGuid(), "Коммуникация", 1, "E3", LevelKind.Current, "Аргументирует", 1),
            new(Guid.NewGuid(), "Коммуникация", 1, "E3", LevelKind.Current, "Слушает", 2),
        ], Now);
        return (session, self, peer, [.. session.Indicators.Select(i => i.Id)]);
    }

    private static AnswerInput[] AllGood(Guid[] ids) => [.. ids.Select(id => new AnswerInput(id, 2, false, null))];

    [Fact]
    public void OpenSurvey_MarksParticipantInProgressOnce()
    {
        var (session, self, _, _) = Launched();

        session.OpenSurvey(self, Now.AddHours(1));
        session.OpenSurvey(self, Now.AddHours(2));

        Assert.Equal(ParticipantStatus.InProgress, self.Status);
        Assert.Equal(Now.AddHours(1), self.FirstOpenedAtUtc);
    }

    [Fact]
    public void SaveDraft_UpsertsAnswers()
    {
        var (session, self, _, ids) = Launched();

        session.SaveDraft(self, [new(ids[0], 1, false, "черновик")], Now);
        session.SaveDraft(self, [new(ids[0], null, true, null), new(ids[1], 3, false, "пример")], Now);

        Assert.Equal(2, self.Answers.Count);
        var first = self.Answers.Single(a => a.SessionIndicatorId == ids[0]);
        Assert.Equal((null, true, null), (first.Score, first.NotApplicable, first.Comment));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public void SaveDraft_InvalidAnswer_Throws(int score, bool notApplicable)
    {
        var (session, self, _, ids) = Launched();

        Assert.Throws<DomainException>(() => session.SaveDraft(self, [new(ids[0], score, notApplicable, null)], Now));
    }

    [Fact]
    public void SaveDraft_ForeignIndicator_Throws()
    {
        var (session, self, _, _) = Launched();

        Assert.Throws<DomainException>(() => session.SaveDraft(self, [new(Guid.NewGuid(), 1, false, null)], Now));
    }

    [Fact]
    public void Submit_Incomplete_ReturnsErrorsAndKeepsParticipantOpen()
    {
        var (session, self, _, ids) = Launched();

        var errors = session.Submit(self, [new(ids[0], 3, false, "  ")], Now);

        Assert.Equal(2, errors.Count); // нет комментария к 3 и нет ответа на второй индикатор
        Assert.Contains(errors, e => e.IndicatorId == ids[0]);
        Assert.Contains(errors, e => e.IndicatorId == ids[1]);
        Assert.NotEqual(ParticipantStatus.Submitted, self.Status);
    }

    [Fact]
    public void Submit_NotApplicableCountsAsAnswered()
    {
        var (session, self, _, ids) = Launched();

        var errors = session.Submit(self, [new(ids[0], null, true, null), new(ids[1], 0, false, "был случай")], Now);

        Assert.Empty(errors);
        Assert.Equal(ParticipantStatus.Submitted, self.Status);
    }

    [Fact]
    public void Submit_LastParticipant_MovesSessionToAwaitingDecision()
    {
        var (session, self, peer, ids) = Launched();

        session.Submit(self, AllGood(ids), Now);
        Assert.Equal(SessionStatus.InProgress, session.Status);

        session.Submit(peer, AllGood(ids), Now.AddMinutes(5));

        Assert.Equal(SessionStatus.AwaitingDecision, session.Status);
        Assert.Equal(Now.AddMinutes(5), session.CompletedAtUtc);
    }

    [Fact]
    public void Submit_RemovedParticipantsAreIgnoredForCompletion()
    {
        var (session, self, peer, ids) = Launched();
        var (extraPeer, _) = session.AddParticipant("Второй", "peer2@x.ru", EvaluatorRole.Peer, Rules, Now);
        session.RemoveParticipant(extraPeer.Id);

        session.Submit(self, AllGood(ids), Now);
        session.Submit(peer, AllGood(ids), Now);

        Assert.Equal(SessionStatus.AwaitingDecision, session.Status);
    }

    [Fact]
    public void Submit_Twice_Throws()
    {
        var (session, self, _, ids) = Launched();
        session.Submit(self, AllGood(ids), Now);

        Assert.Throws<DomainException>(() => session.Submit(self, AllGood(ids), Now));
        Assert.Throws<DomainException>(() => session.SaveDraft(self, AllGood(ids), Now));
    }

    [Fact]
    public void Answers_AfterDeadline_Rejected()
    {
        var (session, self, _, ids) = Launched();

        Assert.False(session.AcceptsAnswers(Now.AddDays(8)));
        Assert.Throws<DomainException>(() => session.SaveDraft(self, AllGood(ids), Now.AddDays(8)));
    }

    [Fact]
    public void Answers_AfterCancel_Rejected()
    {
        var (session, self, _, ids) = Launched();
        session.Cancel(Now);

        Assert.Throws<DomainException>(() => session.Submit(self, AllGood(ids), Now));
    }
}
