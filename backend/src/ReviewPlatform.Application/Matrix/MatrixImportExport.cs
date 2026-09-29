using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Reports;
using ReviewPlatform.Domain.Audit;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Matrix;

public sealed record MatrixReadResult(IReadOnlyList<MatrixImportRow> Rows, IReadOnlyList<string> Errors);

/// <summary>Плоский Excel-шаблон матрицы (ТЗ, 8.1.4): чтение, выгрузка, пустой шаблон.</summary>
public interface IMatrixSpreadsheet
{
    MatrixReadResult Read(Stream stream);

    byte[] Write(IEnumerable<MatrixImportRow> rows);

    byte[] Template();
}

/// <summary>Разобранные файлы между предпросмотром и подтверждением импорта.</summary>
public interface IMatrixImportStore
{
    Guid Save(Guid trackId, IReadOnlyList<MatrixImportRow> rows);

    IReadOnlyList<MatrixImportRow>? Find(Guid importId, Guid trackId);

    void Remove(Guid importId);
}

public sealed record MatrixImportSummaryDto(
    int GroupsAdded, int GroupsChanged, int IndicatorsAdded, int IndicatorsChanged, int IndicatorsArchived, int IndicatorsUnchanged);

/// <summary>Предпросмотр импорта. ImportId есть, только если ошибок нет — по нему импорт подтверждается.</summary>
public sealed record MatrixImportPreviewDto(Guid? ImportId, IReadOnlyList<string> Errors, MatrixImportSummaryDto Summary, IReadOnlyList<MatrixChange> Changes);

internal sealed class MatrixImportPlanner(IAppDbContext db)
{
    public async Task<(Track Track, MatrixImportPlan Plan)> PlanAsync(Guid trackId, IReadOnlyList<MatrixImportRow> rows, CancellationToken cancellationToken)
    {
        var track = await db.Tracks.SingleOrDefaultAsync(t => t.Id == trackId, cancellationToken)
            ?? throw new NotFoundException(nameof(Track), trackId);
        var grades = await db.Grades.ToListAsync(cancellationToken);
        var groups = await db.CompetencyGroups.Include(g => g.Indicators).Where(g => g.TrackId == trackId).ToListAsync(cancellationToken);
        return (track, MatrixImportPlan.Create(track, grades, groups, rows));
    }

    public static MatrixImportSummaryDto Summarize(MatrixImportPlan plan)
    {
        int Count(MatrixChangeKind kind) => plan.Changes.Count(c => c.Kind == kind);
        return new MatrixImportSummaryDto(
            Count(MatrixChangeKind.GroupAdded), Count(MatrixChangeKind.GroupChanged), Count(MatrixChangeKind.IndicatorAdded),
            Count(MatrixChangeKind.IndicatorChanged), Count(MatrixChangeKind.IndicatorArchived), plan.UnchangedIndicators);
    }
}

// ---------- Импорт: предпросмотр ----------

public sealed record PreviewMatrixImportCommand(Guid TrackId, Stream Content) : IRequest<MatrixImportPreviewDto>;

internal sealed class PreviewMatrixImportHandler(IMatrixSpreadsheet spreadsheet, MatrixImportPlanner planner, IMatrixImportStore store)
    : IRequestHandler<PreviewMatrixImportCommand, MatrixImportPreviewDto>
{
    private static readonly MatrixImportSummaryDto Empty = new(0, 0, 0, 0, 0, 0);

    public async Task<MatrixImportPreviewDto> Handle(PreviewMatrixImportCommand request, CancellationToken cancellationToken)
    {
        var file = spreadsheet.Read(request.Content);
        var (_, plan) = await planner.PlanAsync(request.TrackId, file.Rows, cancellationToken);

        // Ошибки разбора строк и ошибки плана показываем вместе: строки с ошибками разбора в план не попали
        var errors = file.Errors.Concat(file.Errors.Count > 0 && file.Rows.Count == 0 ? [] : plan.Errors).ToList();
        if (errors.Count > 0)
        {
            return new MatrixImportPreviewDto(null, errors, Empty, []);
        }

        var importId = store.Save(request.TrackId, file.Rows);
        return new MatrixImportPreviewDto(importId, [], MatrixImportPlanner.Summarize(plan), plan.Changes);
    }
}

// ---------- Импорт: подтверждение ----------

public sealed record ApplyMatrixImportCommand(Guid TrackId, Guid ImportId) : IRequest<MatrixImportSummaryDto>;

internal sealed class ApplyMatrixImportHandler(IAppDbContext db, MatrixImportPlanner planner, IMatrixImportStore store, IAuditLog audit)
    : IRequestHandler<ApplyMatrixImportCommand, MatrixImportSummaryDto>
{
    public async Task<MatrixImportSummaryDto> Handle(ApplyMatrixImportCommand request, CancellationToken cancellationToken)
    {
        var rows = store.Find(request.ImportId, request.TrackId)
            ?? throw new DomainException("Предпросмотр импорта устарел — загрузите файл ещё раз.");

        // План строится заново по текущей матрице: её могли изменить после предпросмотра
        var (track, plan) = await planner.PlanAsync(request.TrackId, rows, cancellationToken);
        db.CompetencyGroups.AddRange(plan.Apply());

        var summary = MatrixImportPlanner.Summarize(plan);
        audit.Record(AuditActions.MatrixImported, nameof(Track), track.Id,
            $"Индикаторов в файле: {plan.TotalIndicators}; добавлено {summary.IndicatorsAdded}, изменено {summary.IndicatorsChanged}, " +
            $"архивировано {summary.IndicatorsArchived}; новых групп {summary.GroupsAdded}");
        await db.SaveChangesAsync(cancellationToken);
        store.Remove(request.ImportId);
        return summary;
    }
}

// ---------- Экспорт и шаблон ----------

public sealed record ExportMatrixQuery(Guid TrackId) : IRequest<ReportFile>;

internal sealed class ExportMatrixHandler(IAppDbContext db, IMatrixSpreadsheet spreadsheet) : IRequestHandler<ExportMatrixQuery, ReportFile>
{
    public async Task<ReportFile> Handle(ExportMatrixQuery request, CancellationToken cancellationToken)
    {
        var track = await db.Tracks.SingleOrDefaultAsync(t => t.Id == request.TrackId, cancellationToken)
            ?? throw new NotFoundException(nameof(Track), request.TrackId);

        var rows = await (
                from i in db.Indicators
                join g in db.CompetencyGroups on i.GroupId equals g.Id
                join gr in db.Grades on i.GradeId equals gr.Id
                where g.TrackId == track.Id && !i.IsArchived
                orderby g.Order, gr.Order, i.Order
                select new MatrixImportRow(0, track.Code, g.Name, g.Order, gr.Code, i.Text, i.Order))
            .ToListAsync(cancellationToken);

        return new ReportFile($"Матрица_{track.Code}_{DateTime.UtcNow:yyyy-MM-dd}.xlsx", spreadsheet.Write(rows));
    }
}

public sealed record GetMatrixTemplateQuery : IRequest<ReportFile>;

internal sealed class GetMatrixTemplateHandler(IMatrixSpreadsheet spreadsheet) : IRequestHandler<GetMatrixTemplateQuery, ReportFile>
{
    public Task<ReportFile> Handle(GetMatrixTemplateQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new ReportFile("Шаблон_матрицы.xlsx", spreadsheet.Template()));
}
