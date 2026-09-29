using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Matrix;

/// <summary>Группа компетенций направления с индикаторами по грейдам. Правки индикаторов — только через группу.</summary>
public sealed class CompetencyGroup : Entity
{
    public const int MaxNameLength = 200;

    private readonly List<Indicator> _indicators = [];

    private CompetencyGroup() { }

    public CompetencyGroup(Guid trackId, string name, int order, string? description = null)
    {
        TrackId = trackId;
        Order = order;
        Update(name, description);
    }

    public Guid TrackId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int Order { get; private set; }
    public IReadOnlyCollection<Indicator> Indicators => _indicators;

    public IEnumerable<Indicator> ActiveIndicators(Guid gradeId) =>
        _indicators.Where(i => !i.IsArchived && i.GradeId == gradeId).OrderBy(i => i.Order);

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > MaxNameLength)
        {
            throw new DomainException($"Название группы длиннее {MaxNameLength} символов.");
        }

        Name = normalized;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetOrder(int order) => Order = order;

    /// <param name="order">null — в конец списка грейда.</param>
    public Indicator AddIndicator(Guid gradeId, string text, int? order = null)
    {
        var normalized = Indicator.NormalizeText(text);
        EnsureUniqueText(gradeId, normalized, exceptId: null);

        var indicator = new Indicator(Id, gradeId, normalized, order ?? NextOrder(gradeId));
        _indicators.Add(indicator);
        return indicator;
    }

    public Indicator EditIndicator(Guid indicatorId, string text)
    {
        var indicator = ActiveIndicator(indicatorId);
        var normalized = Indicator.NormalizeText(text);
        EnsureUniqueText(indicator.GradeId, normalized, indicator.Id);
        indicator.SetText(normalized);
        return indicator;
    }

    /// <summary>Индикатор больше не попадает в новые анкеты; уже запущенные сессии хранят свой снимок.</summary>
    public void ArchiveIndicator(Guid indicatorId) => ActiveIndicator(indicatorId).Archive();

    /// <summary>Физическое удаление. Использованный в сессиях индикатор можно только архивировать — это проверяет вызывающий код.</summary>
    public void RemoveIndicator(Guid indicatorId) => _indicators.Remove(ActiveIndicator(indicatorId));

    /// <summary>Новый порядок индикаторов грейда: список должен содержать ровно все активные индикаторы этого грейда.</summary>
    public void ReorderIndicators(Guid gradeId, IReadOnlyList<Guid> orderedIds)
    {
        var current = ActiveIndicators(gradeId).ToList();
        if (orderedIds.Count != current.Count || !current.Select(i => i.Id).ToHashSet().SetEquals(orderedIds))
        {
            throw new DomainException("Список для сортировки не совпадает с индикаторами грейда — обновите страницу.");
        }

        var byId = current.ToDictionary(i => i.Id);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            byId[orderedIds[i]].SetOrder(i + 1);
        }
    }

    internal void SetIndicatorOrder(Guid indicatorId, int order) => ActiveIndicator(indicatorId).SetOrder(order);

    private int NextOrder(Guid gradeId) => ActiveIndicators(gradeId).Select(i => i.Order).DefaultIfEmpty(0).Max() + 1;

    private Indicator ActiveIndicator(Guid indicatorId) =>
        _indicators.SingleOrDefault(i => i.Id == indicatorId && !i.IsArchived)
            ?? throw new DomainException("Индикатор не найден в группе или уже в архиве.");

    private void EnsureUniqueText(Guid gradeId, string text, Guid? exceptId)
    {
        if (_indicators.Any(i => !i.IsArchived && i.GradeId == gradeId && i.Id != exceptId && i.Text == text))
        {
            throw new DomainException($"Индикатор «{text}» уже есть в группе «{Name}» для этого грейда.");
        }
    }
}
