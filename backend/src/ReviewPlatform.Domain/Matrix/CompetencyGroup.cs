using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Matrix;

public sealed class CompetencyGroup : Entity
{
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

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public Indicator AddIndicator(Guid gradeId, string text, int order)
    {
        var normalized = Indicator.NormalizeText(text);
        if (_indicators.Any(i => !i.IsArchived && i.GradeId == gradeId && i.Text == normalized))
        {
            throw new DomainException($"Индикатор «{normalized}» уже есть в группе «{Name}» для этого грейда.");
        }

        var indicator = new Indicator(Id, gradeId, normalized, order);
        _indicators.Add(indicator);
        return indicator;
    }
}
