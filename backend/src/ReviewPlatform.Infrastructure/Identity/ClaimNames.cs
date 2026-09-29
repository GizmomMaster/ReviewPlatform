namespace ReviewPlatform.Infrastructure.Identity;

public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";

    /// <summary>Присутствует, пока пользователь не сменил временный пароль.</summary>
    public const string PasswordChangeRequired = "pwd_change";
}
