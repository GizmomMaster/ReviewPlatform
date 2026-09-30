using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Application.Notifications;

/// <summary>
/// Письма по событиям сессии (ТЗ, 8.8). Письмо только добавляется в outbox текущего контекста —
/// оно сохранится в той же транзакции, что и изменение сессии, а отправит его фоновый отправщик.
/// </summary>
internal sealed class SessionNotifier(IAppDbContext db, IEmailRenderer renderer, IAppLinks links, IIdentityService identity, TimeProvider time)
{
    /// <param name="kind">Invitation, LinkReissued, Reminder или DeadlineExtended.</param>
    /// <param name="tokens">Новые токены по Id респондента.</param>
    public async Task SurveyLinksAsync(AssessmentSession session, EmailType kind, IReadOnlyDictionary<Guid, AccessToken> tokens, CancellationToken cancellationToken)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        var employeeName = await EmployeeNameAsync(session, cancellationToken);
        foreach (var participant in session.Participants.Where(p => tokens.ContainsKey(p.Id)))
        {
            Enqueue(new SurveyLinkEmail(kind, participant.Email, participant.FullName, employeeName, AuditTexts.Role(participant.Role),
                participant.Role == Domain.Matrix.EvaluatorRole.Self, session.DeadlineAtUtc, links.Survey(tokens[participant.Id].Value)));
        }
    }

    public async Task SurveyCompletedAsync(AssessmentSession session, CancellationToken cancellationToken)
    {
        if (await identity.FindUserAsync(session.OwnerUserId, cancellationToken) is not { } owner)
        {
            return;
        }

        var submitted = session.Participants.Count(p => p.Status == ParticipantStatus.Submitted);
        Enqueue(new SurveyCompletedEmail(owner.Email, owner.FullName, await EmployeeNameAsync(session, cancellationToken),
            session.CompletedAtUtc ?? time.GetUtcNow().UtcDateTime, submitted, links.Report(session.Id)));
    }

    public async Task DeadlinePassedAsync(AssessmentSession session, CancellationToken cancellationToken)
    {
        if (await identity.FindUserAsync(session.OwnerUserId, cancellationToken) is not { } owner)
        {
            return;
        }

        var pending = session.PendingParticipants.OrderBy(p => p.Role).ThenBy(p => p.FullName).Select(AuditTexts.Participant).ToList();
        var submitted = session.Participants.Count(p => p.Status == ParticipantStatus.Submitted);
        Enqueue(new DeadlinePassedEmail(owner.Email, owner.FullName, await EmployeeNameAsync(session, cancellationToken),
            session.DeadlineAtUtc, submitted, pending, links.Session(session.Id)));
    }

    public async Task SessionCancelledAsync(AssessmentSession session, CancellationToken cancellationToken)
    {
        var employeeName = await EmployeeNameAsync(session, cancellationToken);
        foreach (var participant in session.Participants.Where(p => p.IsActive))
        {
            Enqueue(new SessionCancelledEmail(participant.Email, participant.FullName, employeeName));
        }
    }

    private Task<string> EmployeeNameAsync(AssessmentSession session, CancellationToken cancellationToken) =>
        db.Employees.Where(e => e.Id == session.EmployeeId).Select(e => e.FullName).SingleAsync(cancellationToken);

    private void Enqueue(EmailMessage message)
    {
        var email = renderer.Render(message);
        db.EmailOutbox.Add(new OutboxEmail(message.Type, message.To, email.Subject, email.HtmlBody, time.GetUtcNow().UtcDateTime));
    }
}
