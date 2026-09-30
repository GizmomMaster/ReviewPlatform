using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Assessments;

namespace ReviewPlatform.Application.Sessions;

public sealed record ListSessionsQuery(SessionStatus? Status, Guid? EmployeeId) : IRequest<IReadOnlyList<SessionListItemDto>>;

internal sealed class ListSessionsHandler(IAppDbContext db, SessionAccess access, IIdentityService identity)
    : IRequestHandler<ListSessionsQuery, IReadOnlyList<SessionListItemDto>>
{
    public async Task<IReadOnlyList<SessionListItemDto>> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = access.Visible();
        if (request.Status is { } status) sessions = sessions.Where(s => s.Status == status);
        if (request.EmployeeId is { } employeeId) sessions = sessions.Where(s => s.EmployeeId == employeeId);

        var rows = await (
                from s in sessions
                join e in db.Employees on s.EmployeeId equals e.Id
                join cg in db.Grades on s.CurrentGradeId equals cg.Id
                join tg in db.Grades on s.TargetGradeId equals tg.Id into targets
                from tg in targets.DefaultIfEmpty()
                orderby s.CreatedAtUtc descending
                select new
                {
                    s.Id, s.EmployeeId, EmployeeName = e.FullName, s.Type, CurrentGradeCode = cg.Code, TargetGradeCode = tg != null ? tg.Code : null,
                    s.Status, s.DeadlineAtUtc, s.OwnerUserId, s.CreatedAtUtc,
                    Total = s.Participants.Count(p => p.Status != ParticipantStatus.Removed),
                    Submitted = s.Participants.Count(p => p.Status == ParticipantStatus.Submitted),
                })
            .ToListAsync(cancellationToken);

        var owners = await identity.GetUserNamesAsync([.. rows.Select(r => r.OwnerUserId).Distinct()], cancellationToken);

        return [.. rows.Select(r => new SessionListItemDto(
            r.Id, r.EmployeeId, r.EmployeeName, r.Type, r.CurrentGradeCode, r.TargetGradeCode, r.Status, r.DeadlineAtUtc,
            r.Total, r.Submitted, r.OwnerUserId, owners.GetValueOrDefault(r.OwnerUserId, "—"), r.CreatedAtUtc))];
    }
}

public sealed record GetSessionQuery(Guid Id) : IRequest<SessionDetailsDto>;

internal sealed class GetSessionHandler(SessionAccess access, SessionReader reader) : IRequestHandler<GetSessionQuery, SessionDetailsDto>
{
    public async Task<SessionDetailsDto> Handle(GetSessionQuery request, CancellationToken cancellationToken) =>
        await reader.ToDetailsAsync(await access.LoadAsync(request.Id, cancellationToken), cancellationToken);
}

/// <summary>Анкета глазами респондента: для черновика — по текущей матрице, для запущенной — по снимку.</summary>
public sealed record GetSurveyPreviewQuery(Guid Id) : IRequest<SurveyPreviewDto>;

internal sealed class GetSurveyPreviewHandler(IAppDbContext db, SessionAccess access, IndicatorSnapshotBuilder snapshots)
    : IRequestHandler<GetSurveyPreviewQuery, SurveyPreviewDto>
{
    public async Task<SurveyPreviewDto> Handle(GetSurveyPreviewQuery request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.Id, cancellationToken);
        var indicators = session.IsLaunched
            ? (await db.SessionIndicators.Where(i => i.SessionId == session.Id).ToListAsync(cancellationToken))
                .Select(i => new IndicatorSnapshot(i.SourceIndicatorId ?? Guid.Empty, i.GroupName, i.GroupOrder, i.GradeCode, i.LevelKind, i.Text, i.Order))
                .OrderBy(i => i.GroupOrder).ThenBy(i => i.LevelKind).ThenBy(i => i.Order)
                .ToList()
            : await snapshots.BuildAsync(session.TrackId, session.CurrentGradeId, session.TargetGradeId, cancellationToken);

        var groups = indicators
            .GroupBy(i => (i.GroupOrder, i.GroupName))
            .OrderBy(g => g.Key.GroupOrder)
            .Select(g => new PreviewGroupDto(g.Key.GroupName, [.. g.Select(i => new PreviewIndicatorDto(i.Text, i.GradeCode, i.LevelKind))]))
            .ToList();

        return new SurveyPreviewDto(indicators.Count, groups);
    }
}
