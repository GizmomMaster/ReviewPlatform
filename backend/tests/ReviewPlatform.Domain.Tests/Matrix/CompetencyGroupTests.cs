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

public sealed class CompetencyGroupEditingTests
{
    private static readonly Guid GradeId = Guid.NewGuid();

    private static (CompetencyGroup Group, Indicator First, Indicator Second) GroupWithTwo()
    {
        var group = new CompetencyGroup(Guid.NewGuid(), "Коммуникация", 1);
        return (group, group.AddIndicator(GradeId, "Слушает"), group.AddIndicator(GradeId, "Задаёт вопросы"));
    }

    [Fact]
    public void AddIndicator_WithoutOrder_GoesLast()
    {
        var (_, first, second) = GroupWithTwo();

        Assert.Equal((1, 2), (first.Order, second.Order));
    }

    [Fact]
    public void EditIndicator_ToExistingText_Throws()
    {
        var (group, first, _) = GroupWithTwo();

        Assert.Throws<DomainException>(() => group.EditIndicator(first.Id, "Задаёт  вопросы"));
        Assert.Equal("Слушает внимательно", group.EditIndicator(first.Id, " Слушает  внимательно ").Text);
    }

    [Fact]
    public void ArchiveIndicator_FreesTextForNewIndicator()
    {
        var (group, first, _) = GroupWithTwo();

        group.ArchiveIndicator(first.Id);

        Assert.True(first.IsArchived);
        Assert.Throws<DomainException>(() => group.ArchiveIndicator(first.Id));
        Assert.Equal(3, group.AddIndicator(GradeId, "Слушает").Order);
    }

    [Fact]
    public void ReorderIndicators_SetsSequentialOrder()
    {
        var (group, first, second) = GroupWithTwo();

        group.ReorderIndicators(GradeId, [second.Id, first.Id]);

        Assert.Equal([second.Id, first.Id], group.ActiveIndicators(GradeId).Select(i => i.Id));
        Assert.Throws<DomainException>(() => group.ReorderIndicators(GradeId, [second.Id]));
    }

    [Fact]
    public void RemoveIndicator_RemovesFromGroup()
    {
        var (group, first, _) = GroupWithTwo();

        group.RemoveIndicator(first.Id);

        Assert.Single(group.Indicators);
    }
}
