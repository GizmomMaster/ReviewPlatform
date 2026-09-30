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

/// <summary>Типовые шаги подготовки данных через публичный API.</summary>
internal sealed class Scenarios(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed record LaunchedSession(Guid ManagerId, HttpClient Manager, EmployeeDto Employee, SessionDetailsDto Session, List<ParticipantLinkDto> Links)
    {
        public ParticipantLinkDto Link(EvaluatorRole role) => Links.First(l => l.Role == role);

        public string Email(EvaluatorRole role) => Session.Participants.First(p => p.Role == role).Email;
    }

    public static async Task<EmployeeDto> CreateEmployeeAsync(HttpClient client, string gradeCode)
    {
        var track = (await client.GetFromJsonAsync<List<TrackDto>>("/api/tracks", ApiFactory.Json, Ct))!.Single(t => t.Code == "backend");
        var grade = await GradeAsync(client, gradeCode);
        var response = await client.PostAsJsonAsync("/api/employees",
            new CreateEmployeeCommand("Сотрудник Сценариев", $"emp-{Guid.NewGuid():N}@test.local", track.Id, grade.Id, null), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EmployeeDto>(ApiFactory.Json, Ct))!;
    }

    /// <summary>Уникальный адрес: письма в outbox общие для всех тестов и ищутся по получателю.</summary>
    public static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@test.local";

    public static async Task<GradeDto> GradeAsync(HttpClient client, string code) =>
        (await client.GetFromJsonAsync<List<GradeDto>>("/api/grades", ApiFactory.Json, Ct))!.Single(g => g.Code == code);

    /// <summary>Руководитель, сотрудник E3 и запущенная сессия перехода E3 → E4 с коллегой, лидом и менеджером.</summary>
    public async Task<LaunchedSession> LaunchedTransitionAsync()
    {
        var (managerId, manager) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var employee = await CreateEmployeeAsync(manager, "E3");
        var created = await manager.PostAsJsonAsync("/api/assessment-sessions", new CreateSessionCommand(employee.Id, SessionType.Transition,
            DateTime.UtcNow.AddDays(7),
            [new("Коллега Сценариев", UniqueEmail("peer"), EvaluatorRole.Peer), new("Лид Сценариев", UniqueEmail("lead"), EvaluatorRole.TeamLead), new("Менеджер Сценариев", UniqueEmail("m"), EvaluatorRole.Manager)]),
            ApiFactory.Json, Ct);
        var session = (await created.Content.ReadFromJsonAsync<SessionDetailsDto>(ApiFactory.Json, Ct))!;
        var launched = await manager.PostAsync($"/api/assessment-sessions/{session.Id}/launch", null, Ct);
        Assert.Equal(HttpStatusCode.OK, launched.StatusCode);
        return new LaunchedSession(managerId, manager, employee, session, (await launched.Content.ReadFromJsonAsync<List<ParticipantLinkDto>>(ApiFactory.Json, Ct))!);
    }

    /// <summary>Отправляет анкету с одинаковой оценкой на все индикаторы.</summary>
    public async Task SubmitAsync(ParticipantLinkDto link, int score, string? comment = "Пример")
    {
        using var client = factory.CreateClient();
        var url = $"/api/surveys/{link.Url[(link.Url.LastIndexOf('/') + 1)..]}";
        var survey = (await client.GetFromJsonAsync<SurveyDto>(url, ApiFactory.Json, Ct))!;
        var answers = survey.Groups.SelectMany(g => g.Indicators).Select(i => new SurveyAnswerDto(i.Id, score, false, comment)).ToList();
        var response = await client.PostAsJsonAsync($"{url}/submit", new SurveyAnswersRequest(answers), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public async Task SubmitAllAsync(LaunchedSession s, int score)
    {
        foreach (var link in s.Links)
        {
            await SubmitAsync(link, score);
        }
    }
}
