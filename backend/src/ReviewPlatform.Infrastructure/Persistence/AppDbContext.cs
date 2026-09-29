using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Audit;
using ReviewPlatform.Domain.Common;
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
    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<SessionIndicator> SessionIndicators => Set<SessionIndicator>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();
    public DbSet<AssessmentDecision> AssessmentDecisions => Set<AssessmentDecision>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Id доменных сущностей генерируются в конструкторе (UUIDv7). Без этого EF считает новые сущности,
        // добавленные в коллекцию загруженного агрегата, существующими и пытается их обновить.
        foreach (var entity in builder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            builder.Entity(entity.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
}
