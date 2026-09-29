using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Application.Notifications;

/// <summary>Данные письма. Тема и HTML собираются из шаблона в <see cref="IEmailRenderer"/>.</summary>
public abstract record EmailMessage(EmailType Type, string To);

/// <summary>
/// Письмо со ссылкой на анкету: приглашение, перевыпуск ссылки, напоминание, продление дедлайна
/// (<see cref="EmailType.Invitation"/>, <see cref="EmailType.LinkReissued"/>, <see cref="EmailType.Reminder"/>, <see cref="EmailType.DeadlineExtended"/>).
/// </summary>
public sealed record SurveyLinkEmail(
    EmailType Kind,
    string To,
    string RespondentName,
    string EmployeeName,
    string RoleName,
    bool IsSelf,
    DateTime DeadlineAtUtc,
    string SurveyUrl) : EmailMessage(Kind, To);

/// <summary>Все участники отправили анкеты — владельцу сессии.</summary>
public sealed record SurveyCompletedEmail(
    string To,
    string OwnerName,
    string EmployeeName,
    DateTime CompletedAtUtc,
    int SubmittedCount,
    string ReportUrl) : EmailMessage(EmailType.SurveyCompleted, To);

/// <summary>Дедлайн прошёл, не все отправили анкеты — владельцу сессии.</summary>
public sealed record DeadlinePassedEmail(
    string To,
    string OwnerName,
    string EmployeeName,
    DateTime DeadlineAtUtc,
    int SubmittedCount,
    IReadOnlyList<string> PendingRespondents,
    string SessionUrl) : EmailMessage(EmailType.DeadlinePassed, To);

/// <summary>Сессия отменена — респондентам запущенной сессии.</summary>
public sealed record SessionCancelledEmail(string To, string RespondentName, string EmployeeName)
    : EmailMessage(EmailType.SessionCancelled, To);

public sealed record RenderedEmail(string Subject, string HtmlBody);

public interface IEmailRenderer
{
    RenderedEmail Render(EmailMessage message);
}
