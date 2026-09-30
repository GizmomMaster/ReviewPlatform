namespace ReviewPlatform.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "review-platform";
    public string Audience { get; set; } = "review-platform";

    /// <summary>Секрет HMAC-SHA256, не короче 32 байт. Задаётся только через конфигурацию окружения.</summary>
    public string SigningKey { get; set; } = "";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}

public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string FullName { get; set; } = "Администратор";
}
