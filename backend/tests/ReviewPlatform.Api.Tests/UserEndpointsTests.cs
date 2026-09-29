using System.Net;
using System.Net.Http.Json;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Users;

namespace ReviewPlatform.Api.Tests;

public sealed class UserEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Manager_CannotManageUsers()
    {
        var (_, client) = await factory.SignInAsNewUserAsync(Roles.Manager);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users", Ct)).StatusCode);
    }

    [Fact]
    public async Task CreateUser_GetsTemporaryPassword()
    {
        using var admin = await factory.SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/users", new CreateUserCommand("new-manager@test.local", "Новый Руководитель", Roles.Manager, "Temp1234"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<UserDto>(ApiFactory.Json, Ct))!;
        Assert.Equal(("new-manager@test.local", Roles.Manager, true, true), (user.Email, user.Role, user.IsActive, user.MustChangePassword));
    }

    [Theory]
    [InlineData("not-an-email", Roles.Manager, "Temp1234", "email")]
    [InlineData("weak@test.local", Roles.Manager, "weak", "password")]
    [InlineData("role@test.local", "Hacker", "Temp1234", "role")]
    public async Task CreateUser_Invalid_Returns400WithFieldError(string email, string role, string password, string field)
    {
        using var admin = await factory.SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/users", new CreateUserCommand(email, "Кто-то", role, password), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(ApiFactory.Json, Ct);
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Admin_CannotBlockSelf()
    {
        var (adminId, admin) = await factory.SignInAsNewUserAsync(Roles.Admin);

        var response = await admin.PutAsJsonAsync($"/api/users/{adminId}", new UpdateUserRequest("Я", Roles.Admin, IsActive: false), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_RequiresChangeOnNextLogin()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var userId = await factory.CreateUserAsync(Roles.Manager, "reset-me@test.local");

        (await admin.PostAsJsonAsync($"/api/users/{userId}/reset-password", new ResetPasswordRequest("Reset1234"), Ct)).EnsureSuccessStatusCode();

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("reset-me@test.local", "Reset1234"), Ct);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(ApiFactory.Json, Ct))!;
        Assert.True(auth.User.MustChangePassword);
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
