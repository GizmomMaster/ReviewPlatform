namespace ReviewPlatform.Infrastructure.Identity;

/// <summary>Refresh-токен (в БД — только SHA-256). Одноразовый: при обновлении отзывается и заменяется новым.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public string TokenHash { get; init; } = null!;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
