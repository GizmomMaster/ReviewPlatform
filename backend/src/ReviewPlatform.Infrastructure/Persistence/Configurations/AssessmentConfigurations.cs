using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Employees;
using ReviewPlatform.Domain.Matrix;
using ReviewPlatform.Infrastructure.Identity;

namespace ReviewPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentSessionConfiguration : IEntityTypeConfiguration<AssessmentSession>
{
    public void Configure(EntityTypeBuilder<AssessmentSession> builder)
    {
        builder.Property(s => s.Version).IsRowVersion(); // xmin
        builder.Ignore(s => s.PendingParticipants);

        builder.HasOne<Employee>().WithMany().HasForeignKey(s => s.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Track>().WithMany().HasForeignKey(s => s.TrackId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Grade>().WithMany().HasForeignKey(s => s.CurrentGradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Grade>().WithMany().HasForeignKey(s => s.TargetGradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.OwnerUserId).OnDelete(DeleteBehavior.Restrict);

        // Не больше одной незавершённой сессии на сотрудника — защита от гонки поверх проверки в приложении
        var active = string.Join(", ", AssessmentSession.ActiveStatuses.Select(s => $"'{s}'"));
        builder.HasIndex(s => s.EmployeeId).IsUnique().HasFilter($"status IN ({active})").HasDatabaseName("ux_assessment_sessions_active_employee");
        builder.HasIndex(s => new { s.OwnerUserId, s.Status });

        builder.HasMany(s => s.Participants).WithOne().HasForeignKey(p => p.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Participants).HasField("_participants");
        builder.HasMany(s => s.Indicators).WithOne().HasForeignKey(i => i.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Indicators).HasField("_indicators");
        builder.HasOne(s => s.Decision).WithOne().HasForeignKey<AssessmentDecision>(d => d.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AssessmentDecisionConfiguration : IEntityTypeConfiguration<AssessmentDecision>
{
    public void Configure(EntityTypeBuilder<AssessmentDecision> builder)
    {
        builder.Property(d => d.Comment).HasMaxLength(AssessmentDecision.MaxCommentLength);
        builder.HasIndex(d => d.SessionId).IsUnique();
        builder.HasOne<Grade>().WithMany().HasForeignKey(d => d.NewGradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(d => d.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.PlanItems).WithOne().HasForeignKey(p => p.DecisionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.PlanItems).HasField("_planItems");
    }
}

internal sealed class DevelopmentPlanItemConfiguration : IEntityTypeConfiguration<DevelopmentPlanItem>
{
    public void Configure(EntityTypeBuilder<DevelopmentPlanItem> builder)
    {
        builder.Property(p => p.Text).HasMaxLength(DevelopmentPlanItem.MaxTextLength);
        builder.HasOne<SessionIndicator>().WithMany().HasForeignKey(p => p.SessionIndicatorId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.Property(p => p.Version).IsRowVersion(); // xmin
        builder.Property(p => p.FullName).HasMaxLength(200);
        builder.Property(p => p.Email).HasMaxLength(256);
        builder.Property(p => p.TokenHash).HasMaxLength(64);
        builder.HasIndex(p => p.TokenHash).IsUnique().HasFilter("token_hash IS NOT NULL");
        builder.Ignore(p => p.IsActive);
        builder.HasMany(p => p.Answers).WithOne().HasForeignKey(a => a.ParticipantId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Answers).HasField("_answers");
    }
}

internal sealed class SurveyAnswerConfiguration : IEntityTypeConfiguration<SurveyAnswer>
{
    public void Configure(EntityTypeBuilder<SurveyAnswer> builder)
    {
        builder.Property(a => a.Comment).HasMaxLength(SurveyAnswer.MaxCommentLength);
        builder.HasIndex(a => new { a.ParticipantId, a.SessionIndicatorId }).IsUnique();
        builder.HasOne<SessionIndicator>().WithMany().HasForeignKey(a => a.SessionIndicatorId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(a => a.IsAnswered);
        builder.Ignore(a => a.RequiresComment);
    }
}

internal sealed class SessionIndicatorConfiguration : IEntityTypeConfiguration<SessionIndicator>
{
    public void Configure(EntityTypeBuilder<SessionIndicator> builder)
    {
        builder.Property(i => i.GroupName).HasMaxLength(200);
        builder.Property(i => i.GradeCode).HasMaxLength(16);
        builder.Property(i => i.Text).HasMaxLength(Indicator.MaxTextLength);
    }
}
