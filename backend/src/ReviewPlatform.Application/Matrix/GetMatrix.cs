using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;

namespace ReviewPlatform.Application.Matrix;

/// <summary>Матрица направления: группы с актуальными (неархивными) индикаторами. null — направление не найдено.</summary>
public sealed record GetMatrixQuery(Guid TrackId) : IRequest<MatrixDto?>;

internal sealed class GetMatrixHandler(IAppDbContext db) : IRequestHandler<GetMatrixQuery, MatrixDto?>
{
    public async Task<MatrixDto?> Handle(GetMatrixQuery request, CancellationToken cancellationToken)
    {
        var track = await db.Tracks
            .Where(t => t.Id == request.TrackId)
            .Select(t => new TrackDto(t.Id, t.Code, t.Name, t.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        if (track is null)
        {
            return null;
        }

        var grades = await db.Grades
            .OrderBy(g => g.Order)
            .Select(g => new GradeDto(g.Id, g.Code, g.Name, g.Order))
            .ToListAsync(cancellationToken);
        var gradeOrder = grades.ToDictionary(g => g.Id, g => g.Order);

        var groups = await db.CompetencyGroups
            .Where(g => g.TrackId == request.TrackId)
            .OrderBy(g => g.Order)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.Order,
                Indicators = g.Indicators
                    .Where(i => !i.IsArchived)
                    .Select(i => new IndicatorDto(i.Id, i.GradeId, i.Text, i.Order))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var groupDtos = groups
            .Select(g => new CompetencyGroupDto(
                g.Id,
                g.Name,
                g.Description,
                g.Order,
                [.. g.Indicators.OrderBy(i => gradeOrder[i.GradeId]).ThenBy(i => i.Order)]))
            .ToList();

        return new MatrixDto(track, grades, groupDtos);
    }
}
