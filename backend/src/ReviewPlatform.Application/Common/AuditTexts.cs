using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Common;

/// <summary>Человекочитаемые подписи для записей журнала аудита.</summary>
internal static class AuditTexts
{
    public static string Role(EvaluatorRole role) => role switch
    {
        EvaluatorRole.Self => "самооценка",
        EvaluatorRole.Peer => "коллега",
        EvaluatorRole.TeamLead => "лид",
        EvaluatorRole.Manager => "менеджер",
        EvaluatorRole.Rck => "РЦК",
        EvaluatorRole.ItLeader => "ИТ-лидер",
        _ => role.ToString(),
    };

    public static string Participant(Participant p) => $"{p.FullName} ({Role(p.Role)})";

    public static string Decision(DecisionOutcome outcome, string oldGrade, string newGrade, int planItems)
    {
        var text = outcome switch
        {
            DecisionOutcome.Promoted => $"повышен {oldGrade} → {newGrade}",
            DecisionOutcome.GradeConfirmed => $"грейд {oldGrade} подтверждён",
            _ when oldGrade != newGrade => $"грейд не подтверждён, {oldGrade} → {newGrade}",
            _ => $"грейд {oldGrade} не подтверждён",
        };
        return $"{text}; пунктов плана: {planItems}";
    }
}
