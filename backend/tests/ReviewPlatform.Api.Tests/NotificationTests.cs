using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Notifications;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Application.Surveys;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Audit;
using ReviewPlatform.Domain.Matrix;
using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Api.Tests;

public sealed partial class NotificationTests(ApiFactory factory)
{
    private readonly Scenarios _scenarios = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Launch_QueuesInvitationWithWorkingLink_ToEveryRespondent()
    {
        var s = await _scenarios.LaunchedTransitionAsync();

        foreach (var link in s.Links)
        {
            var participant = s.Session.Participants.Single(p => p.Id == link.ParticipantId);
            var email = Assert.Single(await factory.OutboxAsync(participant.Email));
            Assert.Equal((EmailType.Invitation, OutboxStatus.Pending), (email.Type, email.Status));
            Assert.Equal(link.Url, SurveyUrl(email));
            Assert.StartsWith("[360 Review]", email.Subject, StringComparison.Ordinal);
        }

        var peerEmail = (await factory.OutboxAsync(s.Email(EvaluatorRole.Peer))).Single();
        Assert.Contains(s.Employee.FullName, peerEmail.Body, StringComparison.Ordinal);
        Assert.Contains("не анонимна", peerEmail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddParticipant_AfterLaunch_QueuesInvitation()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        var address = Scenarios.UniqueEmail("peer2");

        var response = await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/participants",
            new AddParticipantRequest("Второй Коллега", address, EvaluatorRole.Peer), ApiFactory.Json, Ct);

        var result = (await response.Content.ReadFromJsonAsync<AddParticipantResult>(ApiFactory.Json, Ct))!;
        var email = Assert.Single(await factory.OutboxAsync(address));
        Assert.Equal(EmailType.Invitation, email.Type);
        Assert.Equal(result.Link!.Url, SurveyUrl(email));
    }

    [Fact]
    public async Task ResendInvite_SendsNewLink_AndOldLinkStopsWorking()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        var peer = s.Link(EvaluatorRole.Peer);

        var response = await s.Manager.PostAsync($"/api/assessment-sessions/{s.Session.Id}/participants/{peer.ParticipantId}/resend-invite", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var resent = (await factory.OutboxAsync(s.Email(EvaluatorRole.Peer))).Last();
        Assert.Equal(EmailType.LinkReissued, resent.Type);
        Assert.NotEqual(peer.Url, SurveyUrl(resent));
        Assert.Equal(HttpStatusCode.NotFound, (await GetSurveyAsync(peer.Url)).StatusCode);
        Assert.Equal(SurveyState.Open, (await SurveyAsync(SurveyUrl(resent))).State);
        Assert.Contains(await AuditActionsAsync(s), a => a == AuditActions.InviteResent);
    }

    [Fact]
    public async Task ReissueLink_AlsoEmailsTheNewLink()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        var lead = s.Link(EvaluatorRole.TeamLead);

        var response = await s.Manager.PostAsync($"/api/assessment-sessions/{s.Session.Id}/participants/{lead.ParticipantId}/reissue-link", null, Ct);

        var link = (await response.Content.ReadFromJsonAsync<ParticipantLinkDto>(ApiFactory.Json, Ct))!;
        var email = (await factory.OutboxAsync(s.Email(EvaluatorRole.TeamLead))).Last();
        Assert.Equal((EmailType.LinkReissued, link.Url), (email.Type, SurveyUrl(email)));
    }

    [Fact]
    public async Task LastSubmission_QueuesCompletionEmailToOwner()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        var ownerEmail = await factory.UserEmailAsync(s.ManagerId);

        foreach (var link in s.Links.SkipLast(1))
        {
            await _scenarios.SubmitAsync(link, 2);
        }

        Assert.Empty(await factory.OutboxAsync(ownerEmail));
        await _scenarios.SubmitAsync(s.Links.Last(), 2);

        var email = Assert.Single(await factory.OutboxAsync(ownerEmail));
        Assert.Equal(EmailType.SurveyCompleted, email.Type);
        Assert.Contains($"Все участники завершили опрос по сотруднику {s.Employee.FullName}", email.Subject, StringComparison.Ordinal);
        Assert.Contains($"/admin/sessions/{s.Session.Id}/report", email.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_LaunchedSession_NotifiesRespondents()
    {
        var s = await _scenarios.LaunchedTransitionAsync();

        await s.Manager.PostAsync($"/api/assessment-sessions/{s.Session.Id}/cancel", null, Ct);

        foreach (var participant in s.Session.Participants)
        {
            Assert.Equal(EmailType.SessionCancelled, (await factory.OutboxAsync(participant.Email)).Last().Type);
        }
    }

    // ---------- Сроки ----------

    [Fact]
    public async Task DeadlinePassed_MarksOverdue_EmailsOwnerOnce_AndClosesSurvey()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAsync(s.Link(EvaluatorRole.Peer), 2);
        await MoveDeadlineAsync(s, DateTime.UtcNow.AddMinutes(-1));
        var ownerEmail = await factory.UserEmailAsync(s.ManagerId);

        Assert.True(await factory.SendAsync(new ProcessOverdueSessionsCommand()) >= 1);
        await factory.SendAsync(new ProcessOverdueSessionsCommand());

        var details = await s.Manager.GetFromJsonAsync<SessionDetailsDto>($"/api/assessment-sessions/{s.Session.Id}", ApiFactory.Json, Ct);
        Assert.Equal(SessionStatus.Overdue, details!.Status);
        var email = Assert.Single(await factory.OutboxAsync(ownerEmail));
        Assert.Equal(EmailType.DeadlinePassed, email.Type);
        Assert.Contains("Лид Сценариев", email.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Коллега Сценариев", email.Body, StringComparison.Ordinal);
        Assert.Equal(SurveyState.Closed, (await SurveyAsync(s.Link(EvaluatorRole.TeamLead).Url)).State);
        Assert.Contains(await AuditActionsAsync(s), a => a == AuditActions.SessionOverdue);
    }

    [Fact]
    public async Task Extend_Overdue_ReopensSurvey_AndSendsNewLinksOnlyToPending()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAsync(s.Link(EvaluatorRole.Peer), 2);
        await MoveDeadlineAsync(s, DateTime.UtcNow.AddMinutes(-1));
        await factory.SendAsync(new ProcessOverdueSessionsCommand());
        var newDeadline = DateTime.UtcNow.AddDays(5);

        var response = await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/extend", new ExtendSessionRequest(newDeadline), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = (await response.Content.ReadFromJsonAsync<SessionDetailsDto>(ApiFactory.Json, Ct))!;
        Assert.Equal(SessionStatus.InProgress, details.Status);
        Assert.Equal(EmailType.Invitation, (await factory.OutboxAsync(s.Email(EvaluatorRole.Peer))).Last().Type);

        var lead = s.Link(EvaluatorRole.TeamLead);
        var extended = (await factory.OutboxAsync(s.Email(EvaluatorRole.TeamLead))).Last();
        Assert.Equal(EmailType.DeadlineExtended, extended.Type);
        Assert.Equal(HttpStatusCode.NotFound, (await GetSurveyAsync(lead.Url)).StatusCode);
        Assert.Equal(SurveyState.Open, (await SurveyAsync(SurveyUrl(extended))).State);
    }

    [Fact]
    public async Task Extend_WithPastDeadline_Returns409()
    {
        var s = await _scenarios.LaunchedTransitionAsync();

        var response = await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/extend",
            new ExtendSessionRequest(DateTime.UtcNow.AddDays(-1)), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reminders_GoToPendingRespondentsOnce()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAsync(s.Link(EvaluatorRole.Peer), 2);
        // Опрос запущен неделю назад, до дедлайна два дня — наступила точка «за 3 дня»
        await MoveDeadlineAsync(s, DateTime.UtcNow.AddDays(2), linksIssuedAtUtc: DateTime.UtcNow.AddDays(-7));

        await factory.SendAsync(new SendRemindersCommand());
        await factory.SendAsync(new SendRemindersCommand());

        Assert.Equal(EmailType.Invitation, (await factory.OutboxAsync(s.Email(EvaluatorRole.Peer))).Last().Type);
        foreach (var role in new[] { EvaluatorRole.Self, EvaluatorRole.TeamLead, EvaluatorRole.Manager })
        {
            var emails = await factory.OutboxAsync(s.Email(role));
            var reminder = Assert.Single(emails, e => e.Type == EmailType.Reminder);
            Assert.Equal(SurveyState.Open, (await SurveyAsync(SurveyUrl(reminder))).State);
        }

        Assert.Contains(await AuditActionsAsync(s), a => a == AuditActions.RemindersSent);
    }

    // ---------- Отправка ----------

    [Fact]
    public async Task Dispatcher_SendsPendingEmails_AndRetriesFailures()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        var failing = $"fail-{Guid.NewGuid():N}@test.local";
        await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/participants",
            new AddParticipantRequest("Недоступный Коллега", failing, EvaluatorRole.Peer), ApiFactory.Json, Ct);

        // Outbox общий для параллельных тестов — обрабатываем, пока не дойдёт до писем этого теста
        for (var i = 0; i < 50 && (await factory.OutboxAsync(s.Email(EvaluatorRole.Manager))).Single().Status == OutboxStatus.Pending; i++)
        {
            await factory.ProcessOutboxBatchAsync();
        }

        var sent = (await factory.OutboxAsync(s.Email(EvaluatorRole.Manager))).Single();
        Assert.Equal((OutboxStatus.Sent, 1), (sent.Status, sent.Attempts));
        Assert.Contains(factory.Mail.Sent, m => m.To == sent.To && m.Subject == sent.Subject);

        var failed = await WaitForAttemptAsync(failing);
        Assert.Equal(OutboxStatus.Pending, failed.Status);
        Assert.Equal("SMTP недоступен", failed.LastError);
        Assert.True(failed.NextAttemptAtUtc > DateTime.UtcNow.AddSeconds(30));
    }

    private async Task<OutboxEmail> WaitForAttemptAsync(string to)
    {
        for (var i = 0; i < 50; i++)
        {
            var email = (await factory.OutboxAsync(to)).Single();
            if (email.Attempts > 0)
            {
                return email;
            }

            await factory.ProcessOutboxBatchAsync();
        }

        throw new TimeoutException($"Email to {to} was not processed.");
    }

    // ---------- Вспомогательное ----------

    /// <summary>Сдвигает дедлайн в БД: через API дедлайн в прошлом не поставить, а тестам нужно «время прошло».</summary>
    private Task MoveDeadlineAsync(Scenarios.LaunchedSession s, DateTime deadlineAtUtc, DateTime? linksIssuedAtUtc = null) =>
        factory.WithDbAsync(async db =>
        {
            await db.AssessmentSessions.Where(x => x.Id == s.Session.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.DeadlineAtUtc, deadlineAtUtc), Ct);
            if (linksIssuedAtUtc is { } issued)
            {
                await db.Participants.Where(p => p.SessionId == s.Session.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(p => p.TokenIssuedAtUtc, issued), Ct);
            }
        });

    private static async Task<List<string>> AuditActionsAsync(Scenarios.LaunchedSession s) =>
        [.. (await s.Manager.GetFromJsonAsync<List<AuditEntryDto>>($"/api/assessment-sessions/{s.Session.Id}/audit", ApiFactory.Json, Ct))!.Select(e => e.Action)];

    private static string SurveyUrl(OutboxEmail email) => SurveyLink().Match(email.Body) is { Success: true } m
        ? m.Value
        : throw new InvalidOperationException($"No survey link in email {email.Subject}");

    private static string ApiPath(string surveyUrl) => $"/api/surveys/{surveyUrl[(surveyUrl.LastIndexOf('/') + 1)..]}";

    private async Task<HttpResponseMessage> GetSurveyAsync(string surveyUrl)
    {
        using var client = factory.CreateClient();
        return await client.GetAsync(ApiPath(surveyUrl), Ct);
    }

    private async Task<SurveyDto> SurveyAsync(string surveyUrl)
    {
        using var client = factory.CreateClient();
        return (await client.GetFromJsonAsync<SurveyDto>(ApiPath(surveyUrl), ApiFactory.Json, Ct))!;
    }

    [GeneratedRegex(@"http://localhost:8080/survey/[A-Za-z0-9_\-]+")]
    private static partial Regex SurveyLink();
}
