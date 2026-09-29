using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Employees;
using ReviewPlatform.Domain.Matrix;
using ReviewPlatform.Infrastructure.Identity;

namespace ReviewPlatform.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<CompetencyGroup> CompetencyGroups => Set<CompetencyGroup>();
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<GradeRoleRule> GradeRoleRules => Set<GradeRoleRule>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
}
