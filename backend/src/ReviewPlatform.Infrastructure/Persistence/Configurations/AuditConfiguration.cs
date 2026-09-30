using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReviewPlatform.Domain.Audit;

namespace ReviewPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.Property(e => e.Action).HasMaxLength(64);
        builder.Property(e => e.EntityType).HasMaxLength(64);
        builder.Property(e => e.Details).HasMaxLength(2000);
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAtUtc });
    }
}
