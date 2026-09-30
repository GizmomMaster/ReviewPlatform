using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Infrastructure.Persistence.Configurations;

internal sealed class OutboxEmailConfiguration : IEntityTypeConfiguration<OutboxEmail>
{
    public void Configure(EntityTypeBuilder<OutboxEmail> builder)
    {
        builder.ToTable("email_outbox");
        builder.Property(e => e.To).HasMaxLength(256);
        builder.Property(e => e.Subject).HasMaxLength(OutboxEmail.MaxSubjectLength);
        builder.Property(e => e.LastError).HasMaxLength(OutboxEmail.MaxErrorLength);

        // Выборка отправщика: только ожидающие, по времени следующей попытки
        builder.HasIndex(e => e.NextAttemptAtUtc).HasFilter("status = 'Pending'");
    }
}
