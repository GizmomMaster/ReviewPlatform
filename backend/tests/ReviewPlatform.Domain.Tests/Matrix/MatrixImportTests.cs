using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Matrix;

public sealed class MatrixImportTests
{
    private static readonly Track Track = new("backend", "Backend");
    private static readonly Grade E1 = new("E1", "Intern", 1);
    private static readonly Grade E2 = new("E2", "Junior", 2);
    private static readonly Grade[] Grades = [E1, E2];

    private static MatrixImportRow Row(string group, int groupOrder, string grade, string text, int order, int n = 2, string track = "backend") =>
        new(n, track, group, groupOrder, grade, text, order);

    private static CompetencyGroup ExistingGroup()
    {
        var group = new CompetencyGroup(Track.Id, "Коммуникация", 1);
        group.AddIndicator(E1.Id, "Слушает собеседника", 1);
        group.AddIndicator(E1.Id, "Задаёт вопросы", 2);
        group.AddIndicator(E2.Id, "Аргументирует", 1);
        return group;
    }

    [Fact]
    public void Plan_EmptyMatrix_AddsEverything()
    {
        var plan = MatrixImportPlan.Create(Track, Grades, [],
            [Row("Коммуникация", 1, "E1", "Слушает", 1), Row("Экспертность", 2, "E2", "Пишет тесты", 1)]);

        var groups = plan.Apply();

        Assert.Equal(["Коммуникация", "Экспертность"], groups.Select(g => g.Name));
        Assert.Equal(2, plan.Changes.Count(c => c.Kind == MatrixChangeKind.GroupAdded));
        Assert.Equal(2, plan.Changes.Count(c => c.Kind == MatrixChangeKind.IndicatorAdded));
        Assert.Equal(2, plan.TotalIndicators);
    }

    [Fact]
    public void Plan_ComparesWithExisting_AddsChangesArchives()
    {
        var group = ExistingGroup();
        var archived = group.Indicators.Single(i => i.Text == "Задаёт вопросы");
        var plan = MatrixImportPlan.Create(Track, Grades, [group],
        [
            Row("коммуникация", 1, "E1", "Слушает   собеседника", 1), // тот же индикатор: регистр группы и пробелы не важны
            Row("коммуникация", 1, "E2", "Аргументирует", 5),          // другой порядок
            Row("коммуникация", 1, "E2", "Фасилитирует", 6),           // новый
        ]);

        var added = plan.Apply();

        Assert.Empty(added);
        Assert.Equal(1, plan.UnchangedIndicators);
        Assert.Equal(
            [MatrixChangeKind.GroupChanged, MatrixChangeKind.IndicatorChanged, MatrixChangeKind.IndicatorAdded, MatrixChangeKind.IndicatorArchived],
            plan.Changes.Select(c => c.Kind));
        Assert.Equal("коммуникация", group.Name);
        Assert.True(archived.IsArchived);
        Assert.Equal(["Аргументирует", "Фасилитирует"], group.ActiveIndicators(E2.Id).Select(i => i.Text));
        Assert.Equal([5, 6], group.ActiveIndicators(E2.Id).Select(i => i.Order));
    }

    [Fact]
    public void Plan_GroupMissingInFile_ArchivesItsIndicators()
    {
        var group = ExistingGroup();

        var plan = MatrixImportPlan.Create(Track, Grades, [group], [Row("Экспертность", 1, "E1", "Пишет тесты", 1)]);
        plan.Apply();

        Assert.Equal(3, plan.Changes.Count(c => c.Kind == MatrixChangeKind.IndicatorArchived));
        Assert.All(group.Indicators, i => Assert.True(i.IsArchived));
    }

    [Fact]
    public void Plan_SameFileTwice_SecondTimeHasNoChanges()
    {
        var group = ExistingGroup();
        MatrixImportRow[] rows = [Row("Коммуникация", 1, "E1", "Слушает собеседника", 1), Row("Коммуникация", 1, "E2", "Новое", 2)];
        MatrixImportPlan.Create(Track, Grades, [group], rows).Apply();

        var second = MatrixImportPlan.Create(Track, Grades, [group], rows);

        Assert.Empty(second.Changes);
        Assert.Equal(2, second.UnchangedIndicators);
    }

    [Fact]
    public void Plan_InvalidRows_ReportsAllErrors_AndCannotBeApplied()
    {
        var plan = MatrixImportPlan.Create(Track, Grades, [],
        [
            Row("Коммуникация", 1, "E9", "Слушает", 1, n: 2),
            Row("Коммуникация", 2, "E1", "Задаёт вопросы", 1, n: 3),
            Row("Коммуникация", 1, "E1", "Задаёт  вопросы", 2, n: 4),
            Row("Экспертность", 3, "E1", "Пишет тесты", 1, n: 5, track: "frontend"),
        ]);

        Assert.False(plan.IsValid);
        Assert.Contains(plan.Errors, e => e.Contains("Строка 2", StringComparison.Ordinal) && e.Contains("E9", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, e => e.Contains("Строка 5", StringComparison.Ordinal) && e.Contains("frontend", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, e => e.Contains("разный порядок группы", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, e => e.Contains("Строки 3, 4", StringComparison.Ordinal));
        Assert.Throws<DomainException>(() => plan.Apply());
    }

    [Fact]
    public void Plan_NoRows_IsInvalid() =>
        Assert.False(MatrixImportPlan.Create(Track, Grades, [ExistingGroup()], []).IsValid);
}
