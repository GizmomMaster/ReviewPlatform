using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Infrastructure.Persistence.Configurations;

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.Property(t => t.Code).HasMaxLength(64);
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.HasIndex(t => t.Code).IsUnique();
    }
}

internal sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.Property(g => g.Code).HasMaxLength(16);
        builder.Property(g => g.Name).HasMaxLength(100);
        builder.HasIndex(g => g.Code).IsUnique();
        builder.HasIndex(g => g.Order).IsUnique();
    }
}

internal sealed class CompetencyGroupConfiguration : IEntityTypeConfiguration<CompetencyGroup>
{
    public void Configure(EntityTypeBuilder<CompetencyGroup> builder)
    {
        builder.Property(g => g.Name).HasMaxLength(200);
        builder.Property(g => g.Description).HasMaxLength(2000);
        builder.HasOne<Track>().WithMany().HasForeignKey(g => g.TrackId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => new { g.TrackId, g.Name }).IsUnique();
        builder.HasMany(g => g.Indicators).WithOne().HasForeignKey(i => i.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Indicators).HasField("_indicators");
    }
}

internal sealed class IndicatorConfiguration : IEntityTypeConfiguration<Indicator>
{
    public void Configure(EntityTypeBuilder<Indicator> builder)
    {
        builder.Property(i => i.Text).HasMaxLength(Indicator.MaxTextLength);
        builder.HasOne<Grade>().WithMany().HasForeignKey(i => i.GradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.GroupId, i.GradeId, i.Text }).IsUnique().HasFilter("NOT is_archived");
    }
}

internal sealed class GradeRoleRuleConfiguration : IEntityTypeConfiguration<GradeRoleRule>
{
    public void Configure(EntityTypeBuilder<GradeRoleRule> builder)
    {
        builder.HasOne<Grade>().WithMany().HasForeignKey(r => r.GradeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.GradeId, r.Role }).IsUnique();
    }
}
