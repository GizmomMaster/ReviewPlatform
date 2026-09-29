using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Common;

public interface IAppDbContext
{
    DbSet<Track> Tracks { get; }
    DbSet<Grade> Grades { get; }
    DbSet<CompetencyGroup> CompetencyGroups { get; }
    DbSet<Indicator> Indicators { get; }
    DbSet<GradeRoleRule> GradeRoleRules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
