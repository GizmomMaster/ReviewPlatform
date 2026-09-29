using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Application.Reports;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Application.Surveys;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Assessments.Reporting;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Api.Tests;

public sealed class ReportEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Report_BeforeLaunch_Returns409()
    {
        var (manager, session, _) = await SessionAsync(launch: false);

        Assert.Equal(HttpStatusCode.Conflict, (await manager.GetAsync($"/api/assessment-sessions/{session.Id}/report", Ct)).StatusCode);
    }

    [Fact]
    public async Task Report_IsPreliminaryUntilAllSubmit_ThenFinal()
    {
        var (manager, session, links) = await SessionAsync(launch: true);
        await SubmitAsync(Link(links, EvaluatorRole.Self), 3, "Считаю, что всегда");
        await SubmitAsync(Link(links, EvaluatorRole.Peer), 2);
        await SubmitAsync(Link(links, EvaluatorRole.TeamLead), 1);

        var preliminary = await ReportAsync(manager, session.Id);

        Assert.True(preliminary.IsPreliminary);
        Assert.Equal((3, 4), (preliminary.SubmittedCount, preliminary.ParticipantCount));
        Assert.Null(preliminary.TargetGrade);
        Assert.Null(preliminary.TargetReadiness);
        Assert.Equal(28, preliminary.Indicators.Count);
        var indicator = preliminary.Indicators[0];
        Assert.Equal((1.5, 2, false, 3), (indicator.Score, indicator.RatingsCount, indicator.IsMet, indicator.SelfScore));
        Assert.Equal(BlindSpot.Overestimated, indicator.BlindSpot);
        Assert.Equal(3, indicator.Ratings.Count);
        Assert.Contains(indicator.Ratings, r => r.Comment == "Считаю, что всегда");
        Assert.Equal(0.0, preliminary.CurrentConfirmation);
        Assert.Equal(7, preliminary.Groups.Count);

        await SubmitAsync(Link(links, EvaluatorRole.Manager), 3, "Пример из практики");
        var final = await ReportAsync(manager, session.Id);

        Assert.False(final.IsPreliminary);
        Assert.Equal(SessionStatus.AwaitingDecision, final.Status);
        Assert.Equal(2.0, final.Indicators[0].Score); // (2 + 1 + 3) / 3
        Assert.Equal(1.0, final.CurrentConfirmation);
        Assert.True(final.Indicators[0].IsDisputed); // 1 и 3
    }

    [Fact]
    public async Task Report_OfAnotherManagersSession_Returns404()
    {
        var (_, session, _) = await SessionAsync(launch: true);
        var (_, stranger) = await factory.SignInAsNewUserAsync(Roles.Manager);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/assessment-sessions/{session.Id}/report", Ct)).StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsWorkbookWithRatersAndComments()
    {
        var (manager, session, links) = await SessionAsync(launch: true);
        await SubmitAsync(Link(links, EvaluatorRole.Peer), 0, "Не видел такого");

        var response = await manager.GetAsync($"/api/assessment-sessions/{session.Id}/report/export", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".xlsx", response.Content.Headers.ContentDisposition?.FileNameStar, StringComparison.Ordinal);

        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync(Ct));
        Assert.Equal(["Сводка", "Индикаторы", "Комментарии"], workbook.Worksheets.Select(w => w.Name));
        Assert.Equal("Коллега Отчётов (Коллега)", workbook.Worksheet("Индикаторы").Cell(1, 10).GetString());
        Assert.Equal(28 + 1, workbook.Worksheet("Индикаторы").LastRowUsed()!.RowNumber());
        Assert.Equal("Не видел такого", workbook.Worksheet("Комментарии").Cell(2, 6).GetString());
    }

    private async Task<(HttpClient Manager, SessionDetailsDto Session, List<ParticipantLinkDto> Links)> SessionAsync(bool launch)
    {
        var (_, manager) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var track = (await manager.GetFromJsonAsync<List<TrackDto>>("/api/tracks", ApiFactory.Json, Ct))!.Single();
        var grade = (await manager.GetFromJsonAsync<List<GradeDto>>("/api/grades", ApiFactory.Json, Ct))!.Single(g => g.Code == "E3");
        var employee = await (await manager.PostAsJsonAsync("/api/employees",
            new CreateEmployeeCommand("Отчётный Сотрудник", $"emp-{Guid.NewGuid():N}@test.local", track.Id, grade.Id, null), ApiFactory.Json, Ct))
            .Content.ReadFromJsonAsync<EmployeeDto>(ApiFactory.Json, Ct);
        var created = await manager.PostAsJsonAsync("/api/assessment-sessions", new CreateSessionCommand(employee!.Id, SessionType.Confirmation,
            DateTime.UtcNow.AddDays(7),
            [new("Коллега Отчётов", "peer@test.local", EvaluatorRole.Peer), new("Лид Отчётов", "lead@test.local", EvaluatorRole.TeamLead), new("Менеджер Отчётов", "m@test.local", EvaluatorRole.Manager)]),
            ApiFactory.Json, Ct);
        var session = (await created.Content.ReadFromJsonAsync<SessionDetailsDto>(ApiFactory.Json, Ct))!;
        if (!launch)
        {
            return (manager, session, []);
        }

        var launched = await manager.PostAsync($"/api/assessment-sessions/{session.Id}/launch", null, Ct);
        return (manager, session, (await launched.Content.ReadFromJsonAsync<List<ParticipantLinkDto>>(ApiFactory.Json, Ct))!);
    }

    private static ParticipantLinkDto Link(List<ParticipantLinkDto> links, EvaluatorRole role) => links.Single(l => l.Role == role);

    /// <summary>Отправляет анкету с одинаковой оценкой на все индикаторы.</summary>
    private async Task SubmitAsync(ParticipantLinkDto link, int score, string? comment = null)
    {
        using var client = factory.CreateClient();
        var url = $"/api/surveys/{link.Url[(link.Url.LastIndexOf('/') + 1)..]}";
        var survey = (await client.GetFromJsonAsync<SurveyDto>(url, ApiFactory.Json, Ct))!;
        var answers = survey.Groups.SelectMany(g => g.Indicators).Select(i => new SurveyAnswerDto(i.Id, score, false, comment)).ToList();
        var response = await client.PostAsJsonAsync($"{url}/submit", new SurveyAnswersRequest(answers), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<SessionReportDto> ReportAsync(HttpClient client, Guid sessionId) =>
        (await client.GetFromJsonAsync<SessionReportDto>($"/api/assessment-sessions/{sessionId}/report", ApiFactory.Json, Ct))!;
}
