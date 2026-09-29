using System.Net;
using System.Net.Http.Json;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Api.Tests;

public sealed class SessionEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateTime Deadline => DateTime.UtcNow.AddDays(14);

    private static readonly NewParticipant[] FullTeam =
    [
        new("Коллега Один", "peer1@test.local", EvaluatorRole.Peer),
        new("Лид Лидов", "lead@test.local", EvaluatorRole.TeamLead),
        new("Менеджер Менеджеров", "manager@test.local", EvaluatorRole.Manager),
    ];

    [Fact]
    public async Task Create_AddsSelfAutomatically_AndOwnerIsEmployeeManager()
    {
        var (managerId, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var employee = await CreateEmployeeAsync(client, "E3");

        var session = await CreateSessionAsync(client, employee.Id, SessionType.Transition, []);

        Assert.Equal(SessionStatus.Draft, session.Status);
        Assert.Equal(managerId, session.OwnerUserId);
        Assert.Equal(("E3", "E4"), (session.CurrentGrade.Code, session.TargetGrade!.Code));
        var self = Assert.Single(session.Participants);
        Assert.Equal((EvaluatorRole.Self, employee.Email), (self.Role, self.Email));
        Assert.Contains(session.RoleRequirements, r => r.Role == EvaluatorRole.Peer && !r.IsSatisfied);
        Assert.Equal(54, session.IndicatorCount); // E3 (28) + E4 (26) в seed-матрице
    }

    [Fact]
    public async Task Create_SecondActiveSession_Returns409()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var employee = await CreateEmployeeAsync(client, "E3");
        await CreateSessionAsync(client, employee.Id, SessionType.Transition, []);

        var second = await client.PostAsJsonAsync("/api/assessment-sessions",
            new CreateSessionCommand(employee.Id, SessionType.Confirmation, Deadline, []), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_ForAnotherManagersEmployee_Returns404()
    {
        var (_, owner) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var (_, stranger) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var employee = await CreateEmployeeAsync(owner, "E3");

        var response = await stranger.PostAsJsonAsync("/api/assessment-sessions",
            new CreateSessionCommand(employee.Id, SessionType.Transition, Deadline, []), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_TransitionForLead_Returns409_ConfirmationAllowed()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var lead = await CreateEmployeeAsync(client, "E8");

        var transition = await client.PostAsJsonAsync("/api/assessment-sessions",
            new CreateSessionCommand(lead.Id, SessionType.Transition, Deadline, []), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, transition.StatusCode);

        var confirmation = await CreateSessionAsync(client, lead.Id, SessionType.Confirmation, []);
        Assert.Null(confirmation.TargetGrade);
        Assert.Equal(14, confirmation.IndicatorCount); // только E8
    }

    [Fact]
    public async Task Create_RoleNotAllowedForGrade_Returns409()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var employee = await CreateEmployeeAsync(client, "E3");

        var response = await client.PostAsJsonAsync("/api/assessment-sessions",
            new CreateSessionCommand(employee.Id, SessionType.Transition, Deadline, [new("РЦК", "rck@test.local", EvaluatorRole.Rck)]), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Launch_WithoutRequiredRoles_Returns409_WithDetails()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var session = await CreateSessionAsync(client, (await CreateEmployeeAsync(client, "E3")).Id, SessionType.Transition, []);

        var response = await client.PostAsync($"/api/assessment-sessions/{session.Id}/launch", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Peer", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Launch_ReturnsLinks_SnapshotsIndicators_AndLocksDraftEditing()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var session = await CreateSessionAsync(client, (await CreateEmployeeAsync(client, "E3")).Id, SessionType.Transition, FullTeam);

        var links = await LaunchAsync(client, session.Id);

        Assert.Equal(4, links.Count);
        Assert.All(links, l => Assert.Matches(@"^http://localhost:8080/survey/[A-Za-z0-9_-]{43}$", l.Url));
        Assert.Equal(4, links.Select(l => l.Url).Distinct().Count());

        var details = await GetAsync(client, session.Id);
        Assert.Equal(SessionStatus.InProgress, details.Status);
        Assert.NotNull(details.LaunchedAtUtc);
        Assert.All(details.Participants, p => Assert.NotNull(p.TokenIssuedAtUtc));

        var preview = await client.GetFromJsonAsync<SurveyPreviewDto>($"/api/assessment-sessions/{session.Id}/survey-preview", ApiFactory.Json, Ct);
        Assert.Equal(54, preview!.IndicatorCount);
        Assert.Equal(7, preview.Groups.Count);

        var edit = await client.PutAsJsonAsync($"/api/assessment-sessions/{session.Id}", new UpdateSessionRequest(SessionType.Confirmation, Deadline), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/assessment-sessions/{session.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task AfterLaunch_AddParticipantReturnsLink_ReissueChangesLink_RemoveMarksRemoved()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var session = await CreateSessionAsync(client, (await CreateEmployeeAsync(client, "E3")).Id, SessionType.Transition, FullTeam);
        var links = await LaunchAsync(client, session.Id);

        var added = await client.PostAsJsonAsync($"/api/assessment-sessions/{session.Id}/participants",
            new AddParticipantRequest("Коллега Два", "peer2@test.local", EvaluatorRole.Peer), ApiFactory.Json, Ct);
        var result = (await added.Content.ReadFromJsonAsync<AddParticipantResult>(ApiFactory.Json, Ct))!;
        Assert.NotNull(result.Link);

        var peer1 = links.Single(l => l.Role == EvaluatorRole.Peer);
        var reissued = await client.PostAsync($"/api/assessment-sessions/{session.Id}/participants/{peer1.ParticipantId}/reissue-link", null, Ct);
        var newLink = (await reissued.Content.ReadFromJsonAsync<ParticipantLinkDto>(ApiFactory.Json, Ct))!;
        Assert.NotEqual(peer1.Url, newLink.Url);

        (await client.DeleteAsync($"/api/assessment-sessions/{session.Id}/participants/{peer1.ParticipantId}", Ct)).EnsureSuccessStatusCode();
        var details = await GetAsync(client, session.Id);
        Assert.Equal(ParticipantStatus.Removed, details.Participants.Single(p => p.Id == peer1.ParticipantId).Status);

        var self = details.Participants.Single(p => p.Role == EvaluatorRole.Self);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/assessment-sessions/{session.Id}/participants/{self.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Cancel_AllowsNewSessionForEmployee()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var employee = await CreateEmployeeAsync(client, "E3");
        var session = await CreateSessionAsync(client, employee.Id, SessionType.Transition, FullTeam);
        await LaunchAsync(client, session.Id);

        (await client.PostAsync($"/api/assessment-sessions/{session.Id}/cancel", null, Ct)).EnsureSuccessStatusCode();

        Assert.Equal(SessionStatus.Cancelled, (await GetAsync(client, session.Id)).Status);
        await CreateSessionAsync(client, employee.Id, SessionType.Transition, []);
    }

    [Fact]
    public async Task DeleteDraft_And_List_ShowsProgressOnlyForOwnSessions()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var (_, other) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var launched = await CreateSessionAsync(client, (await CreateEmployeeAsync(client, "E3")).Id, SessionType.Transition, FullTeam);
        await LaunchAsync(client, launched.Id);
        var draft = await CreateSessionAsync(client, (await CreateEmployeeAsync(client, "E2")).Id, SessionType.Transition, []);

        (await client.DeleteAsync($"/api/assessment-sessions/{draft.Id}", Ct)).EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<List<SessionListItemDto>>("/api/assessment-sessions", ApiFactory.Json, Ct);
        var item = Assert.Single(list!);
        Assert.Equal((launched.Id, 4, 0, "E3", "E4"), (item.Id, item.ParticipantsTotal, item.ParticipantsSubmitted, item.CurrentGradeCode, item.TargetGradeCode));
        Assert.Empty((await other.GetFromJsonAsync<List<SessionListItemDto>>("/api/assessment-sessions", ApiFactory.Json, Ct))!);
    }

    private static async Task<EmployeeDto> CreateEmployeeAsync(HttpClient client, string gradeCode)
    {
        var track = (await client.GetFromJsonAsync<List<TrackDto>>("/api/tracks", ApiFactory.Json, Ct))!.Single();
        var grade = (await client.GetFromJsonAsync<List<GradeDto>>("/api/grades", ApiFactory.Json, Ct))!.Single(g => g.Code == gradeCode);
        var response = await client.PostAsJsonAsync("/api/employees",
            new CreateEmployeeCommand("Оцениваемый Сотрудник", $"emp-{Guid.NewGuid():N}@test.local", track.Id, grade.Id, null), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EmployeeDto>(ApiFactory.Json, Ct))!;
    }

    private static async Task<SessionDetailsDto> CreateSessionAsync(HttpClient client, Guid employeeId, SessionType type, IReadOnlyList<NewParticipant> participants)
    {
        var response = await client.PostAsJsonAsync("/api/assessment-sessions", new CreateSessionCommand(employeeId, type, Deadline, participants), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDetailsDto>(ApiFactory.Json, Ct))!;
    }

    private static async Task<List<ParticipantLinkDto>> LaunchAsync(HttpClient client, Guid sessionId)
    {
        var response = await client.PostAsync($"/api/assessment-sessions/{sessionId}/launch", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ParticipantLinkDto>>(ApiFactory.Json, Ct))!;
    }

    private static async Task<SessionDetailsDto> GetAsync(HttpClient client, Guid sessionId) =>
        (await client.GetFromJsonAsync<SessionDetailsDto>($"/api/assessment-sessions/{sessionId}", ApiFactory.Json, Ct))!;
}
