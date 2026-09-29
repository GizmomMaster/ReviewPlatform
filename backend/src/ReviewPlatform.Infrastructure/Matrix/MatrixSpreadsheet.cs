using ClosedXML.Excel;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Infrastructure.Matrix;

/// <summary>Плоский шаблон матрицы (ТЗ, п. 8.1.4): первый лист, строка заголовков, одна строка = один индикатор.</summary>
internal sealed class MatrixSpreadsheet : IMatrixSpreadsheet
{
    public const string TrackHeader = "Направление";
    public const string GroupHeader = "Группа";
    public const string GroupOrderHeader = "Порядок группы";
    public const string GradeHeader = "Грейд";
    public const string TextHeader = "Индикатор";
    public const string OrderHeader = "Порядок";

    private const string SheetName = "Матрица";

    private static readonly string[] Headers = [TrackHeader, GroupHeader, GroupOrderHeader, GradeHeader, TextHeader, OrderHeader];

    public MatrixReadResult Read(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is InvalidDataException or FileFormatException or ArgumentException or NotSupportedException)
        {
            return new MatrixReadResult([], ["Не удалось прочитать файл — нужен Excel в формате .xlsx."]);
        }

        using (workbook)
        {
            return Read(workbook.Worksheet(1));
        }
    }

    public byte[] Write(IEnumerable<MatrixImportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = AddDataSheet(workbook);
        var n = 2;
        foreach (var row in rows)
        {
            sheet.Cell(n, 1).Value = row.TrackCode;
            sheet.Cell(n, 2).Value = row.GroupName;
            sheet.Cell(n, 3).Value = row.GroupOrder;
            sheet.Cell(n, 4).Value = row.GradeCode;
            sheet.Cell(n, 5).Value = row.Text;
            sheet.Cell(n, 6).Value = row.Order;
            n++;
        }

        return Save(workbook);
    }

    public byte[] Template()
    {
        using var workbook = new XLWorkbook();
        AddDataSheet(workbook);

        var help = workbook.AddWorksheet("Инструкция");
        string[] lines =
        [
            "Импорт читает первый лист «Матрица»: одна строка — один индикатор.",
            $"{TrackHeader} — код направления, например backend. Должен совпадать с направлением, в которое загружается файл.",
            $"{GroupHeader} — название группы компетенций. {GroupOrderHeader} — её место в матрице (одинаковое во всех строках группы).",
            $"{GradeHeader} — код грейда E1…E8. {TextHeader} — наблюдаемое поведение. {OrderHeader} — место индикатора внутри группы и грейда.",
            "Импорт заменяет матрицу направления целиком: индикаторы, которых нет в файле, будут архивированы.",
            "Перед применением система покажет, что будет добавлено, изменено и архивировано.",
            "Проще всего начать с выгрузки текущей матрицы: «Экспорт» на странице матрицы.",
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            help.Cell(i + 1, 1).Value = lines[i];
        }

        help.Column(1).Width = 120;
        return Save(workbook);
    }

    private static MatrixReadResult Read(IXLWorksheet sheet)
    {
        var errors = new List<string>();
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            columns.TryAdd(cell.GetString().Trim(), cell.Address.ColumnNumber);
        }

        var missing = Headers.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            return new MatrixReadResult([], [$"Нет обязательных колонок: {string.Join(", ", missing)}. Скачайте шаблон и сверьте заголовки."]);
        }

        var rows = new List<MatrixImportRow>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var n = row.RowNumber();
            string Text(string header) => row.Cell(columns[header]).GetString().Trim();

            var text = Text(TextHeader);
            if (Headers.All(h => Text(h).Length == 0))
            {
                continue;
            }

            var rowErrors = new List<string>();
            if (Text(TrackHeader).Length == 0) rowErrors.Add("не указано направление");
            if (Text(GroupHeader).Length == 0) rowErrors.Add("не указана группа");
            if (Text(GradeHeader).Length == 0) rowErrors.Add("не указан грейд");
            if (text.Length == 0) rowErrors.Add("пустой текст индикатора");
            if (!int.TryParse(Text(GroupOrderHeader), out var groupOrder)) rowErrors.Add("порядок группы — не целое число");
            if (!int.TryParse(Text(OrderHeader), out var order)) rowErrors.Add("порядок — не целое число");

            if (rowErrors.Count > 0)
            {
                errors.Add($"Строка {n}: {string.Join(", ", rowErrors)}.");
                continue;
            }

            rows.Add(new MatrixImportRow(n, Text(TrackHeader).ToLowerInvariant(), Text(GroupHeader), groupOrder, Text(GradeHeader).ToUpperInvariant(), text, order));
        }

        return new MatrixReadResult(rows, errors);
    }

    private static IXLWorksheet AddDataSheet(XLWorkbook workbook)
    {
        var sheet = workbook.AddWorksheet(SheetName);
        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Column(1).Width = 14;
        sheet.Column(2).Width = 32;
        sheet.Column(3).Width = 16;
        sheet.Column(4).Width = 8;
        sheet.Column(5).Width = 90;
        sheet.Column(6).Width = 10;
        return sheet;
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
