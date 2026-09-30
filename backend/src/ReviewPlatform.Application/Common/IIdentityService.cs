using ReviewPlatform.Application.Users;

namespace ReviewPlatform.Application.Common;

/// <summary>Учётные записи админки. Ошибки валидации паролей/email — FluentValidation.ValidationException.</summary>
public interface IIdentityService
{
    Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken cancellationToken);

    Task<UserDto?> FindUserAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid> CreateUserAsync(string email, string fullName, string role, string password, CancellationToken cancellationToken);

    Task UpdateUserAsync(Guid id, string fullName, string role, bool isActive, CancellationToken cancellationToken);

    /// <summary>Устанавливает временный пароль и требует сменить его при следующем входе.</summary>
    Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
