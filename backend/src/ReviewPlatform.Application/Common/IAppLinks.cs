namespace ReviewPlatform.Application.Common;

/// <summary>Публичные адреса фронтенда ({FrontendBaseUrl}/…) для ссылок в ответах API и письмах.</summary>
public interface IAppLinks
{
    /// <summary>Анкета респондента: /survey/{token}.</summary>
    string Survey(string token);

    /// <summary>Карточка сессии в админке.</summary>
    string Session(Guid sessionId);

    /// <summary>Отчёт по сессии.</summary>
    string Report(Guid sessionId);
}
