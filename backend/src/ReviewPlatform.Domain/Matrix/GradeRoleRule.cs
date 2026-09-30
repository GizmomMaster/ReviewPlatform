using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Matrix;

/// <summary>Сколько оценщиков роли нужно в сессии сотрудника с данным грейдом.</summary>
public sealed class GradeRoleRule : Entity
{
    private GradeRoleRule() { }

    public GradeRoleRule(Guid gradeId, EvaluatorRole role, int minCount, int maxCount)
    {
        GradeId = gradeId;
        Role = role;
        SetLimits(minCount, maxCount);
    }

    public Guid GradeId { get; private set; }
    public EvaluatorRole Role { get; private set; }
    public int MinCount { get; private set; }
    public int MaxCount { get; private set; }

    public void SetLimits(int minCount, int maxCount)
    {
        if (minCount < 0 || maxCount < 1 || minCount > maxCount)
        {
            throw new DomainException($"Некорректные границы количества оценщиков: {minCount}..{maxCount}.");
        }

        if (Role == EvaluatorRole.Self && (minCount != 1 || maxCount != 1))
        {
            throw new DomainException("Самооценка в сессии всегда ровно одна.");
        }

        MinCount = minCount;
        MaxCount = maxCount;
    }
}
