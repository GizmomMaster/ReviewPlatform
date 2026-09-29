using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Matrix;

/// <summary>Строка плоского шаблона матрицы (ТЗ, 8.1.4): один индикатор.</summary>
public sealed record MatrixImportRow(int RowNumber, string TrackCode, string GroupName, int GroupOrder, string GradeCode, string Text, int Order);

public enum MatrixChangeKind
{
    GroupAdded,
    GroupChanged,
    IndicatorAdded,
    IndicatorChanged,
    IndicatorArchived,
}

public sealed record MatrixChange(MatrixChangeKind Kind, string GroupName, string? GradeCode, string? Text, string? Details);

/// <summary>
/// Импорт заменяет матрицу направления целиком (ТЗ, 8.1.4). Индикатор узнаётся по группе (без учёта регистра),
/// грейду и тексту: совпал — остаётся (при другом порядке — «изменён»), нет в матрице — добавляется,
/// нет в файле — архивируется. Изменённый текст — это архивирование старого и добавление нового индикатора.
/// Группы, которых нет в файле, остаются, но все их индикаторы архивируются.
/// </summary>
public sealed class MatrixImportPlan
{
    private readonly List<Action> _operations = [];
    private readonly List<CompetencyGroup> _newGroups = [];
    private readonly List<MatrixChange> _changes = [];

    private MatrixImportPlan(IReadOnlyList<string> errors) => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<MatrixChange> Changes => _changes;
    public int UnchangedIndicators { get; private set; }
    public int TotalIndicators { get; private set; }
    public bool IsValid => Errors.Count == 0;

    public static MatrixImportPlan Create(
        Track track, IReadOnlyCollection<Grade> grades, IReadOnlyCollection<CompetencyGroup> groups, IReadOnlyList<MatrixImportRow> rows)
    {
        var gradeByCode = grades.ToDictionary(g => g.Code, StringComparer.OrdinalIgnoreCase);
        var errors = Validate(track, gradeByCode, rows);
        var plan = new MatrixImportPlan(errors);
        if (errors.Count > 0)
        {
            return plan;
        }

        plan.Build(track, gradeByCode, groups, rows);
        return plan;
    }

    /// <summary>Применяет план к загруженным группам направления.</summary>
    /// <returns>Новые группы — их нужно добавить в контекст.</returns>
    public IReadOnlyList<CompetencyGroup> Apply()
    {
        if (!IsValid)
        {
            throw new DomainException("Файл содержит ошибки — импорт невозможен.");
        }

        foreach (var operation in _operations)
        {
            operation();
        }

        _operations.Clear();
        return _newGroups;
    }

    private static List<string> Validate(Track track, Dictionary<string, Grade> gradeByCode, IReadOnlyList<MatrixImportRow> rows)
    {
        var errors = new List<string>();
        if (rows.Count == 0)
        {
            errors.Add("В файле нет ни одного индикатора.");
            return errors;
        }

        foreach (var row in rows)
        {
            var problems = new List<string>();
            if (!string.Equals(row.TrackCode, track.Code, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"направление «{row.TrackCode}» не совпадает с выбранным «{track.Code}»");
            }

            if (!gradeByCode.ContainsKey(row.GradeCode))
            {
                problems.Add($"неизвестный грейд «{row.GradeCode}»");
            }

            if (row.GroupName.Trim().Length > CompetencyGroup.MaxNameLength)
            {
                problems.Add($"название группы длиннее {CompetencyGroup.MaxNameLength} символов");
            }

            if (row.Text.Length > Indicator.MaxTextLength)
            {
                problems.Add($"текст индикатора длиннее {Indicator.MaxTextLength} символов");
            }

            if (problems.Count > 0)
            {
                errors.Add($"Строка {row.RowNumber}: {string.Join(", ", problems)}.");
            }
        }

        foreach (var group in rows.GroupBy(r => r.GroupName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (group.Select(r => r.GroupOrder).Distinct().Count() > 1)
            {
                errors.Add($"Группа «{group.Key}»: в строках указан разный порядок группы ({string.Join(", ", group.Select(r => r.GroupOrder).Distinct())}).");
            }
        }

        var duplicates = rows
            .Where(r => r.Text.Length <= Indicator.MaxTextLength)
            .GroupBy(r => (Group: r.GroupName.Trim().ToUpperInvariant(), Grade: r.GradeCode.ToUpperInvariant(), Text: Indicator.NormalizeText(r.Text)))
            .Where(g => g.Count() > 1);
        foreach (var duplicate in duplicates)
        {
            errors.Add($"Строки {string.Join(", ", duplicate.Select(r => r.RowNumber))}: индикатор повторяется в группе и грейде.");
        }

        return errors;
    }

    private void Build(Track track, Dictionary<string, Grade> gradeByCode, IReadOnlyCollection<CompetencyGroup> groups, IReadOnlyList<MatrixImportRow> rows)
    {
        var existingByName = groups.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);
        var gradeCodeById = gradeByCode.Values.ToDictionary(g => g.Id, g => g.Code);
        var matched = new HashSet<Guid>();
        var updates = new List<Action>();

        foreach (var fileGroup in rows.GroupBy(r => r.GroupName.Trim(), StringComparer.OrdinalIgnoreCase).OrderBy(g => g.First().GroupOrder))
        {
            var name = fileGroup.Key;
            var order = fileGroup.First().GroupOrder;
            CompetencyGroup target;
            if (existingByName.TryGetValue(name, out var existing))
            {
                target = existing;
                var details = new List<string>();
                if (existing.Name != name)
                {
                    details.Add($"название «{existing.Name}» → «{name}»");
                }

                if (existing.Order != order)
                {
                    details.Add($"порядок {existing.Order} → {order}");
                }

                if (details.Count > 0)
                {
                    _changes.Add(new MatrixChange(MatrixChangeKind.GroupChanged, name, null, null, string.Join(", ", details)));
                    updates.Add(() =>
                    {
                        existing.Update(name, existing.Description);
                        existing.SetOrder(order);
                    });
                }
            }
            else
            {
                target = new CompetencyGroup(track.Id, name, order);
                _newGroups.Add(target);
                _changes.Add(new MatrixChange(MatrixChangeKind.GroupAdded, name, null, null, $"порядок {order}"));
            }

            foreach (var row in fileGroup.OrderBy(r => gradeByCode[r.GradeCode].Order).ThenBy(r => r.Order))
            {
                TotalIndicators++;
                var grade = gradeByCode[row.GradeCode];
                var text = Indicator.NormalizeText(row.Text);
                var match = target.ActiveIndicators(grade.Id).FirstOrDefault(i => i.Text == text);
                if (match is null)
                {
                    _changes.Add(new MatrixChange(MatrixChangeKind.IndicatorAdded, name, grade.Code, text, null));
                    var group = target;
                    var rowOrder = row.Order;
                    updates.Add(() => group.AddIndicator(grade.Id, text, rowOrder));
                    continue;
                }

                matched.Add(match.Id);
                if (match.Order == row.Order)
                {
                    UnchangedIndicators++;
                    continue;
                }

                _changes.Add(new MatrixChange(MatrixChangeKind.IndicatorChanged, name, grade.Code, text, $"порядок {match.Order} → {row.Order}"));
                var owner = target;
                var newOrder = row.Order;
                updates.Add(() => owner.SetIndicatorOrder(match.Id, newOrder));
            }
        }

        // Архивирование — первым: освобождает тексты до добавления новых индикаторов
        foreach (var group in groups.OrderBy(g => g.Order))
        {
            foreach (var indicator in group.Indicators.Where(i => !i.IsArchived && !matched.Contains(i.Id)).OrderBy(i => i.Order))
            {
                _changes.Add(new MatrixChange(MatrixChangeKind.IndicatorArchived, group.Name, gradeCodeById.GetValueOrDefault(indicator.GradeId), indicator.Text, null));
                _operations.Add(() => group.ArchiveIndicator(indicator.Id));
            }
        }

        _operations.AddRange(updates);
    }
}
