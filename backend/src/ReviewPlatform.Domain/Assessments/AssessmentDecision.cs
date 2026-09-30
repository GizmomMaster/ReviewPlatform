using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Assessments;

public enum DecisionOutcome
{
    /// <summary>Повышен до более высокого грейда.</summary>
    Promoted,

    /// <summary>Текущий грейд подтверждён.</summary>
    GradeConfirmed,

    /// <summary>Текущий грейд не подтверждён (грейд остаётся или понижается).</summary>
    NotConfirmed,
}

/// <summary>Решение руководителя по итогам сессии (ТЗ, 8.6).</summary>
public sealed class AssessmentDecision : Entity
{
    public const int MaxCommentLength = 4000;
    public const int MaxPlanItems = 30;

    private readonly List<DevelopmentPlanItem> _planItems = [];

    private AssessmentDecision() { }

    internal AssessmentDecision(Guid sessionId, Guid decidedByUserId, DecisionOutcome outcome, Guid newGradeId, string comment,
        IReadOnlyList<PlanItemInput> planItems, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new DomainException("Опишите обоснование решения.");
        }

        if (comment.Trim().Length > MaxCommentLength)
        {
            throw new DomainException($"Обоснование длиннее {MaxCommentLength} символов.");
        }

        if (planItems.Count > MaxPlanItems)
        {
            throw new DomainException($"В плане развития не больше {MaxPlanItems} пунктов.");
        }

        SessionId = sessionId;
        DecidedByUserId = decidedByUserId;
        Outcome = outcome;
        NewGradeId = newGradeId;
        Comment = comment.Trim();
        DecidedAtUtc = nowUtc;
        _planItems.AddRange(planItems.Select((p, i) => new DevelopmentPlanItem(Id, p, i + 1)));
    }

    public Guid SessionId { get; private set; }
    public Guid DecidedByUserId { get; private set; }
    public DecisionOutcome Outcome { get; private set; }
    public Guid NewGradeId { get; private set; }
    public string Comment { get; private set; } = null!;
    public DateTime DecidedAtUtc { get; private set; }
    public IReadOnlyCollection<DevelopmentPlanItem> PlanItems => _planItems;
}

public sealed class DevelopmentPlanItem : Entity
{
    public const int MaxTextLength = 1000;

    private DevelopmentPlanItem() { }

    internal DevelopmentPlanItem(Guid decisionId, PlanItemInput input, int order)
    {
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            throw new DomainException("Пункт плана развития не может быть пустым.");
        }

        var text = input.Text.Trim();
        if (text.Length > MaxTextLength)
        {
            throw new DomainException($"Пункт плана длиннее {MaxTextLength} символов.");
        }

        DecisionId = decisionId;
        SessionIndicatorId = input.SessionIndicatorId;
        Text = text;
        DueDate = input.DueDate;
        Order = order;
    }

    public Guid DecisionId { get; private set; }
    public Guid? SessionIndicatorId { get; private set; }
    public string Text { get; private set; } = null!;
    public DateOnly? DueDate { get; private set; }
    public int Order { get; private set; }
}

public sealed record PlanItemInput(string Text, Guid? SessionIndicatorId, DateOnly? DueDate);
