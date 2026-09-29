using Microsoft.AspNetCore.Identity;

namespace ReviewPlatform.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    /// <summary>Временный пароль: до смены доступны только смена пароля и выход.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
