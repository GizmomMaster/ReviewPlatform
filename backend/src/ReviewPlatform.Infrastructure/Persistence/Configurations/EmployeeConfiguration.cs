using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReviewPlatform.Domain.Employees;
using ReviewPlatform.Domain.Matrix;
using ReviewPlatform.Infrastructure.Identity;

namespace ReviewPlatform.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.FullName).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.HasIndex(e => e.Email).IsUnique();
        builder.HasIndex(e => e.ManagerUserId);
        builder.HasOne<Track>().WithMany().HasForeignKey(e => e.TrackId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Grade>().WithMany().HasForeignKey(e => e.CurrentGradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
