using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Matrix;

/// <summary>Наблюдаемое поведение уровня грейда — вопрос анкеты.</summary>
public sealed class Indicator : Entity
{
    /// <summary>Ограничение уникального btree-индекса Postgres (~2.7 КБ на строку, кириллица — 2 байта на символ).</summary>
    public const int MaxTextLength = 1000;

    private Indicator() { }

    internal Indicator(Guid groupId, Guid gradeId, string text, int order)
    {
        GroupId = groupId;
        GradeId = gradeId;
        Text = text;
        Order = order;
    }

    public Guid GroupId { get; private set; }
    public Guid GradeId { get; private set; }
    public string Text { get; private set; } = null!;
    public int Order { get; private set; }
    public bool IsArchived { get; private set; }

    internal static string NormalizeText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= MaxTextLength
            ? normalized
            : throw new DomainException($"Текст индикатора длиннее {MaxTextLength} символов.");
    }
}
