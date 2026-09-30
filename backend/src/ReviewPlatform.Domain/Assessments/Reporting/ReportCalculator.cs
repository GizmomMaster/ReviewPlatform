using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Assessments.Reporting;

public enum BlindSpot
{
    None,

    /// <summary>Сотрудник оценивает себя выше, чем окружение.</summary>
    Overestimated,

    /// <summary>Сотрудник оценивает себя ниже, чем окружение.</summary>
    Underestimated,
}

public sealed record Rating(Guid RaterId, EvaluatorRole Role, int? Score, bool NotApplicable);

public sealed record IndicatorResult(
    Guid IndicatorId,
    double? Score,
    int RatingsCount,
    bool InsufficientData,
    bool? IsMet,
    int? SelfScore,
    bool IsDisputed,
    BlindSpot BlindSpot);

public sealed record LevelStats(double? Score, double? Self, double? MetShare, int Included, int Total);

public sealed record GroupResult(string Name, int Order, LevelStats Current, LevelStats? Target);

public sealed record ReportResult(
    double? CurrentConfirmation,
    double? TargetReadiness,
    int InsufficientCount,
    IReadOnlyList<GroupResult> Groups,
    IReadOnlyList<IndicatorResult> Indicators);

/// <summary>Расчёт отчёта по сессии (ТЗ, 6.2). Чистая функция: на входе — только отправленные анкеты.</summary>
public static class ReportCalculator
{
    /// <param name="ratings">Оценки из отправленных анкет, по Id индикатора.</param>
    public static ReportResult Calculate(
        IReadOnlyCollection<SessionIndicator> indicators,
        IReadOnlyDictionary<Guid, IReadOnlyList<Rating>> ratings,
        bool hasTargetLevel,
        ReportPolicy policy)
    {
        var results = indicators.Select(i => Evaluate(i, ratings.GetValueOrDefault(i.Id) ?? [], policy)).ToList();
        var byId = results.ToDictionary(r => r.IndicatorId);

        var groups = indicators
            .GroupBy(i => (i.GroupOrder, i.GroupName))
            .OrderBy(g => g.Key.GroupOrder)
            .Select(g => new GroupResult(
                g.Key.GroupName,
                g.Key.GroupOrder,
                Stats(g.Where(i => i.LevelKind == LevelKind.Current), byId),
                hasTargetLevel ? Stats(g.Where(i => i.LevelKind == LevelKind.Target), byId) : null))
            .ToList();

        return new ReportResult(
            MetShare(indicators.Where(i => i.LevelKind == LevelKind.Current), byId),
            hasTargetLevel ? MetShare(indicators.Where(i => i.LevelKind == LevelKind.Target), byId) : null,
            results.Count(r => r.InsufficientData),
            groups,
            results);
    }

    private static IndicatorResult Evaluate(SessionIndicator indicator, IReadOnlyList<Rating> ratings, ReportPolicy policy)
    {
        var others = ratings.Where(r => r.Role != EvaluatorRole.Self && !r.NotApplicable && r.Score is not null).Select(r => r.Score!.Value).ToList();
        var self = ratings.FirstOrDefault(r => r.Role == EvaluatorRole.Self && !r.NotApplicable)?.Score;

        var insufficient = others.Count < policy.Quorum;
        double? score = others.Count > 0 ? others.Average() : null;
        var disputed = others.Count >= 2 && others.Max() - others.Min() >= policy.DisagreementSpread;

        var blindSpot = BlindSpot.None;
        if (!insufficient && self is { } s && score is { } o && Math.Abs(s - o) >= policy.BlindSpotGap)
        {
            blindSpot = s > o ? BlindSpot.Overestimated : BlindSpot.Underestimated;
        }

        return new IndicatorResult(
            indicator.Id,
            score,
            others.Count,
            insufficient,
            insufficient ? null : score >= policy.MetThreshold,
            self,
            disputed,
            blindSpot);
    }

    private static LevelStats Stats(IEnumerable<SessionIndicator> indicators, Dictionary<Guid, IndicatorResult> results)
    {
        var level = indicators.Select(i => results[i.Id]).ToList();
        var included = level.Where(r => !r.InsufficientData).ToList();
        var selfScores = level.Where(r => r.SelfScore is not null).Select(r => (double)r.SelfScore!.Value).ToList();

        return new LevelStats(
            included.Count > 0 ? included.Average(r => r.Score!.Value) : null,
            selfScores.Count > 0 ? selfScores.Average() : null,
            included.Count > 0 ? (double)included.Count(r => r.IsMet == true) / included.Count : null,
            included.Count,
            level.Count);
    }

    private static double? MetShare(IEnumerable<SessionIndicator> indicators, Dictionary<Guid, IndicatorResult> results)
    {
        var included = indicators.Select(i => results[i.Id]).Where(r => !r.InsufficientData).ToList();
        return included.Count > 0 ? (double)included.Count(r => r.IsMet == true) / included.Count : null;
    }
}
