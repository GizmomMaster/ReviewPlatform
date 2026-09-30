using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Assessments;

/// <summary>Снимок индикатора матрицы на момент запуска сессии. Правки матрицы на него не влияют.</summary>
public sealed class SessionIndicator : Entity
{
    private SessionIndicator() { }

    internal SessionIndicator(Guid sessionId, IndicatorSnapshot source)
    {
        SessionId = sessionId;
        SourceIndicatorId = source.IndicatorId;
        GroupName = source.GroupName;
        GroupOrder = source.GroupOrder;
        GradeCode = source.GradeCode;
        LevelKind = source.LevelKind;
        Text = source.Text;
        Order = source.Order;
    }

    public Guid SessionId { get; private set; }
    public Guid? SourceIndicatorId { get; private set; }
    public string GroupName { get; private set; } = null!;
    public int GroupOrder { get; private set; }
    public string GradeCode { get; private set; } = null!;
    public LevelKind LevelKind { get; private set; }
    public string Text { get; private set; } = null!;
    public int Order { get; private set; }
}

public sealed record IndicatorSnapshot(Guid IndicatorId, string GroupName, int GroupOrder, string GradeCode, LevelKind LevelKind, string Text, int Order);
