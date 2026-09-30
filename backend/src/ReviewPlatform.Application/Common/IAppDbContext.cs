using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Audit;
using ReviewPlatform.Domain.Employees;
using ReviewPlatform.Domain.Matrix;
using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Application.Common;

public interface IAppDbContext
{
    DbSet<Track> Tracks { get; }
    DbSet<Grade> Grades { get; }
    DbSet<CompetencyGroup> CompetencyGroups { get; }
    DbSet<Indicator> Indicators { get; }
    DbSet<GradeRoleRule> GradeRoleRules { get; }
    DbSet<Employee> Employees { get; }
    DbSet<AssessmentSession> AssessmentSessions { get; }
    DbSet<Participant> Participants { get; }
    DbSet<SessionIndicator> SessionIndicators { get; }
    DbSet<SurveyAnswer> SurveyAnswers { get; }
    DbSet<AssessmentDecision> AssessmentDecisions { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<OutboxEmail> EmailOutbox { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
