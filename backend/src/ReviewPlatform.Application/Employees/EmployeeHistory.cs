using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Employees;

namespace ReviewPlatform.Application.Employees;

public sealed record EmployeeHistoryItemDto(
    Guid SessionId,
    SessionType Type,
    string CurrentGradeCode,
    string? TargetGradeCode,
    SessionStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    DecisionOutcome? Outcome,
    string? NewGradeCode,
    string? DecisionComment);

/// <summary>История оценок сотрудника: все сессии и решения, от новых к старым.</summary>
public sealed record GetEmployeeHistoryQuery(Guid EmployeeId) : IRequest<IReadOnlyList<EmployeeHistoryItemDto>>;

internal sealed class GetEmployeeHistoryHandler(IAppDbContext db, EmployeeReader employees)
    : IRequestHandler<GetEmployeeHistoryQuery, IReadOnlyList<EmployeeHistoryItemDto>>
{
    public async Task<IReadOnlyList<EmployeeHistoryItemDto>> Handle(GetEmployeeHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!await employees.Visible().AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Employee), request.EmployeeId);
        }

        return await (
                from s in db.AssessmentSessions
                where s.EmployeeId == request.EmployeeId
                join cg in db.Grades on s.CurrentGradeId equals cg.Id
                join tg in db.Grades on s.TargetGradeId equals tg.Id into targets
                from tg in targets.DefaultIfEmpty()
                join d in db.AssessmentDecisions on s.Id equals d.SessionId into decisions
                from d in decisions.DefaultIfEmpty()
                join ng in db.Grades on d.NewGradeId equals ng.Id into newGrades
                from ng in newGrades.DefaultIfEmpty()
                orderby s.CreatedAtUtc descending
                select new EmployeeHistoryItemDto(
                    s.Id, s.Type, cg.Code, tg != null ? tg.Code : null, s.Status, s.CreatedAtUtc, s.ClosedAtUtc,
                    d != null ? d.Outcome : null, ng != null ? ng.Code : null, d != null ? d.Comment : null))
            .ToListAsync(cancellationToken);
    }
}
