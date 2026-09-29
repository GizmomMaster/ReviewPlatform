using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Infrastructure.Identity;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(ReviewPlatform.Api.Tests.ApiFactory))]

namespace ReviewPlatform.Api.Tests;

/// <summary>API поверх настоящего Postgres в контейнере. Один экземпляр на весь прогон тестов.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BootstrapAdminEmail = "bootstrap-admin@test.local";
    public const string BootstrapAdminPassword = "Bootstrap1";
    public const string DefaultPassword = "Passw0rd!";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-0123456789-0123456789-abcdef");
        builder.UseSetting("Auth:SecureCookies", "false");
        builder.UseSetting("FrontendBaseUrl", "http://localhost:8080");
        builder.UseSetting("BootstrapAdmin:Email", BootstrapAdminEmail);
        builder.UseSetting("BootstrapAdmin:Password", BootstrapAdminPassword);
    }

    /// <summary>Создаёт активного пользователя с постоянным паролем и возвращает его id.</summary>
    public async Task<Guid> CreateUserAsync(string role, string? email = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        email ??= $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        var user = new AppUser { UserName = email, Email = email, FullName = $"{role} {email[..8]}" };
        Assert.True((await users.CreateAsync(user, DefaultPassword)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }

    /// <summary>Новый пользователь с ролью и HTTP-клиент, авторизованный от его имени.</summary>
    public async Task<(Guid UserId, HttpClient Client)> SignInAsNewUserAsync(string role)
    {
        var id = await CreateUserAsync(role);
        await using var scope = Services.CreateAsyncScope();
        var email = (await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(id.ToString()))!.Email!;
        return (id, await SignInAsync(email, DefaultPassword));
    }

    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public Task<HttpClient> SignInAsAdminAsync() => SignInAsNewUserAsync(Roles.Admin).ContinueWith(t => t.Result.Client, TaskScheduler.Default);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
