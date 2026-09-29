using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Application.Surveys;
using ReviewPlatform.Application.Users;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Api.Tests;

/// <summary>Каждый тест правит матрицу своего направления: seed-матрица «backend» общая для всех тестов.</summary>
public sealed class MatrixEditingTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GroupsAndIndicators_CreateEditReorder_ShowInMatrix()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var track = await CreateTrackAsync(admin);
        var e3 = await Scenarios.GradeAsync(admin, "E3");

        var first = await CreateGroupAsync(admin, track.Id, "Коммуникация");
        var second = await CreateGroupAsync(admin, track.Id, "Экспертность");
        var a = await CreateIndicatorAsync(admin, first.Id, e3.Id, "Слушает собеседника");
        var b = await CreateIndicatorAsync(admin, first.Id, e3.Id, "Задаёт вопросы");

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/competency-groups/{first.Id}", new UpdateGroupRequest("Общение", "Как работает с людьми"), ApiFactory.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/indicators/{a.Id}", new UpdateIndicatorRequest("Внимательно слушает"), ApiFactory.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/competency-groups/reorder", new ReorderGroupsRequest(track.Id, [second.Id, first.Id]), ApiFactory.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/indicators/reorder", new ReorderIndicatorsRequest(first.Id, e3.Id, [b.Id, a.Id]), ApiFactory.Json, Ct)).StatusCode);

        var matrix = await MatrixAsync(admin, track.Id);
        Assert.Equal(["Экспертность", "Общение"], matrix.Groups.Select(g => g.Name));
        Assert.Equal("Как работает с людьми", matrix.Groups[1].Description);
        Assert.Equal(["Задаёт вопросы", "Внимательно слушает"], matrix.Groups[1].Indicators.Select(i => i.Text));
    }

    [Fact]
    public async Task Duplicates_Return409()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var track = await CreateTrackAsync(admin);
        var e3 = await Scenarios.GradeAsync(admin, "E3");
        var group = await CreateGroupAsync(admin, track.Id, "Коммуникация");
        await CreateIndicatorAsync(admin, group.Id, e3.Id, "Слушает");

        var sameGroup = await admin.PostAsJsonAsync("/api/competency-groups", new CreateGroupRequest(track.Id, "коммуникация", null), ApiFactory.Json, Ct);
        var sameIndicator = await admin.PostAsJsonAsync("/api/indicators", new CreateIndicatorRequest(group.Id, e3.Id, " Слушает "), ApiFactory.Json, Ct);
        var sameTrack = await admin.PostAsJsonAsync("/api/tracks", new CreateTrackRequest(track.Code, "Дубль"), ApiFactory.Json, Ct);

        Assert.Equal([HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.Conflict], [sameGroup.StatusCode, sameIndicator.StatusCode, sameTrack.StatusCode]);
    }

    [Fact]
    public async Task Manager_CannotEditMatrix()
    {
        var (_, manager) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var backend = (await manager.GetFromJsonAsync<List<TrackDto>>("/api/tracks", ApiFactory.Json, Ct))!.Single(t => t.Code == "backend");

        var create = await manager.PostAsJsonAsync("/api/competency-groups", new CreateGroupRequest(backend.Id, "Чужая", null), ApiFactory.Json, Ct);
        var export = await manager.GetAsync($"/api/tracks/{backend.Id}/matrix/export", Ct);

        Assert.Equal([HttpStatusCode.Forbidden, HttpStatusCode.Forbidden], [create.StatusCode, export.StatusCode]);
    }

    [Fact]
    public async Task IndicatorUsedInSession_CanOnlyBeArchived_AndSessionKeepsSnapshot()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var track = await CreateTrackAsync(admin);
        var (e3, e4) = (await Scenarios.GradeAsync(admin, "E3"), await Scenarios.GradeAsync(admin, "E4"));
        var group = await CreateGroupAsync(admin, track.Id, "Коммуникация");
        var used = await CreateIndicatorAsync(admin, group.Id, e3.Id, "Слушает собеседника");
        await CreateIndicatorAsync(admin, group.Id, e3.Id, "Задаёт вопросы");
        await CreateIndicatorAsync(admin, group.Id, e4.Id, "Фасилитирует встречи");
        var link = await LaunchSessionAsync(admin, track.Id, e3.Id);
        var fresh = await CreateIndicatorAsync(admin, group.Id, e3.Id, "Добавлен после запуска");

        var delete = await admin.DeleteAsync($"/api/indicators/{used.Id}", Ct);
        var archive = await admin.PostAsync($"/api/indicators/{used.Id}/archive", null, Ct);
        var deleteFresh = await admin.DeleteAsync($"/api/indicators/{fresh.Id}", Ct);

        Assert.Equal([HttpStatusCode.Conflict, HttpStatusCode.NoContent, HttpStatusCode.NoContent], [delete.StatusCode, archive.StatusCode, deleteFresh.StatusCode]);
        Assert.DoesNotContain((await MatrixAsync(admin, track.Id)).Groups.Single().Indicators, i => i.Id == used.Id);

        // Анкета запущенной сессии не изменилась
        using var anonymous = factory.CreateClient();
        var survey = await anonymous.GetFromJsonAsync<SurveyDto>($"/api/surveys/{link.Url[(link.Url.LastIndexOf('/') + 1)..]}", ApiFactory.Json, Ct);
        Assert.Equal(["Слушает собеседника", "Задаёт вопросы", "Фасилитирует встречи"], survey!.Groups.SelectMany(g => g.Indicators).Select(i => i.Text));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/competency-groups/{group.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task DeleteGroup_OnlyWhenEmpty()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var track = await CreateTrackAsync(admin);
        var e3 = await Scenarios.GradeAsync(admin, "E3");
        var empty = await CreateGroupAsync(admin, track.Id, "Пустая");
        var filled = await CreateGroupAsync(admin, track.Id, "С индикатором");
        await CreateIndicatorAsync(admin, filled.Id, e3.Id, "Что-то делает");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/competency-groups/{empty.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/competency-groups/{filled.Id}", Ct)).StatusCode);
    }

    // ---------- Импорт и экспорт ----------

    [Fact]
    public async Task Import_PreviewThenApply_ReplacesMatrix()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var track = await CreateTrackAsync(admin);
        var e3 = await Scenarios.GradeAsync(admin, "E3");
        var group = await CreateGroupAsync(admin, track.Id, "Коммуникация");
        await CreateIndicatorAsync(admin, group.Id, e3.Id, "Слушает собеседника");
        await CreateIndicatorAsync(admin, group.Id, e3.Id, "Уйдёт в архив");

        var file = Workbook(
            (track.Code, "Коммуникация", 2, "E3", "Слушает собеседника", 1),
            (track.Code, "Экспертность", 1, "E3", "Пишет тесты", 1),
            (track.Code, "Экспертность", 1, "E4", "Проектирует модули", 1));
        var preview = await PreviewAsync(admin, track.Id, file);

        Assert.Empty(preview.Errors);
        Assert.NotNull(preview.ImportId);
        Assert.Equal(new MatrixImportSummaryDto(1, 1, 2, 0, 1, 1), preview.Summary);
        Assert.Contains(preview.Changes, c => c.Kind == MatrixChangeKind.IndicatorArchived && c.Text == "Уйдёт в архив");
        Assert.Equal(2, (await MatrixAsync(admin, track.Id)).Groups.Single().Indicators.Count); // до подтверждения ничего не меняется

        var apply = await admin.PostAsync($"/api/tracks/{track.Id}/matrix/import/{preview.ImportId}/apply", null, Ct);

        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        var matrix = await MatrixAsync(admin, track.Id);
        Assert.Equal(["Экспертность", "Коммуникация"], matrix.Groups.Select(g => g.Name));
        Assert.Equal(["Слушает собеседника"], matrix.Groups[1].Indicators.Select(i => i.Text));
        Assert.Equal(2, matrix.Groups[0].Indicators.Count);

        // Повторно подтвердить тот же предпросмотр нельзя
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/tracks/{track.Id}/matrix/import/{preview.ImportId}/apply", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Export_ThenImport_HasNoChanges()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var backend = (await admin.GetFromJsonAsync<List<TrackDto>>("/api/tracks", ApiFactory.Json, Ct))!.Single(t => t.Code == "backend");

        var export = await admin.GetAsync($"/api/tracks/{backend.Id}/matrix/export", Ct);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);

        var preview = await PreviewAsync(admin, backend.Id, await export.Content.ReadAsByteArrayAsync(Ct));

        Assert.Empty(preview.Errors);
        Assert.Empty(preview.Changes);
        Assert.Equal(160, preview.Summary.IndicatorsUnchanged);
    }

    [Fact]
    public async Task Import_InvalidFile_ReturnsErrorsWithoutImportId()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var track = await CreateTrackAsync(admin);

        var wrongRows = await PreviewAsync(admin, track.Id, Workbook(("other", "Группа", 1, "E9", "Текст", 1)));
        var notExcel = await PreviewAsync(admin, track.Id, "просто текст"u8.ToArray());

        Assert.Null(wrongRows.ImportId);
        Assert.Contains(wrongRows.Errors, e => e.Contains("E9", StringComparison.Ordinal) && e.Contains("other", StringComparison.Ordinal));
        Assert.Null(notExcel.ImportId);
        Assert.Contains(notExcel.Errors, e => e.Contains(".xlsx", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Template_HasTemplateHeaders()
    {
        using var admin = await factory.SignInAsAdminAsync();

        var response = await admin.GetAsync("/api/matrix/import-template", Ct);

        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync(Ct));
        Assert.Equal(["Направление", "Группа", "Порядок группы", "Грейд", "Индикатор", "Порядок"],
            workbook.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString()));
    }

    // ---------- Справочники ----------

    [Fact]
    public async Task RoleRules_Update_ValidatesAndPersists()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var original = (await admin.GetFromJsonAsync<List<GradeRoleRuleDto>>("/api/grade-role-rules", ApiFactory.Json, Ct))!;
        var inputs = original.Select(r => new GradeRoleRuleInput(r.GradeId, r.Role, r.MinCount, r.MaxCount)).ToList();
        var e1 = original.First(r => r.GradeCode == "E1").GradeId;
        try
        {
            // E1 не используется другими тестами: меняем только его и возвращаем обратно
            var changed = inputs.Select(r => r.GradeId == e1 && r.Role == EvaluatorRole.Peer ? r with { MinCount = 2, MaxCount = 4 } : r).ToList();
            var response = await admin.PutAsJsonAsync("/api/grade-role-rules", changed, ApiFactory.Json, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = (await response.Content.ReadFromJsonAsync<List<GradeRoleRuleDto>>(ApiFactory.Json, Ct))!;
            Assert.Contains(saved, r => r is { GradeCode: "E1", Role: EvaluatorRole.Peer, MinCount: 2, MaxCount: 4 });

            var noSelf = inputs.Where(r => !(r.GradeId == e1 && r.Role == EvaluatorRole.Self)).ToList();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/grade-role-rules", noSelf, ApiFactory.Json, Ct)).StatusCode);

            var badLimits = inputs.Select(r => r.GradeId == e1 && r.Role == EvaluatorRole.Peer ? r with { MinCount = 3, MaxCount = 1 } : r).ToList();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/grade-role-rules", badLimits, ApiFactory.Json, Ct)).StatusCode);
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/grade-role-rules", inputs, ApiFactory.Json, Ct);
        }

        Assert.Equal(original, await admin.GetFromJsonAsync<List<GradeRoleRuleDto>>("/api/grade-role-rules", ApiFactory.Json, Ct));
    }

    [Fact]
    public async Task RenameGrade_And_UpdateTrack()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var e1 = await Scenarios.GradeAsync(admin, "E1");
        var track = await CreateTrackAsync(admin);
        try
        {
            var renamed = await admin.PutAsJsonAsync($"/api/grades/{e1.Id}", new RenameGradeRequest("Стажёр"), ApiFactory.Json, Ct);
            Assert.Equal("Стажёр", (await renamed.Content.ReadFromJsonAsync<GradeDto>(ApiFactory.Json, Ct))!.Name);
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/grades/{e1.Id}", new RenameGradeRequest(e1.Name), ApiFactory.Json, Ct);
        }

        var response = await admin.PutAsJsonAsync($"/api/tracks/{track.Id}", new UpdateTrackRequest("Frontend", false), ApiFactory.Json, Ct);
        var updated = (await response.Content.ReadFromJsonAsync<TrackDto>(ApiFactory.Json, Ct))!;
        Assert.Equal(("Frontend", false), (updated.Name, updated.IsActive));
    }

    // ---------- Вспомогательное ----------

    private static async Task<TrackDto> CreateTrackAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/tracks", new CreateTrackRequest($"t-{Guid.NewGuid():N}"[..20], "Тестовое направление"), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TrackDto>(ApiFactory.Json, Ct))!;
    }

    private static async Task<CompetencyGroupDto> CreateGroupAsync(HttpClient admin, Guid trackId, string name)
    {
        var response = await admin.PostAsJsonAsync("/api/competency-groups", new CreateGroupRequest(trackId, name, null), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CompetencyGroupDto>(ApiFactory.Json, Ct))!;
    }

    private static async Task<IndicatorDto> CreateIndicatorAsync(HttpClient admin, Guid groupId, Guid gradeId, string text)
    {
        var response = await admin.PostAsJsonAsync("/api/indicators", new CreateIndicatorRequest(groupId, gradeId, text), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IndicatorDto>(ApiFactory.Json, Ct))!;
    }

    private static async Task<MatrixDto> MatrixAsync(HttpClient client, Guid trackId) =>
        (await client.GetFromJsonAsync<MatrixDto>($"/api/tracks/{trackId}/matrix", ApiFactory.Json, Ct))!;

    /// <summary>Сотрудник на направлении и запущенная сессия перехода; возвращает ссылку самооценки.</summary>
    private async Task<ParticipantLinkDto> LaunchSessionAsync(HttpClient admin, Guid trackId, Guid gradeId)
    {
        var managerId = await factory.CreateUserAsync(Roles.Manager);
        var employee = await admin.PostAsJsonAsync("/api/employees",
            new CreateEmployeeCommand("Сотрудник Матрицы", Scenarios.UniqueEmail("emp"), trackId, gradeId, managerId), ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, employee.StatusCode);
        var employeeId = (await employee.Content.ReadFromJsonAsync<EmployeeDto>(ApiFactory.Json, Ct))!.Id;
        var created = await admin.PostAsJsonAsync("/api/assessment-sessions", new CreateSessionCommand(employeeId, SessionType.Transition, DateTime.UtcNow.AddDays(7),
            [new("Коллега", Scenarios.UniqueEmail("peer"), EvaluatorRole.Peer), new("Лид", Scenarios.UniqueEmail("lead"), EvaluatorRole.TeamLead), new("Менеджер", Scenarios.UniqueEmail("m"), EvaluatorRole.Manager)]),
            ApiFactory.Json, Ct);
        var session = (await created.Content.ReadFromJsonAsync<SessionDetailsDto>(ApiFactory.Json, Ct))!;
        var launched = await admin.PostAsync($"/api/assessment-sessions/{session.Id}/launch", null, Ct);
        Assert.Equal(HttpStatusCode.OK, launched.StatusCode);
        return (await launched.Content.ReadFromJsonAsync<List<ParticipantLinkDto>>(ApiFactory.Json, Ct))!.Single(l => l.Role == EvaluatorRole.Self);
    }

    private static async Task<MatrixImportPreviewDto> PreviewAsync(HttpClient admin, Guid trackId, byte[] file)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(part, "file", "matrix.xlsx");
        var response = await admin.PostAsync($"/api/tracks/{trackId}/matrix/import/preview", content, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MatrixImportPreviewDto>(ApiFactory.Json, Ct))!;
    }

    private static byte[] Workbook(params (string Track, string Group, int GroupOrder, string Grade, string Text, int Order)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Матрица");
        string[] headers = ["Направление", "Группа", "Порядок группы", "Грейд", "Индикатор", "Порядок"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            var (track, group, groupOrder, grade, text, order) = rows[r];
            sheet.Cell(r + 2, 1).Value = track;
            sheet.Cell(r + 2, 2).Value = group;
            sheet.Cell(r + 2, 3).Value = groupOrder;
            sheet.Cell(r + 2, 4).Value = grade;
            sheet.Cell(r + 2, 5).Value = text;
            sheet.Cell(r + 2, 6).Value = order;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
