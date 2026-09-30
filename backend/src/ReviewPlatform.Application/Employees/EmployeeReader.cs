using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Employees;

namespace ReviewPlatform.Application.Employees;

/// <summary>Чтение сотрудников с учётом прав: руководитель видит только своих.</summary>
internal sealed class EmployeeReader(IAppDbContext db, IIdentityService identity, ICurrentUser currentUser)
{
    public IQueryable<Employee> Visible() =>
        currentUser.IsAdmin ? db.Employees : db.Employees.Where(e => e.ManagerUserId == currentUser.UserId);

    public async Task<IReadOnlyList<EmployeeDto>> ToDtosAsync(IQueryable<Employee> query, CancellationToken cancellationToken)
    {
        var rows = await (
                from e in query
                join t in db.Tracks on e.TrackId equals t.Id
                join g in db.Grades on e.CurrentGradeId equals g.Id
                orderby e.FullName
                select new { e, TrackName = t.Name, GradeCode = g.Code, GradeName = g.Name })
            .ToListAsync(cancellationToken);

        var managerNames = await identity.GetUserNamesAsync([.. rows.Select(r => r.e.ManagerUserId).Distinct()], cancellationToken);

        return [.. rows.Select(r => new EmployeeDto(
            r.e.Id,
            r.e.FullName,
            r.e.Email,
            r.e.TrackId,
            r.TrackName,
            r.e.CurrentGradeId,
            r.GradeCode,
            r.GradeName,
            r.e.ManagerUserId,
            managerNames.GetValueOrDefault(r.e.ManagerUserId, "—"),
            r.e.IsActive))];
    }

    public async Task<EmployeeDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await ToDtosAsync(Visible().Where(e => e.Id == id), cancellationToken)).SingleOrDefault()
            ?? throw new NotFoundException(nameof(Employee), id);
}
