using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<CompetencyGroup> CompetencyGroups => Set<CompetencyGroup>();
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<GradeRoleRule> GradeRoleRules => Set<GradeRoleRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
}
