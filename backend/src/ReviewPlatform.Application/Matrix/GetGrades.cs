using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;

namespace ReviewPlatform.Application.Matrix;

public sealed record GetGradesQuery : IRequest<IReadOnlyList<GradeDto>>;

internal sealed class GetGradesHandler(IAppDbContext db) : IRequestHandler<GetGradesQuery, IReadOnlyList<GradeDto>>
{
    public async Task<IReadOnlyList<GradeDto>> Handle(GetGradesQuery request, CancellationToken cancellationToken) =>
        await db.Grades
            .OrderBy(g => g.Order)
            .Select(g => new GradeDto(g.Id, g.Code, g.Name, g.Order))
            .ToListAsync(cancellationToken);
}
