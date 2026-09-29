using ClosedXML.Excel;

namespace ReviewPlatform.Infrastructure.Seeding;

public sealed record MatrixRow(int RowNumber, string TrackCode, string GroupName, int GroupOrder, string GradeCode, string Text, int Order);

public sealed record MatrixReadResult(IReadOnlyList<MatrixRow> Rows, IReadOnlyList<string> Errors);

/// <summary>Чтение плоского шаблона матрицы (ТЗ, п. 8.1.4): первый лист, строка заголовков, одна строка = один индикатор.</summary>
public static class MatrixSpreadsheet
{
    public const string TrackHeader = "Направление";
    public const string GroupHeader = "Группа";
    public const string GroupOrderHeader = "Порядок группы";
    public const string GradeHeader = "Грейд";
    public const string TextHeader = "Индикатор";
    public const string OrderHeader = "Порядок";

    private static readonly string[] RequiredHeaders = [TrackHeader, GroupHeader, GroupOrderHeader, GradeHeader, TextHeader, OrderHeader];

    public static MatrixReadResult Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var errors = new List<string>();

        var headerRow = sheet.Row(1);
        var columns = headerRow.CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber);
        var missing = RequiredHeaders.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            return new MatrixReadResult([], [$"Нет обязательных колонок: {string.Join(", ", missing)}."]);
        }

        var rows = new List<MatrixRow>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var n = row.RowNumber();
            string Text(string header) => row.Cell(columns[header]).GetString().Trim();

            var text = Text(TextHeader);
            if (text.Length == 0 && Text(GroupHeader).Length == 0)
            {
                continue;
            }

            var rowErrors = new List<string>();
            if (Text(TrackHeader).Length == 0) rowErrors.Add("не указано направление");
            if (Text(GroupHeader).Length == 0) rowErrors.Add("не указана группа");
            if (Text(GradeHeader).Length == 0) rowErrors.Add("не указан грейд");
            if (text.Length == 0) rowErrors.Add("пустой текст индикатора");
            if (!int.TryParse(Text(GroupOrderHeader), out var groupOrder)) rowErrors.Add("порядок группы не число");
            if (!int.TryParse(Text(OrderHeader), out var order)) rowErrors.Add("порядок не число");

            if (rowErrors.Count > 0)
            {
                errors.Add($"Строка {n}: {string.Join(", ", rowErrors)}.");
                continue;
            }

            rows.Add(new MatrixRow(n, Text(TrackHeader).ToLowerInvariant(), Text(GroupHeader), groupOrder, Text(GradeHeader).ToUpperInvariant(), text, order));
        }

        return new MatrixReadResult(rows, errors);
    }
}
