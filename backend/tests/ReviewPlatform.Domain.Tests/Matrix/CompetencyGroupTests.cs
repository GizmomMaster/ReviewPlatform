using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Matrix;

public sealed class CompetencyGroupTests
{
    private static readonly Guid GradeId = Guid.NewGuid();

    [Fact]
    public void AddIndicator_NormalizesWhitespace()
    {
        var group = new CompetencyGroup(Guid.NewGuid(), "Коммуникация", 1);

        var indicator = group.AddIndicator(GradeId, "  Аргументирует\n свою   точку зрения ", 1);

        Assert.Equal("Аргументирует свою точку зрения", indicator.Text);
        Assert.Equal(group.Id, indicator.GroupId);
    }

    [Fact]
    public void AddIndicator_DuplicateForSameGrade_Throws()
    {
        var group = new CompetencyGroup(Guid.NewGuid(), "Коммуникация", 1);
        group.AddIndicator(GradeId, "Аргументирует свою точку зрения", 1);

        Assert.Throws<DomainException>(() => group.AddIndicator(GradeId, "Аргументирует  свою точку зрения", 2));
    }

    [Fact]
    public void AddIndicator_SameTextForAnotherGrade_Allowed()
    {
        var group = new CompetencyGroup(Guid.NewGuid(), "Коммуникация", 1);
        group.AddIndicator(GradeId, "Фасилитирует дискуссии", 1);

        group.AddIndicator(Guid.NewGuid(), "Фасилитирует дискуссии", 1);

        Assert.Equal(2, group.Indicators.Count);
    }

    [Fact]
    public void AddIndicator_TooLong_Throws()
    {
        var group = new CompetencyGroup(Guid.NewGuid(), "Коммуникация", 1);

        Assert.Throws<DomainException>(() => group.AddIndicator(GradeId, new string('а', Indicator.MaxTextLength + 1), 1));
    }
}
