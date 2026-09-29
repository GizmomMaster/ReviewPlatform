using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Assessments;

public sealed class DecisionTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CurrentGrade = Guid.NewGuid();
    private static readonly Guid TargetGrade = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private static readonly GradeRoleRule[] Rules =
    [
        new(CurrentGrade, EvaluatorRole.Self, 1, 1),
        new(CurrentGrade, EvaluatorRole.Peer, 1, 2),
    ];

    private static (AssessmentSession Session, Participant Self, Participant Peer) Launched()
    {
        var session = new AssessmentSession(Guid.NewGuid(), Guid.NewGuid(), CurrentGrade, Manager,
            new SessionPlan(SessionType.Transition, TargetGrade), Now.AddDays(7), Now);
        var (self, _) = session.AddParticipant("Я", "self@x.ru", EvaluatorRole.Self, Rules, Now);
        var (peer, _) = session.AddParticipant("Коллега", "peer@x.ru", EvaluatorRole.Peer, Rules, Now);
        session.Launch(Rules, [new(Guid.NewGuid(), "Коммуникация", 1, "E4", LevelKind.Target, "Фасилитирует", 1)], Now);
        return (session, self, peer);
    }

    private static AnswerInput[] Answers(AssessmentSession session) => [.. session.Indicators.Select(i => new AnswerInput(i.Id, 2, false, null))];

    private static AssessmentSession AwaitingDecision()
    {
        var (session, self, peer) = Launched();
        session.Submit(self, Answers(session), Now);
        session.Submit(peer, Answers(session), Now);
        return session;
    }

    [Fact]
    public void CloseEarly_OnlySelfSubmitted_Throws()
    {
        var (session, self, _) = Launched();
        session.Submit(self, Answers(session), Now);

        Assert.Throws<DomainException>(() => session.CloseEarly(Now));
    }

    [Fact]
    public void CloseEarly_WithPeerSubmitted_MovesToAwaitingDecision_AndStopsAnswers()
    {
        var (session, self, peer) = Launched();
        session.Submit(peer, Answers(session), Now);

        session.CloseEarly(Now.AddHours(1));

        Assert.Equal(SessionStatus.AwaitingDecision, session.Status);
        Assert.Equal(Now.AddHours(1), session.CompletedAtUtc);
        Assert.Throws<DomainException>(() => session.SaveDraft(self, Answers(session), Now.AddHours(2)));
    }

    [Fact]
    public void Decide_Promoted_ClosesSessionWithPlan()
    {
        var session = AwaitingDecision();
        var indicatorId = session.Indicators.Single().Id;

        var decision = session.Decide(Manager, DecisionOutcome.Promoted, TargetGrade, "  Готов к E5  ",
            [new("Вести фичу целиком", indicatorId, new DateOnly(2027, 3, 1)), new("Выступить на митапе", null, null)], Now);

        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(Now, session.ClosedAtUtc);
        Assert.Same(decision, session.Decision);
        Assert.Equal("Готов к E5", decision.Comment);
        Assert.Equal([1, 2], decision.PlanItems.Select(p => p.Order));
        Assert.Equal(indicatorId, decision.PlanItems.First().SessionIndicatorId);
    }

    [Fact]
    public void Decide_BeforeSurveyCompleted_Throws()
    {
        var (session, _, _) = Launched();

        Assert.Throws<DomainException>(() => session.Decide(Manager, DecisionOutcome.GradeConfirmed, CurrentGrade, "ок", [], Now));
    }

    [Fact]
    public void Decide_Twice_Throws()
    {
        var session = AwaitingDecision();
        session.Decide(Manager, DecisionOutcome.GradeConfirmed, CurrentGrade, "ок", [], Now);

        Assert.Throws<DomainException>(() => session.Decide(Manager, DecisionOutcome.GradeConfirmed, CurrentGrade, "ок", [], Now));
    }

    [Fact]
    public void Decide_InconsistentGrade_Throws()
    {
        Assert.Throws<DomainException>(() => AwaitingDecision().Decide(Manager, DecisionOutcome.GradeConfirmed, TargetGrade, "ок", [], Now));
        Assert.Throws<DomainException>(() => AwaitingDecision().Decide(Manager, DecisionOutcome.Promoted, CurrentGrade, "ок", [], Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_WithoutComment_Throws(string comment) =>
        Assert.Throws<DomainException>(() => AwaitingDecision().Decide(Manager, DecisionOutcome.GradeConfirmed, CurrentGrade, comment, [], Now));

    [Fact]
    public void Decide_PlanItemWithForeignIndicator_Throws() =>
        Assert.Throws<DomainException>(() => AwaitingDecision().Decide(Manager, DecisionOutcome.NotConfirmed, CurrentGrade, "нет",
            [new("Пункт", Guid.NewGuid(), null)], Now));

    [Fact]
    public void Decide_EmptyPlanItem_Throws() =>
        Assert.Throws<DomainException>(() => AwaitingDecision().Decide(Manager, DecisionOutcome.NotConfirmed, CurrentGrade, "нет",
            [new(" ", null, null)], Now));
}
