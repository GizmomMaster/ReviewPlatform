namespace ReviewPlatform.Application.Common;

public interface ISurveyLinks
{
    /// <summary>Публичная ссылка на анкету: {FrontendBaseUrl}/survey/{token}.</summary>
    string Build(string token);
}
