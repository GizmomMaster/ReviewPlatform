using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Infrastructure.Seeding;

/// <summary>Начальные справочники (ТЗ, разделы 2 и 5.1).</summary>
internal static class ReferenceData
{
    public const string BackendTrackCode = "backend";

    public static readonly (string Code, string Name)[] Grades =
    [
        ("E1", "Intern"),
        ("E2", "Junior"),
        ("E3", "Junior+"),
        ("E4", "Middle"),
        ("E5", "Middle+"),
        ("E6", "Senior"),
        ("E7", "Senior+"),
        ("E8", "Lead"),
    ];

    /// <summary>Роли оценщиков по грейду: (роль, мин, макс).</summary>
    public static IReadOnlyList<(EvaluatorRole Role, int Min, int Max)> RoleRulesFor(string gradeCode) => gradeCode switch
    {
        "E7" =>
        [
            (EvaluatorRole.Self, 1, 1),
            (EvaluatorRole.TeamLead, 1, 1),
            (EvaluatorRole.Manager, 1, 1),
            (EvaluatorRole.Rck, 1, 1),
            (EvaluatorRole.ItLeader, 1, 1),
        ],
        "E8" =>
        [
            (EvaluatorRole.Self, 1, 1),
            (EvaluatorRole.Manager, 1, 1),
            (EvaluatorRole.Rck, 1, 1),
            (EvaluatorRole.ItLeader, 1, 1),
        ],
        _ =>
        [
            (EvaluatorRole.Self, 1, 1),
            (EvaluatorRole.Peer, 1, 5),
            (EvaluatorRole.TeamLead, 1, 1),
            (EvaluatorRole.Manager, 1, 1),
        ],
    };
}
