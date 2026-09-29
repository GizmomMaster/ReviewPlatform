using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Assessments.Reporting;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Assessments;

public sealed class ReportCalculatorTests
{
    private static readonly ReportPolicy Policy = new();
    private static readonly Guid Self = Guid.NewGuid();
    private static readonly Guid Peer1 = Guid.NewGuid();
    private static readonly Guid Peer2 = Guid.NewGuid();
    private static readonly Guid Lead = Guid.NewGuid();

    private static SessionIndicator Indicator(string group, int groupOrder, LevelKind level, int order = 1) =>
        new(Guid.NewGuid(), new IndicatorSnapshot(Guid.NewGuid(), group, groupOrder, level == LevelKind.Current ? "E3" : "E4", level, $"{group} {level} {order}", order));

    private static Rating R(Guid rater, EvaluatorRole role, int? score, bool na = false) => new(rater, role, score, na);

    private static Rating[] Ratings(int? self, params int?[] others)
    {
        var raters = new[] { (Peer1, EvaluatorRole.Peer), (Peer2, EvaluatorRole.Peer), (Lead, EvaluatorRole.TeamLead) };
        return [R(Self, EvaluatorRole.Self, self), .. others.Select((s, i) => R(raters[i].Item1, raters[i].Item2, s))];
    }

    private static IndicatorResult Single(IReadOnlyList<Rating> ratings)
    {
        var indicator = Indicator("Коммуникация", 1, LevelKind.Current);
        var report = ReportCalculator.Calculate([indicator], new Dictionary<Guid, IReadOnlyList<Rating>> { [indicator.Id] = ratings }, false, Policy);
        return report.Indicators.Single();
    }

    [Fact]
    public void Score_IsMeanOfOthers_SelfExcluded_AllRolesEqual()
    {
        var result = Single(Ratings(self: 0, 3, 2, 1));

        Assert.Equal(2.0, result.Score);
        Assert.Equal(3, result.RatingsCount);
        Assert.Equal(0, result.SelfScore);
        Assert.True(result.IsMet);
    }

    [Fact]
    public void NotApplicable_IsExcluded()
    {
        var result = Single([R(Self, EvaluatorRole.Self, null, na: true), R(Peer1, EvaluatorRole.Peer, 3), R(Peer2, EvaluatorRole.Peer, null, na: true), R(Lead, EvaluatorRole.TeamLead, 1)]);

        Assert.Equal(2.0, result.Score);
        Assert.Equal(2, result.RatingsCount);
        Assert.Null(result.SelfScore);
    }

    [Fact]
    public void BelowQuorum_IsInsufficient_AndNotMet()
    {
        var result = Single(Ratings(self: 3, 3));

        Assert.True(result.InsufficientData);
        Assert.Null(result.IsMet);
        Assert.Equal(BlindSpot.None, result.BlindSpot); // без кворума слепое пятно не считаем
    }

    [Theory]
    [InlineData(2, 1, false)] // 1.5 < 2.0
    [InlineData(2, 2, true)]
    public void IsMet_UsesThreshold(int a, int b, bool expected) =>
        Assert.Equal(expected, Single(Ratings(null, a, b)).IsMet);

    [Fact]
    public void Disputed_WhenSpreadAtLeastTwo()
    {
        Assert.True(Single(Ratings(null, 0, 2, 3)).IsDisputed);
        Assert.False(Single(Ratings(null, 1, 2, 2)).IsDisputed);
    }

    [Fact]
    public void BlindSpot_DirectionAndGap()
    {
        Assert.Equal(BlindSpot.Overestimated, Single(Ratings(self: 3, 1, 2)).BlindSpot);  // 3 − 1.5 = 1.5
        Assert.Equal(BlindSpot.Underestimated, Single(Ratings(self: 0, 2, 2)).BlindSpot); // 0 − 2 = −2
        Assert.Equal(BlindSpot.None, Single(Ratings(self: 3, 2, 2)).BlindSpot);           // разница 1
    }

    [Fact]
    public void Summary_ConfirmationAndReadiness_IgnoreInsufficientIndicators()
    {
        var c1 = Indicator("Экспертность", 1, LevelKind.Current, 1);
        var c2 = Indicator("Экспертность", 1, LevelKind.Current, 2);
        var c3 = Indicator("Коммуникация", 2, LevelKind.Current, 1);
        var t1 = Indicator("Экспертность", 1, LevelKind.Target, 1);
        var t2 = Indicator("Коммуникация", 2, LevelKind.Target, 1);
        var ratings = new Dictionary<Guid, IReadOnlyList<Rating>>
        {
            [c1.Id] = Ratings(2, 3, 3),
            [c2.Id] = Ratings(2, 1, 1),
            [c3.Id] = Ratings(2, 2),     // нет кворума
            [t1.Id] = Ratings(1, 2, 2),
            [t2.Id] = Ratings(1, 1, 2),
        };

        var report = ReportCalculator.Calculate([c1, c2, c3, t1, t2], ratings, hasTargetLevel: true, Policy);

        Assert.Equal(0.5, report.CurrentConfirmation);  // 1 из 2 учитываемых
        Assert.Equal(0.5, report.TargetReadiness);      // 1 из 2
        Assert.Equal(1, report.InsufficientCount);

        var expertise = report.Groups[0];
        Assert.Equal("Экспертность", expertise.Name);
        Assert.Equal(2.0, expertise.Current.Score);     // (3 + 1) / 2
        Assert.Equal(2.0, expertise.Current.Self);
        Assert.Equal(0.5, expertise.Current.MetShare);
        Assert.Equal(1.0, expertise.Target!.MetShare);

        var communication = report.Groups[1];
        Assert.Null(communication.Current.Score);       // единственный индикатор без кворума
        Assert.Equal((0, 1), (communication.Current.Included, communication.Current.Total));
    }

    [Fact]
    public void Confirmation_HasNoTargetStats()
    {
        var c = Indicator("Экспертность", 1, LevelKind.Current);

        var report = ReportCalculator.Calculate([c], new Dictionary<Guid, IReadOnlyList<Rating>> { [c.Id] = Ratings(2, 2, 2) }, hasTargetLevel: false, Policy);

        Assert.Null(report.TargetReadiness);
        Assert.Null(report.Groups.Single().Target);
        Assert.Equal(1.0, report.CurrentConfirmation);
    }

    [Fact]
    public void NoRatingsYet_EverythingInsufficient()
    {
        var c = Indicator("Экспертность", 1, LevelKind.Current);

        var report = ReportCalculator.Calculate([c], new Dictionary<Guid, IReadOnlyList<Rating>>(), false, Policy);

        Assert.Null(report.CurrentConfirmation);
        Assert.Equal(1, report.InsufficientCount);
        Assert.Null(report.Indicators.Single().Score);
    }
}
