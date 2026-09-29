using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Assessments;

public sealed record RoleRequirement(EvaluatorRole Role, int MinCount, int MaxCount, int Count)
{
    public bool IsSatisfied => Count >= MinCount && Count <= MaxCount;
}

/// <summary>Сверка состава респондентов с правилами грейда (ТЗ, 5.1 п.6).</summary>
public static class RoleRequirements
{
    public static IReadOnlyList<RoleRequirement> Evaluate(IEnumerable<GradeRoleRule> rules, IEnumerable<Participant> participants)
    {
        var counts = participants.Where(p => p.IsActive).GroupBy(p => p.Role).ToDictionary(g => g.Key, g => g.Count());
        var ruleList = rules.ToList();

        var result = ruleList
            .Select(r => new RoleRequirement(r.Role, r.MinCount, r.MaxCount, counts.GetValueOrDefault(r.Role)))
            .ToList();

        // Роли без правила для грейда недопустимы: 0..0
        result.AddRange(counts.Keys
            .Where(role => ruleList.All(r => r.Role != role))
            .Select(role => new RoleRequirement(role, 0, 0, counts[role])));

        return [.. result.OrderBy(r => r.Role)];
    }
}
