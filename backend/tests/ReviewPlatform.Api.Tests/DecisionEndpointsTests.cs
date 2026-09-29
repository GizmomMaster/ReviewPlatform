using System.Net;
using System.Net.Http.Json;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Application.Reports;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Application.Surveys;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Audit;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Api.Tests;

public sealed class DecisionEndpointsTests(ApiFactory factory)
{
    private readonly Scenarios _scenarios = new(factory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Promote_ClosesSession_UpdatesEmployeeGrade_AndShowsInHistory()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAllAsync(s, 3);
        var report = await s.Manager.GetFromJsonAsync<SessionReportDto>($"/api/assessment-sessions/{s.Session.Id}/report", ApiFactory.Json, Ct);
        var growthIndicator = report!.Indicators.First(i => i.Level == LevelKind.Target).Id;
        var e4 = await Scenarios.GradeAsync(s.Manager, "E4");

        var response = await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/decision",
            new DecideRequest(DecisionOutcome.Promoted, e4.Id, "Стабильно работает на уровне E4",
                [new("Провести дизайн-ревью", growthIndicator, new DateOnly(2027, 1, 31)), new("Менторить стажёра", null, null)]), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decision = (await response.Content.ReadFromJsonAsync<DecisionDto>(ApiFactory.Json, Ct))!;
        Assert.Equal(("E4", 2), (decision.NewGrade.Code, decision.PlanItems.Count));

        var details = await s.Manager.GetFromJsonAsync<SessionDetailsDto>($"/api/assessment-sessions/{s.Session.Id}", ApiFactory.Json, Ct);
        Assert.Equal(SessionStatus.Closed, details!.Status);
        Assert.Equal(DecisionOutcome.Promoted, details.Decision!.Outcome);

        var employee = await s.Manager.GetFromJsonAsync<EmployeeDto>($"/api/employees/{s.Employee.Id}", ApiFactory.Json, Ct);
        Assert.Equal("E4", employee!.GradeCode);

        var history = await s.Manager.GetFromJsonAsync<List<EmployeeHistoryItemDto>>($"/api/employees/{s.Employee.Id}/history", ApiFactory.Json, Ct);
        var item = Assert.Single(history!);
        Assert.Equal((DecisionOutcome.Promoted, "E4", SessionStatus.Closed), (item.Outcome, item.NewGradeCode, item.Status));

        // После закрытия можно начать новую сессию уже с грейда E4
        var next = await s.Manager.PostAsJsonAsync("/api/assessment-sessions",
            new CreateSessionCommand(s.Employee.Id, SessionType.Transition, DateTime.UtcNow.AddDays(7), []), ApiFactory.Json, Ct);
        Assert.Equal("E4", (await next.Content.ReadFromJsonAsync<SessionDetailsDto>(ApiFactory.Json, Ct))!.CurrentGrade.Code);
    }

    [Fact]
    public async Task Decide_BeforeSurveyCompleted_Returns409()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        var e3 = await Scenarios.GradeAsync(s.Manager, "E3");

        var response = await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/decision",
            new DecideRequest(DecisionOutcome.GradeConfirmed, e3.Id, "ок", []), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(DecisionOutcome.Promoted, "E2")]
    [InlineData(DecisionOutcome.Promoted, "E3")]
    [InlineData(DecisionOutcome.GradeConfirmed, "E4")]
    [InlineData(DecisionOutcome.NotConfirmed, "E4")]
    public async Task Decide_InconsistentGrade_Returns409(DecisionOutcome outcome, string gradeCode)
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAllAsync(s, 2);
        var grade = await Scenarios.GradeAsync(s.Manager, gradeCode);

        var response = await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/decision",
            new DecideRequest(outcome, grade.Id, "обоснование", []), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Decide_WithoutComment_Returns400()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAllAsync(s, 2);
        var e3 = await Scenarios.GradeAsync(s.Manager, "E3");

        var response = await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/decision",
            new DecideRequest(DecisionOutcome.GradeConfirmed, e3.Id, "", []), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NotConfirmed_WithLowerGrade_DemotesEmployee()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAllAsync(s, 0);
        var e2 = await Scenarios.GradeAsync(s.Manager, "E2");

        (await s.Manager.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/decision",
            new DecideRequest(DecisionOutcome.NotConfirmed, e2.Id, "Не соответствует E3", []), ApiFactory.Json, Ct)).EnsureSuccessStatusCode();

        var employee = await s.Manager.GetFromJsonAsync<EmployeeDto>($"/api/employees/{s.Employee.Id}", ApiFactory.Json, Ct);
        Assert.Equal("E2", employee!.GradeCode);
    }

    [Fact]
    public async Task Decide_OnAnotherManagersSession_Returns404()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAllAsync(s, 2);
        var (_, stranger) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var e3 = await Scenarios.GradeAsync(stranger, "E3");

        var response = await stranger.PostAsJsonAsync($"/api/assessment-sessions/{s.Session.Id}/decision",
            new DecideRequest(DecisionOutcome.GradeConfirmed, e3.Id, "ок", []), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CloseEarly_RequiresNonSelfSubmission_ThenClosesSurvey()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await _scenarios.SubmitAsync(s.Link(EvaluatorRole.Self), 2);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Manager.PostAsync($"/api/assessment-sessions/{s.Session.Id}/close-early", null, Ct)).StatusCode);

        await _scenarios.SubmitAsync(s.Link(EvaluatorRole.Peer), 2);
        (await s.Manager.PostAsync($"/api/assessment-sessions/{s.Session.Id}/close-early", null, Ct)).EnsureSuccessStatusCode();

        var details = await s.Manager.GetFromJsonAsync<SessionDetailsDto>($"/api/assessment-sessions/{s.Session.Id}", ApiFactory.Json, Ct);
        Assert.Equal(SessionStatus.AwaitingDecision, details!.Status);
        using var anonymous = factory.CreateClient();
        var lead = s.Link(EvaluatorRole.TeamLead);
        var survey = await anonymous.GetFromJsonAsync<SurveyDto>($"/api/surveys/{lead.Url[(lead.Url.LastIndexOf('/') + 1)..]}", ApiFactory.Json, Ct);
        Assert.Equal(SurveyState.Closed, survey!.State);
    }

    [Fact]
    public async Task Audit_RecordsKeyActionsWithUser()
    {
        var s = await _scenarios.LaunchedTransitionAsync();
        await s.Manager.PostAsync($"/api/assessment-sessions/{s.Session.Id}/participants/{s.Link(EvaluatorRole.Peer).ParticipantId}/reissue-link", null, Ct);
        await s.Manager.PostAsync($"/api/assessment-sessions/{s.Session.Id}/cancel", null, Ct);

        var audit = (await s.Manager.GetFromJsonAsync<List<AuditEntryDto>>($"/api/assessment-sessions/{s.Session.Id}/audit", ApiFactory.Json, Ct))!;

        Assert.Equal([AuditActions.SessionCancelled, AuditActions.LinkReissued, AuditActions.SessionLaunched], audit.Select(a => a.Action));
        Assert.All(audit, a => Assert.NotNull(a.UserName));
        Assert.Contains("Коллега Сценариев", audit[1].Details, StringComparison.Ordinal);
    }
}
