using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Sessions;

/// <summary>Доступ к сессиям: администратор видит все, руководитель — только свои.</summary>
internal sealed class SessionAccess(IAppDbContext db, ICurrentUser currentUser)
{
    public IQueryable<AssessmentSession> Visible() =>
        currentUser.IsAdmin ? db.AssessmentSessions : db.AssessmentSessions.Where(s => s.OwnerUserId == currentUser.UserId);

    public async Task<AssessmentSession> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await Visible()
            .Include(s => s.Participants)
            .Include(s => s.Decision).ThenInclude(d => d!.PlanItems)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssessmentSession), id);
}

/// <summary>Индикаторы матрицы для текущего и целевого грейда — снимок при запуске и предпросмотр черновика.</summary>
internal sealed class IndicatorSnapshotBuilder(IAppDbContext db)
{
    public async Task<IReadOnlyList<IndicatorSnapshot>> BuildAsync(Guid trackId, Guid currentGradeId, Guid? targetGradeId, CancellationToken cancellationToken)
    {
        Guid[] gradeIds = targetGradeId is { } target ? [currentGradeId, target] : [currentGradeId];
        var rows = await (
                from i in db.Indicators
                join g in db.CompetencyGroups on i.GroupId equals g.Id
                join gr in db.Grades on i.GradeId equals gr.Id
                where g.TrackId == trackId && !i.IsArchived && gradeIds.Contains(i.GradeId)
                select new { i.Id, GroupName = g.Name, GroupOrder = g.Order, gr.Code, i.GradeId, i.Text, i.Order })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Select(r => new IndicatorSnapshot(r.Id, r.GroupName, r.GroupOrder, r.Code,
                r.GradeId == currentGradeId ? LevelKind.Current : LevelKind.Target, r.Text, r.Order))
            .OrderBy(r => r.GroupOrder).ThenBy(r => r.LevelKind).ThenBy(r => r.Order)];
    }
}

internal sealed class SessionReader(IAppDbContext db, IIdentityService identity, IndicatorSnapshotBuilder snapshots)
{
    public async Task<IReadOnlyList<GradeRoleRule>> RulesForAsync(Guid gradeId, CancellationToken cancellationToken) =>
        await db.GradeRoleRules.Where(r => r.GradeId == gradeId).ToListAsync(cancellationToken);

    public async Task<SessionDetailsDto> ToDetailsAsync(AssessmentSession session, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.SingleAsync(e => e.Id == session.EmployeeId, cancellationToken);
        var trackName = await db.Tracks.Where(t => t.Id == session.TrackId).Select(t => t.Name).SingleAsync(cancellationToken);
        var grades = await db.Grades
            .Where(g => g.Id == session.CurrentGradeId || g.Id == session.TargetGradeId)
            .Select(g => new GradeDto(g.Id, g.Code, g.Name, g.Order))
            .ToDictionaryAsync(g => g.Id, cancellationToken);
        var ownerName = (await identity.GetUserNamesAsync([session.OwnerUserId], cancellationToken)).GetValueOrDefault(session.OwnerUserId, "—");

        var indicatorCount = session.IsLaunched
            ? await db.SessionIndicators.CountAsync(i => i.SessionId == session.Id, cancellationToken)
            : (await snapshots.BuildAsync(session.TrackId, session.CurrentGradeId, session.TargetGradeId, cancellationToken)).Count;

        var requirements = RoleRequirements.Evaluate(await RulesForAsync(session.CurrentGradeId, cancellationToken), session.Participants)
            .Select(r => new RoleRequirementDto(r.Role, r.MinCount, r.MaxCount, r.Count, r.IsSatisfied))
            .ToList();

        var participants = session.Participants
            .OrderBy(p => p.Status == ParticipantStatus.Removed).ThenBy(p => p.Role).ThenBy(p => p.FullName)
            .Select(p => new ParticipantDto(p.Id, p.FullName, p.Email, p.Role, p.Status, p.TokenIssuedAtUtc, p.FirstOpenedAtUtc, p.SubmittedAtUtc))
            .ToList();

        return new SessionDetailsDto(
            session.Id, employee.Id, employee.FullName, employee.Email, trackName, session.Type,
            grades[session.CurrentGradeId], session.TargetGradeId is { } t ? grades[t] : null,
            session.Status, session.DeadlineAtUtc, session.CreatedAtUtc, session.LaunchedAtUtc, session.CompletedAtUtc, session.ClosedAtUtc,
            session.OwnerUserId, ownerName, indicatorCount, participants, requirements,
            await DecisionAsync(session, cancellationToken));
    }

    public async Task<DecisionDto?> DecisionAsync(AssessmentSession session, CancellationToken cancellationToken)
    {
        if (session.Decision is not { } d)
        {
            return null;
        }

        var grade = await db.Grades.Where(g => g.Id == d.NewGradeId).Select(g => new GradeDto(g.Id, g.Code, g.Name, g.Order)).SingleAsync(cancellationToken);
        var decidedBy = (await identity.GetUserNamesAsync([d.DecidedByUserId], cancellationToken)).GetValueOrDefault(d.DecidedByUserId, "—");
        return new DecisionDto(d.Outcome, grade, d.Comment, d.DecidedAtUtc, decidedBy,
            [.. d.PlanItems.OrderBy(p => p.Order).Select(p => new PlanItemDto(p.Text, p.SessionIndicatorId, p.DueDate))]);
    }
}
