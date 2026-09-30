using ClosedXML.Excel;
using ReviewPlatform.Application.Reports;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Assessments.Reporting;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Infrastructure.Reports;

/// <summary>Отчёт по сессии в Excel: сводка, индикаторы с оценками каждого респондента, комментарии.</summary>
internal sealed class ReportExcelExporter : IReportExporter
{
    private static readonly Dictionary<EvaluatorRole, string> RoleNames = new()
    {
        [EvaluatorRole.Self] = "Самооценка",
        [EvaluatorRole.Peer] = "Коллега",
        [EvaluatorRole.TeamLead] = "Лид",
        [EvaluatorRole.Manager] = "Менеджер",
        [EvaluatorRole.Rck] = "РЦК",
        [EvaluatorRole.ItLeader] = "ИТ-Лидер",
    };

    public byte[] ToExcel(SessionReportDto report)
    {
        using var workbook = new XLWorkbook();
        WriteSummary(workbook.AddWorksheet("Сводка"), report);
        WriteIndicators(workbook.AddWorksheet("Индикаторы"), report);
        WriteComments(workbook.AddWorksheet("Комментарии"), report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummary(IXLWorksheet sheet, SessionReportDto r)
    {
        sheet.Cell(1, 1).Value = $"Оценка компетенций: {r.EmployeeName}";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = r.TargetGrade is { } t
            ? $"Переход {r.CurrentGrade.Code} {r.CurrentGrade.Name} → {t.Code} {t.Name}"
            : $"Подтверждение {r.CurrentGrade.Code} {r.CurrentGrade.Name}";
        sheet.Cell(3, 1).Value = $"Анкет отправлено: {r.SubmittedCount} из {r.ParticipantCount}" + (r.IsPreliminary ? " (промежуточные результаты)" : "");

        var row = 5;
        void Metric(string name, double? value, double hint)
        {
            sheet.Cell(row, 1).Value = name;
            SetPercent(sheet.Cell(row, 2), value);
            sheet.Cell(row, 3).Value = $"ориентир {hint:P0}";
            row++;
        }

        Metric("Подтверждение текущего грейда", r.CurrentConfirmation, r.Policy.CurrentConfirmationHint);
        if (r.TargetGrade is not null)
        {
            Metric("Готовность к следующему грейду", r.TargetReadiness, r.Policy.TargetReadinessHint);
        }

        sheet.Cell(row++, 1).Value = $"Индикаторов с недостаточными данными (меньше {r.Policy.Quorum} оценок): {r.InsufficientCount}";
        row++;

        string[] headers = r.TargetGrade is null
            ? ["Группа", "Среднее окружения", "Самооценка", "Выполнено"]
            : ["Группа", "Текущий: среднее окружения", "Текущий: самооценка", "Текущий: выполнено", "Следующий: среднее окружения", "Следующий: самооценка", "Готовность к следующему"];
        var headerRow = row;
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(row, c + 1).Value = headers[c];
        }

        foreach (var g in r.Groups)
        {
            row++;
            sheet.Cell(row, 1).Value = g.Name;
            WriteStats(sheet, row, 2, g.Current);
            if (g.Target is { } target)
            {
                WriteStats(sheet, row, 5, target);
            }
        }

        StyleTable(sheet.Range(headerRow, 1, row, headers.Length));
        sheet.Columns().AdjustToContents(1, 120);
    }

    private static void WriteIndicators(IXLWorksheet sheet, SessionReportDto r)
    {
        var raters = r.Raters.Where(x => x.Status == ParticipantStatus.Submitted).ToList();
        string[] fixedHeaders = ["Группа", "Грейд", "Индикатор", "Итог окружения", "Оценок", "Самооценка", "Выполнен", "Спорный", "Слепое пятно"];
        var headers = fixedHeaders.Concat(raters.Select(x => $"{x.FullName} ({RoleNames[x.Role]})")).ToArray();
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var row = 1;
        foreach (var i in r.Indicators)
        {
            row++;
            sheet.Cell(row, 1).Value = i.GroupName;
            sheet.Cell(row, 2).Value = i.GradeCode;
            sheet.Cell(row, 3).Value = i.Text;
            SetNumber(sheet.Cell(row, 4), i.Score);
            sheet.Cell(row, 5).Value = i.RatingsCount;
            SetNumber(sheet.Cell(row, 6), i.SelfScore);
            sheet.Cell(row, 7).Value = i.InsufficientData ? "мало данных" : i.IsMet == true ? "да" : "нет";
            sheet.Cell(row, 8).Value = i.IsDisputed ? "да" : "";
            sheet.Cell(row, 9).Value = i.BlindSpot switch
            {
                BlindSpot.Overestimated => "переоценка",
                BlindSpot.Underestimated => "недооценка",
                _ => "",
            };

            var byRater = i.Ratings.ToDictionary(x => x.RaterId);
            for (var c = 0; c < raters.Count; c++)
            {
                var cell = sheet.Cell(row, fixedHeaders.Length + c + 1);
                if (byRater.TryGetValue(raters[c].Id, out var rating))
                {
                    if (rating.NotApplicable) cell.Value = "N/A";
                    else SetNumber(cell, rating.Score);
                }
            }

            if (i.IsMet == false)
            {
                sheet.Cell(row, 4).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FDE2E1"));
            }
        }

        StyleTable(sheet.Range(1, 1, row, headers.Length));
        sheet.Column(3).Width = 70;
        sheet.Column(3).Style.Alignment.SetWrapText();
        sheet.Columns(1, 2).AdjustToContents();
        sheet.Columns(4, headers.Length).Width = 14;
        sheet.Row(1).Style.Alignment.SetWrapText();
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteComments(IXLWorksheet sheet, SessionReportDto r)
    {
        string[] headers = ["Группа", "Индикатор", "Респондент", "Роль", "Оценка", "Комментарий"];
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var raters = r.Raters.ToDictionary(x => x.Id);
        var row = 1;
        foreach (var i in r.Indicators)
        {
            foreach (var rating in i.Ratings.Where(x => x.Comment is not null))
            {
                row++;
                var rater = raters[rating.RaterId];
                sheet.Cell(row, 1).Value = i.GroupName;
                sheet.Cell(row, 2).Value = i.Text;
                sheet.Cell(row, 3).Value = rater.FullName;
                sheet.Cell(row, 4).Value = RoleNames[rater.Role];
                if (rating.NotApplicable) sheet.Cell(row, 5).Value = "N/A";
                else SetNumber(sheet.Cell(row, 5), rating.Score);
                sheet.Cell(row, 6).Value = rating.Comment;
            }
        }

        StyleTable(sheet.Range(1, 1, Math.Max(row, 1), headers.Length));
        sheet.Column(2).Width = 60;
        sheet.Column(6).Width = 80;
        sheet.Columns(2, 6).Style.Alignment.SetWrapText();
        sheet.Columns(1, 1).AdjustToContents();
        sheet.Columns(3, 5).AdjustToContents();
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteStats(IXLWorksheet sheet, int row, int column, LevelStats stats)
    {
        SetNumber(sheet.Cell(row, column), stats.Score);
        SetNumber(sheet.Cell(row, column + 1), stats.Self);
        SetPercent(sheet.Cell(row, column + 2), stats.MetShare);
    }

    private static void SetNumber(IXLCell cell, double? value)
    {
        if (value is { } v)
        {
            cell.Value = Math.Round(v, 2);
            cell.Style.NumberFormat.Format = "0.00";
        }
        else
        {
            cell.Value = "—";
        }
    }

    private static void SetPercent(IXLCell cell, double? value)
    {
        if (value is { } v)
        {
            cell.Value = v;
            cell.Style.NumberFormat.Format = "0%";
        }
        else
        {
            cell.Value = "—";
        }
    }

    private static void StyleTable(IXLRange range)
    {
        range.FirstRow().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EEF2F7"));
        range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
        range.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
    }
}
