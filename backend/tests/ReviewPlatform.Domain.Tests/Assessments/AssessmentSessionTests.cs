using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Assessments;

public sealed class AssessmentSessionTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid GradeId = Guid.NewGuid();

    private static readonly GradeRoleRule[] Rules =
    [
        new(GradeId, EvaluatorRole.Self, 1, 1),
        new(GradeId, EvaluatorRole.Peer, 1, 2),
        new(GradeId, EvaluatorRole.TeamLead, 1, 1),
        new(GradeId, EvaluatorRole.Manager, 1, 1),
    ];

    private static readonly IndicatorSnapshot[] Indicators =
    [
        new(Guid.NewGuid(), "Коммуникация", 1, "E3", LevelKind.Current, "Аргументирует", 1),
        new(Guid.NewGuid(), "Коммуникация", 1, "E4", LevelKind.Target, "Фасилитирует", 1),
    ];

    private static AssessmentSession NewDraft()
    {
        var session = new AssessmentSession(Guid.NewGuid(), Guid.NewGuid(), GradeId, Guid.NewGuid(),
            new SessionPlan(SessionType.Transition, Guid.NewGuid()), Now.AddDays(14), Now);
        session.AddParticipant("Сам Себе", "self@x.ru", EvaluatorRole.Self, Rules, Now);
        return session;
    }

    private static AssessmentSession ReadyDraft()
    {
        var session = NewDraft();
        session.AddParticipant("Коллега", "peer@x.ru", EvaluatorRole.Peer, Rules, Now);
        session.AddParticipant("Лид", "lead@x.ru", EvaluatorRole.TeamLead, Rules, Now);
        session.AddParticipant("Менеджер", "manager@x.ru", EvaluatorRole.Manager, Rules, Now);
        return session;
    }

    [Fact]
    public void Create_DeadlineInPast_Throws() =>
        Assert.Throws<DomainException>(() => new AssessmentSession(Guid.NewGuid(), Guid.NewGuid(), GradeId, Guid.NewGuid(),
            new SessionPlan(SessionType.Confirmation, null), Now.AddMinutes(-1), Now));

    [Fact]
    public void AddParticipant_InDraft_DoesNotIssueToken()
    {
        var session = NewDraft();

        var (participant, token) = session.AddParticipant("Коллега", "Peer@X.ru ", EvaluatorRole.Peer, Rules, Now);

        Assert.Null(token);
        Assert.Null(participant.TokenHash);
        Assert.Equal("peer@x.ru", participant.Email);
    }

    [Fact]
    public void AddParticipant_DuplicateEmail_Throws()
    {
        var session = NewDraft();

        Assert.Throws<DomainException>(() => session.AddParticipant("Двойник", "SELF@x.ru", EvaluatorRole.Peer, Rules, Now));
    }

    [Fact]
    public void AddParticipant_OverRoleMaximum_Throws()
    {
        var session = NewDraft();
        session.AddParticipant("Коллега 1", "p1@x.ru", EvaluatorRole.Peer, Rules, Now);
        session.AddParticipant("Коллега 2", "p2@x.ru", EvaluatorRole.Peer, Rules, Now);

        Assert.Throws<DomainException>(() => session.AddParticipant("Коллега 3", "p3@x.ru", EvaluatorRole.Peer, Rules, Now));
    }

    [Fact]
    public void AddParticipant_RoleNotAllowedForGrade_Throws() =>
        Assert.Throws<DomainException>(() => NewDraft().AddParticipant("РЦК", "rck@x.ru", EvaluatorRole.Rck, Rules, Now));

    [Fact]
    public void Launch_MissingRequiredRoles_ThrowsWithDetails()
    {
        var session = NewDraft();

        var error = Assert.Throws<DomainException>(() => session.Launch(Rules, Indicators, Now));

        Assert.Contains("Peer", error.Message, StringComparison.Ordinal);
        Assert.Equal(SessionStatus.Draft, session.Status);
    }

    [Fact]
    public void Launch_Valid_SnapshotsIndicatorsAndIssuesUniqueTokens()
    {
        var session = ReadyDraft();

        var tokens = session.Launch(Rules, Indicators, Now);

        Assert.Equal(SessionStatus.InProgress, session.Status);
        Assert.Equal(Now, session.LaunchedAtUtc);
        Assert.Equal(2, session.Indicators.Count);
        Assert.Equal(4, tokens.Count);
        Assert.Equal(4, tokens.Values.Select(t => t.Value).Distinct().Count());
        Assert.All(session.Participants, p => Assert.Equal(tokens[p.Id].Hash, p.TokenHash));
        Assert.All(tokens.Values, t => Assert.Equal(AccessToken.HashOf(t.Value), t.Hash));
    }

    [Fact]
    public void Launch_Twice_Throws()
    {
        var session = ReadyDraft();
        session.Launch(Rules, Indicators, Now);

        Assert.Throws<DomainException>(() => session.Launch(Rules, Indicators, Now));
    }

    [Fact]
    public void Launch_AfterDeadline_Throws()
    {
        var session = ReadyDraft();

        Assert.Throws<DomainException>(() => session.Launch(Rules, Indicators, Now.AddDays(15)));
    }

    [Fact]
    public void AddParticipant_AfterLaunch_IssuesToken()
    {
        var session = ReadyDraft();
        session.Launch(Rules, Indicators, Now);

        var (participant, token) = session.AddParticipant("Коллега 2", "p2@x.ru", EvaluatorRole.Peer, Rules, Now);

        Assert.NotNull(token);
        Assert.Equal(token.Value.Hash, participant.TokenHash);
    }

    [Fact]
    public void RemoveParticipant_Self_Throws()
    {
        var session = NewDraft();
        var self = session.Participants.Single();

        Assert.Throws<DomainException>(() => session.RemoveParticipant(self.Id));
    }

    [Fact]
    public void RemoveParticipant_InDraft_DeletesIt_AfterLaunch_MarksRemoved()
    {
        var draft = ReadyDraft();
        var peer = draft.Participants.Single(p => p.Role == EvaluatorRole.Peer);
        draft.RemoveParticipant(peer.Id);
        Assert.DoesNotContain(peer, draft.Participants);

        var launched = ReadyDraft();
        launched.AddParticipant("Коллега 2", "p2@x.ru", EvaluatorRole.Peer, Rules, Now);
        launched.Launch(Rules, Indicators, Now);
        var launchedPeer = launched.Participants.First(p => p.Role == EvaluatorRole.Peer);
        launched.RemoveParticipant(launchedPeer.Id);
        Assert.Equal(ParticipantStatus.Removed, launchedPeer.Status);
        Assert.Null(launchedPeer.TokenHash);
    }

    [Fact]
    public void ReissueToken_ChangesHash()
    {
        var session = ReadyDraft();
        var tokens = session.Launch(Rules, Indicators, Now);
        var peer = session.Participants.Single(p => p.Role == EvaluatorRole.Peer);

        var token = session.ReissueToken(peer.Id, Now.AddHours(1));

        Assert.NotEqual(tokens[peer.Id].Hash, token.Hash);
        Assert.Equal(token.Hash, peer.TokenHash);
    }

    [Fact]
    public void ReissueToken_InDraft_Throws()
    {
        var session = ReadyDraft();

        Assert.Throws<DomainException>(() => session.ReissueToken(session.Participants.First().Id, Now));
    }

    [Fact]
    public void Cancel_FromInProgress_ThenNothingElseAllowed()
    {
        var session = ReadyDraft();
        session.Launch(Rules, Indicators, Now);

        session.Cancel(Now);

        Assert.Equal(SessionStatus.Cancelled, session.Status);
        Assert.Throws<DomainException>(() => session.Cancel(Now));
        Assert.Throws<DomainException>(() => session.AddParticipant("X", "x@x.ru", EvaluatorRole.Peer, Rules, Now));
    }

    [Fact]
    public void Reschedule_AfterLaunch_Throws()
    {
        var session = ReadyDraft();
        session.Launch(Rules, Indicators, Now);

        Assert.Throws<DomainException>(() => session.Reschedule(new SessionPlan(SessionType.Confirmation, null), Now.AddDays(30), Now));
    }

    [Fact]
    public void SessionPlan_TransitionForLastGrade_Throws() =>
        Assert.Throws<DomainException>(() => SessionPlan.For(SessionType.Transition, nextGrade: null));

    [Fact]
    public void RoleRequirements_ReportsUnexpectedRoleAsUnsatisfied()
    {
        var session = ReadyDraft();
        var withoutPeerRule = Rules.Where(r => r.Role != EvaluatorRole.Peer).ToList();

        var requirements = RoleRequirements.Evaluate(withoutPeerRule, session.Participants);

        var peer = Assert.Single(requirements, r => r.Role == EvaluatorRole.Peer);
        Assert.False(peer.IsSatisfied);
    }
}
