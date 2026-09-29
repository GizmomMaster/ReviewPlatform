using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Assessments.Reporting;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Reports;

public sealed record ReportRaterDto(Guid Id, string FullName, EvaluatorRole Role, ParticipantStatus Status);

public sealed record ReportRatingDto(Guid RaterId, int? Score, bool NotApplicable, string? Comment);

public sealed record ReportIndicatorDto(
    Guid Id,
    string GroupName,
    LevelKind Level,
    string GradeCode,
    string Text,
    double? Score,
    int RatingsCount,
    bool InsufficientData,
    bool? IsMet,
    int? SelfScore,
    bool IsDisputed,
    BlindSpot BlindSpot,
    IReadOnlyList<ReportRatingDto> Ratings);

public sealed record ReportGroupDto(string Name, LevelStats Current, LevelStats? Target);

public sealed record ReportPolicyDto(double MetThreshold, int Quorum, double CurrentConfirmationHint, double TargetReadinessHint, double GroupReadinessHint);

public sealed record SessionReportDto(
    Guid SessionId,
    string EmployeeName,
    SessionType Type,
    GradeDto CurrentGrade,
    GradeDto? TargetGrade,
    SessionStatus Status,
    bool IsPreliminary,
    int SubmittedCount,
    int ParticipantCount,
    double? CurrentConfirmation,
    double? TargetReadiness,
    int InsufficientCount,
    ReportPolicyDto Policy,
    IReadOnlyList<ReportGroupDto> Groups,
    IReadOnlyList<ReportIndicatorDto> Indicators,
    IReadOnlyList<ReportRaterDto> Raters);

/// <summary>Отчёт по сессии (ТЗ, 8.5). До завершения опроса — промежуточный: учитываются только отправленные анкеты.</summary>
public sealed record GetSessionReportQuery(Guid SessionId) : IRequest<SessionReportDto>;

internal sealed class GetSessionReportHandler(IAppDbContext db, SessionAccess access, ReportPolicy policy)
    : IRequestHandler<GetSessionReportQuery, SessionReportDto>
{
    public async Task<SessionReportDto> Handle(GetSessionReportQuery request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.SessionId, cancellationToken);
        if (!session.IsLaunched)
        {
            throw new DomainException("Отчёт доступен после запуска опроса.");
        }

        var indicators = await db.SessionIndicators
            .Where(i => i.SessionId == session.Id)
            .OrderBy(i => i.GroupOrder).ThenBy(i => i.LevelKind).ThenBy(i => i.Order)
            .ToListAsync(cancellationToken);

        var active = session.Participants.Where(p => p.IsActive).OrderBy(p => p.Role).ThenBy(p => p.FullName).ToList();
        var submitted = active.Where(p => p.Status == ParticipantStatus.Submitted).ToDictionary(p => p.Id);
        var submittedIds = submitted.Keys.ToList();
        var answers = await db.SurveyAnswers.Where(a => submittedIds.Contains(a.ParticipantId)).ToListAsync(cancellationToken);
        var answersByIndicator = answers.ToLookup(a => a.SessionIndicatorId);

        var ratings = indicators.ToDictionary(
            i => i.Id,
            i => (IReadOnlyList<Rating>)[.. answersByIndicator[i.Id].Select(a => new Rating(a.ParticipantId, submitted[a.ParticipantId].Role, a.Score, a.NotApplicable))]);
        var result = ReportCalculator.Calculate(indicators, ratings, session.TargetGradeId is not null, policy);
        var results = result.Indicators.ToDictionary(r => r.IndicatorId);

        var employeeName = await db.Employees.Where(e => e.Id == session.EmployeeId).Select(e => e.FullName).SingleAsync(cancellationToken);
        var grades = await db.Grades
            .Where(g => g.Id == session.CurrentGradeId || g.Id == session.TargetGradeId)
            .Select(g => new GradeDto(g.Id, g.Code, g.Name, g.Order))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        var indicatorDtos = indicators.Select(i =>
        {
            var r = results[i.Id];
            var detail = answersByIndicator[i.Id]
                .OrderBy(a => submitted[a.ParticipantId].Role).ThenBy(a => submitted[a.ParticipantId].FullName)
                .Select(a => new ReportRatingDto(a.ParticipantId, a.Score, a.NotApplicable, a.Comment))
                .ToList();
            return new ReportIndicatorDto(i.Id, i.GroupName, i.LevelKind, i.GradeCode, i.Text,
                r.Score, r.RatingsCount, r.InsufficientData, r.IsMet, r.SelfScore, r.IsDisputed, r.BlindSpot, detail);
        }).ToList();

        return new SessionReportDto(
            session.Id,
            employeeName,
            session.Type,
            grades[session.CurrentGradeId],
            session.TargetGradeId is { } t ? grades[t] : null,
            session.Status,
            session.Status is not (SessionStatus.AwaitingDecision or SessionStatus.Closed),
            submitted.Count,
            active.Count,
            result.CurrentConfirmation,
            result.TargetReadiness,
            result.InsufficientCount,
            new ReportPolicyDto(policy.MetThreshold, policy.Quorum, policy.CurrentConfirmationHint, policy.TargetReadinessHint, policy.GroupReadinessHint),
            [.. result.Groups.Select(g => new ReportGroupDto(g.Name, g.Current, g.Target))],
            indicatorDtos,
            [.. active.Select(p => new ReportRaterDto(p.Id, p.FullName, p.Role, p.Status))]);
    }
}

public sealed record ReportFile(string FileName, byte[] Content);

public interface IReportExporter
{
    byte[] ToExcel(SessionReportDto report);
}

public sealed record ExportSessionReportQuery(Guid SessionId) : IRequest<ReportFile>;

internal sealed class ExportSessionReportHandler(ISender sender, IReportExporter exporter) : IRequestHandler<ExportSessionReportQuery, ReportFile>
{
    public async Task<ReportFile> Handle(ExportSessionReportQuery request, CancellationToken cancellationToken)
    {
        var report = await sender.Send(new GetSessionReportQuery(request.SessionId), cancellationToken);
        var level = report.TargetGrade is { } target ? $"{report.CurrentGrade.Code}-{target.Code}" : report.CurrentGrade.Code;
        var name = string.Concat(report.EmployeeName.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
        return new ReportFile($"Отчёт_{name}_{level}.xlsx", exporter.ToExcel(report));
    }
}
