using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;

namespace ReviewPlatform.Application.Matrix;

public sealed record GetTracksQuery : IRequest<IReadOnlyList<TrackDto>>;

internal sealed class GetTracksHandler(IAppDbContext db) : IRequestHandler<GetTracksQuery, IReadOnlyList<TrackDto>>
{
    public async Task<IReadOnlyList<TrackDto>> Handle(GetTracksQuery request, CancellationToken cancellationToken) =>
        await db.Tracks
            .OrderBy(t => t.Name)
            .Select(t => new TrackDto(t.Id, t.Code, t.Name, t.IsActive))
            .ToListAsync(cancellationToken);
}
