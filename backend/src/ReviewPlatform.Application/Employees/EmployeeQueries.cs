using Microsoft.EntityFrameworkCore;
using MediatR;

namespace ReviewPlatform.Application.Employees;

public sealed record ListEmployeesQuery(string? Search, bool IncludeArchived) : IRequest<IReadOnlyList<EmployeeDto>>;

internal sealed class ListEmployeesHandler(EmployeeReader reader) : IRequestHandler<ListEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    public Task<IReadOnlyList<EmployeeDto>> Handle(ListEmployeesQuery request, CancellationToken cancellationToken)
    {
        var query = reader.Visible();
        if (!request.IncludeArchived)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim().ToLowerInvariant()}%";
            // ToLower() транслируется в SQL lower(), культура .NET здесь не участвует
#pragma warning disable CA1304, CA1311
            query = query.Where(e => EF.Functions.Like(e.FullName.ToLower(), pattern) || EF.Functions.Like(e.Email, pattern));
#pragma warning restore CA1304, CA1311
        }

        return reader.ToDtosAsync(query, cancellationToken);
    }
}

public sealed record GetEmployeeQuery(Guid Id) : IRequest<EmployeeDto>;

internal sealed class GetEmployeeHandler(EmployeeReader reader) : IRequestHandler<GetEmployeeQuery, EmployeeDto>
{
    public Task<EmployeeDto> Handle(GetEmployeeQuery request, CancellationToken cancellationToken) =>
        reader.GetAsync(request.Id, cancellationToken);
}
