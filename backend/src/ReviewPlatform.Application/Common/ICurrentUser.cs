namespace ReviewPlatform.Application.Common;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id пользователя. Бросает исключение, если запрос анонимный.</summary>
    Guid UserId { get; }

    bool IsAdmin { get; }
}
