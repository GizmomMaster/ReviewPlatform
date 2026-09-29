using System.Net;
using System.Net.Http.Json;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Application.Surveys;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Api.Tests;

public sealed class SurveyEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Open_ReturnsQuestionsWithoutGrades_AndMarksOpened()
    {
        var (manager, session, links) = await LaunchedSessionAsync();
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(SurveyUrl(links[0]), Ct);
        var json = await response.Content.ReadAsStringAsync(Ct);
        var survey = (await response.Content.ReadFromJsonAsync<SurveyDto>(ApiFactory.Json, Ct))!;

        Assert.Equal(SurveyState.Open, survey.State);
        Assert.Equal(7, survey.Groups.Count);
        Assert.Equal(28, survey.Groups.Sum(g => g.Indicators.Count)); // подтверждение E3
        Assert.DoesNotContain("E3", json, StringComparison.Ordinal);
        Assert.DoesNotContain("grade", json, StringComparison.OrdinalIgnoreCase);

        var details = await manager.GetFromJsonAsync<SessionDetailsDto>($"/api/assessment-sessions/{session.Id}", ApiFactory.Json, Ct);
        Assert.Equal(ParticipantStatus.InProgress, details!.Participants.Single(p => p.Id == links[0].ParticipantId).Status);
    }

    [Fact]
    public async Task Get_UnknownToken_Returns404()
    {
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/surveys/not-a-real-token", Ct)).StatusCode);
    }

    [Fact]
    public async Task Draft_IsReturnedOnReload()
    {
        var (_, _, links) = await LaunchedSessionAsync();
        using var anonymous = factory.CreateClient();
        var survey = await GetSurveyAsync(anonymous, links[0]);
        var first = survey.Groups[0].Indicators[0].Id;
        var second = survey.Groups[0].Indicators[1].Id;

        await SaveDraftAsync(anonymous, links[0], [new(first, 1, false, "Пример"), new(second, null, true, null)]);
        await SaveDraftAsync(anonymous, links[0], [new(first, 2, false, null)]);

        var reloaded = await GetSurveyAsync(anonymous, links[0]);
        Assert.Equal(2, reloaded.Answers.Count);
        Assert.Equal(new SurveyAnswerDto(first, 2, false, null), reloaded.Answers.Single(a => a.IndicatorId == first));
        Assert.True(reloaded.Answers.Single(a => a.IndicatorId == second).NotApplicable);
    }

    [Fact]
    public async Task Submit_Incomplete_Returns400_KeyedByIndicator()
    {
        var (_, _, links) = await LaunchedSessionAsync();
        using var anonymous = factory.CreateClient();
        var survey = await GetSurveyAsync(anonymous, links[0]);
        var ids = AllIds(survey);

        var response = await anonymous.PostAsJsonAsync($"{SurveyUrl(links[0])}/submit",
            new SurveyAnswersRequest([new(ids[0], 3, false, null), new(ids[1], 2, false, null)]), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblem>(ApiFactory.Json, Ct))!;
        Assert.Contains(ids[0].ToString(), problem.Errors.Keys); // 3 без комментария
        Assert.DoesNotContain(ids[1].ToString(), problem.Errors.Keys);
        Assert.Equal(ids.Count - 1, problem.Errors.Count);
    }

    [Fact]
    public async Task Submit_Complete_ThenSurveyIsReadOnly()
    {
        var (_, _, links) = await LaunchedSessionAsync();
        using var anonymous = factory.CreateClient();
        var survey = await GetSurveyAsync(anonymous, links[0]);

        await SubmitAllAsync(anonymous, links[0], survey);

        Assert.Equal(SurveyState.Submitted, (await GetSurveyAsync(anonymous, links[0])).State);
        var draft = await anonymous.PutAsJsonAsync($"{SurveyUrl(links[0])}/draft", new SurveyAnswersRequest([]), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
    }

    [Fact]
    public async Task SubmitByAll_Concurrently_CompletesSessionExactlyOnce()
    {
        var (manager, session, links) = await LaunchedSessionAsync();
        using var anonymous = factory.CreateClient();
        var surveys = new List<SurveyDto>();
        foreach (var link in links) surveys.Add(await GetSurveyAsync(anonymous, link));

        await Task.WhenAll(links.Select((link, i) => SubmitAllAsync(factory.CreateClient(), link, surveys[i])));

        var details = await manager.GetFromJsonAsync<SessionDetailsDto>($"/api/assessment-sessions/{session.Id}", ApiFactory.Json, Ct);
        Assert.Equal(SessionStatus.AwaitingDecision, details!.Status);
        Assert.NotNull(details.CompletedAtUtc);
        Assert.All(details.Participants, p => Assert.Equal(ParticipantStatus.Submitted, p.Status));
    }

    [Fact]
    public async Task ReissuedLink_OldTokenStopsWorking_DraftIsKept()
    {
        var (manager, session, links) = await LaunchedSessionAsync();
        using var anonymous = factory.CreateClient();
        var survey = await GetSurveyAsync(anonymous, links[1]);
        await SaveDraftAsync(anonymous, links[1], [new(AllIds(survey)[0], 1, false, null)]);

        var reissued = await manager.PostAsync($"/api/assessment-sessions/{session.Id}/participants/{links[1].ParticipantId}/reissue-link", null, Ct);
        var newLink = (await reissued.Content.ReadFromJsonAsync<ParticipantLinkDto>(ApiFactory.Json, Ct))!;

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(SurveyUrl(links[1]), Ct)).StatusCode);
        Assert.Single((await GetSurveyAsync(anonymous, newLink)).Answers);
    }

    [Fact]
    public async Task CancelledSession_SurveyIsClosed()
    {
        var (manager, session, links) = await LaunchedSessionAsync();
        (await manager.PostAsync($"/api/assessment-sessions/{session.Id}/cancel", null, Ct)).EnsureSuccessStatusCode();
        using var anonymous = factory.CreateClient();

        var survey = await GetSurveyAsync(anonymous, links[0]);

        Assert.Equal(SurveyState.Closed, survey.State);
        Assert.Empty(survey.Groups);
    }

    private async Task<(HttpClient Manager, SessionDetailsDto Session, List<ParticipantLinkDto> Links)> LaunchedSessionAsync()
    {
        var (_, manager) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var track = (await manager.GetFromJsonAsync<List<TrackDto>>("/api/tracks", ApiFactory.Json, Ct))!.Single();
        var grade = (await manager.GetFromJsonAsync<List<GradeDto>>("/api/grades", ApiFactory.Json, Ct))!.Single(g => g.Code == "E3");
        var employee = await (await manager.PostAsJsonAsync("/api/employees",
            new CreateEmployeeCommand("Анкетный Сотрудник", $"emp-{Guid.NewGuid():N}@test.local", track.Id, grade.Id, null), ApiFactory.Json, Ct))
            .Content.ReadFromJsonAsync<EmployeeDto>(ApiFactory.Json, Ct);

        var created = await manager.PostAsJsonAsync("/api/assessment-sessions", new CreateSessionCommand(employee!.Id, SessionType.Confirmation,
            DateTime.UtcNow.AddDays(7),
            [new("Коллега", "peer@test.local", EvaluatorRole.Peer), new("Лид", "lead@test.local", EvaluatorRole.TeamLead), new("Менеджер", "m@test.local", EvaluatorRole.Manager)]),
            ApiFactory.Json, Ct);
        var session = (await created.Content.ReadFromJsonAsync<SessionDetailsDto>(ApiFactory.Json, Ct))!;
        var launched = await manager.PostAsync($"/api/assessment-sessions/{session.Id}/launch", null, Ct);
        var links = (await launched.Content.ReadFromJsonAsync<List<ParticipantLinkDto>>(ApiFactory.Json, Ct))!;
        return (manager, session, links);
    }

    private static string SurveyUrl(ParticipantLinkDto link) => $"/api/surveys/{link.Url[(link.Url.LastIndexOf('/') + 1)..]}";

    private static List<Guid> AllIds(SurveyDto survey) => [.. survey.Groups.SelectMany(g => g.Indicators).Select(i => i.Id)];

    private static async Task<SurveyDto> GetSurveyAsync(HttpClient client, ParticipantLinkDto link) =>
        (await client.GetFromJsonAsync<SurveyDto>(SurveyUrl(link), ApiFactory.Json, Ct))!;

    private static async Task SaveDraftAsync(HttpClient client, ParticipantLinkDto link, IReadOnlyList<SurveyAnswerDto> answers) =>
        (await client.PutAsJsonAsync($"{SurveyUrl(link)}/draft", new SurveyAnswersRequest(answers), ApiFactory.Json, Ct)).EnsureSuccessStatusCode();

    private static async Task SubmitAllAsync(HttpClient client, ParticipantLinkDto link, SurveyDto survey)
    {
        var answers = AllIds(survey).Select(id => new SurveyAnswerDto(id, 2, false, null)).ToList();
        var response = await client.PostAsJsonAsync($"{SurveyUrl(link)}/submit", new SurveyAnswersRequest(answers), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
