using System.Net;
using System.Net.Http.Json;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Application.Matrix;

namespace ReviewPlatform.Api.Tests;

public sealed class EmployeeEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Manager_SeesOnlyOwnEmployees()
    {
        var (managerA, clientA) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var (_, clientB) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var (trackId, gradeId) = await TrackAndGradeAsync(clientA);

        var own = await CreateAsync(clientA, new CreateEmployeeCommand("Иван Своих", Email(), trackId, gradeId, ManagerUserId: Guid.NewGuid()));
        Assert.Equal(managerA, own.ManagerUserId); // руководитель всегда назначает себя

        var listB = await clientB.GetFromJsonAsync<List<EmployeeDto>>("/api/employees", ApiFactory.Json, Ct);
        Assert.DoesNotContain(listB!, e => e.Id == own.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/employees/{own.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/employees/{own.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_MustSpecifyExistingManager()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var (trackId, gradeId) = await TrackAndGradeAsync(admin);

        var withoutManager = await admin.PostAsJsonAsync("/api/employees", new CreateEmployeeCommand("Без Руководителя", Email(), trackId, gradeId, null), ApiFactory.Json, Ct);
        var unknownManager = await admin.PostAsJsonAsync("/api/employees", new CreateEmployeeCommand("Без Руководителя", Email(), trackId, gradeId, Guid.NewGuid()), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, withoutManager.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownManager.StatusCode);
    }

    [Fact]
    public async Task DuplicateEmail_Returns409()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var (trackId, gradeId) = await TrackAndGradeAsync(client);
        var email = Email();
        await CreateAsync(client, new CreateEmployeeCommand("Первый", email, trackId, gradeId, null));

        var response = await client.PostAsJsonAsync("/api/employees", new CreateEmployeeCommand("Второй", email.ToUpperInvariant(), trackId, gradeId, null), ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Archive_HidesFromDefaultList_AndUpdateCanRestore()
    {
        var (managerId, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var (trackId, gradeId) = await TrackAndGradeAsync(client);
        var employee = await CreateAsync(client, new CreateEmployeeCommand("Архивный", Email(), trackId, gradeId, null));

        (await client.DeleteAsync($"/api/employees/{employee.Id}", Ct)).EnsureSuccessStatusCode();

        Assert.DoesNotContain((await client.GetFromJsonAsync<List<EmployeeDto>>("/api/employees", ApiFactory.Json, Ct))!, e => e.Id == employee.Id);
        Assert.Contains((await client.GetFromJsonAsync<List<EmployeeDto>>("/api/employees?includeArchived=true", ApiFactory.Json, Ct))!, e => e.Id == employee.Id && !e.IsActive);

        var restored = await client.PutAsJsonAsync($"/api/employees/{employee.Id}", new UpdateEmployeeRequest("Вернулся", employee.Email, trackId, gradeId, managerId, IsActive: true), ApiFactory.Json, Ct);
        var dto = (await restored.Content.ReadFromJsonAsync<EmployeeDto>(ApiFactory.Json, Ct))!;
        Assert.Equal(("Вернулся", true), (dto.FullName, dto.IsActive));
    }

    [Fact]
    public async Task Search_FiltersByNameCaseInsensitive()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);
        var (trackId, gradeId) = await TrackAndGradeAsync(client);
        await CreateAsync(client, new CreateEmployeeCommand("Пётр Уникальнов", Email(), trackId, gradeId, null));
        await CreateAsync(client, new CreateEmployeeCommand("Анна Другая", Email(), trackId, gradeId, null));

        var found = await client.GetFromJsonAsync<List<EmployeeDto>>("/api/employees?search=уникальн", ApiFactory.Json, Ct);

        Assert.Equal("Пётр Уникальнов", Assert.Single(found!).FullName);
    }

    private static string Email() => $"emp-{Guid.NewGuid():N}@test.local";

    private static async Task<EmployeeDto> CreateAsync(HttpClient client, CreateEmployeeCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/employees", command, ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EmployeeDto>(ApiFactory.Json, Ct))!;
    }

    private static async Task<(Guid TrackId, Guid GradeId)> TrackAndGradeAsync(HttpClient client)
    {
        var track = (await client.GetFromJsonAsync<List<TrackDto>>("/api/tracks", ApiFactory.Json, Ct))!.Single();
        var grade = (await client.GetFromJsonAsync<List<GradeDto>>("/api/grades", ApiFactory.Json, Ct))!.First(g => g.Code == "E3");
        return (track.Id, grade.Id);
    }
}
