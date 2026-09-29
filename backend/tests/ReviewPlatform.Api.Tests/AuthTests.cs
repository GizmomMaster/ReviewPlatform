using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Users;

namespace ReviewPlatform.Api.Tests;

public sealed class AuthTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await factory.CreateUserAsync(Roles.Manager, "wrong-password@test.local");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("wrong-password@test.local", "nope"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BootstrapAdmin_MustChangePassword_BeforeUsingApi()
    {
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.BootstrapAdminEmail, ApiFactory.BootstrapAdminPassword), Ct);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(ApiFactory.Json, Ct))!;
        Assert.True(auth.User.MustChangePassword);
        Assert.Equal(Roles.Admin, auth.User.Role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/employees", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);

        var weak = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(ApiFactory.BootstrapAdminPassword, "short"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var changed = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(ApiFactory.BootstrapAdminPassword, "NewStrong1"), Ct);
        changed.EnsureSuccessStatusCode();
        var newAuth = (await changed.Content.ReadFromJsonAsync<AuthResponse>(ApiFactory.Json, Ct))!;
        Assert.False(newAuth.User.MustChangePassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newAuth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees", Ct)).StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenIsRejected()
    {
        var (userId, _) = await factory.SignInAsNewUserAsync(Roles.Manager);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var email = (await ListUsersAsAdmin()).Single(u => u.Id == userId).Email;

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, ApiFactory.DefaultPassword), Ct);
        var firstCookie = RefreshCookieOf(login);

        var refreshed = await PostWithCookie(client, "/api/auth/refresh", firstCookie);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var secondCookie = RefreshCookieOf(refreshed);
        Assert.NotEqual(firstCookie, secondCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWithCookie(client, "/api/auth/refresh", firstCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostWithCookie(client, "/api/auth/refresh", secondCookie)).StatusCode);
    }

    [Fact]
    public async Task BlockedUser_CannotSignIn()
    {
        using var admin = await factory.SignInAsAdminAsync();
        var created = await admin.PostAsJsonAsync("/api/users", new CreateUserCommand("to-block@test.local", "Будет заблокирован", Roles.Manager, ApiFactory.DefaultPassword), Ct);
        var user = (await created.Content.ReadFromJsonAsync<UserDto>(ApiFactory.Json, Ct))!;

        (await admin.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest(user.FullName, Roles.Manager, IsActive: false), Ct)).EnsureSuccessStatusCode();

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("to-block@test.local", ApiFactory.DefaultPassword), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private async Task<List<UserDto>> ListUsersAsAdmin()
    {
        using var admin = await factory.SignInAsAdminAsync();
        return (await admin.GetFromJsonAsync<List<UserDto>>("/api/users", ApiFactory.Json, Ct))!;
    }

    private static string RefreshCookieOf(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rp_refresh=", StringComparison.Ordinal)).Split(';')[0];

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string url, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request, Ct);
    }
}
