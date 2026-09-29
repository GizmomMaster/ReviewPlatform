namespace ReviewPlatform.Application.Common;

public interface ICurrentUser
{
    /// <summary>Id пользователя. Бросает исключение, если запрос анонимный.</summary>
    Guid UserId { get; }

    bool IsAdmin { get; }
}
