using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Notifications;
using ReviewPlatform.Infrastructure.Email;
using ReviewPlatform.Infrastructure.Identity;
using ReviewPlatform.Infrastructure.Persistence;
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

    /// <summary>Вместо SMTP: письма копятся в памяти. Фоновые отправщик и планировщик в тестах выключены.</summary>
    internal FakeEmailSender Mail { get; } = new();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-0123456789-0123456789-abcdef");
        builder.UseSetting("Auth:SecureCookies", "false");
        builder.UseSetting("FrontendBaseUrl", "http://localhost:8080");
        builder.UseSetting("RateLimits:SurveyPerMinute", "100000");
        builder.UseSetting("RateLimits:LoginPerMinute", "100000");
        builder.UseSetting("BootstrapAdmin:Email", BootstrapAdminEmail);
        builder.UseSetting("BootstrapAdmin:Password", BootstrapAdminPassword);
        builder.UseSetting("Outbox:Enabled", "false");
        builder.UseSetting("Scheduler:Enabled", "false");
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Mail));
    }

    /// <summary>Команда MediatR вне HTTP — так тесты запускают задачи планировщика.</summary>
    public async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, TestContext.Current.CancellationToken);
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Письма в outbox для получателя, старые первыми.</summary>
    public Task<List<OutboxEmail>> OutboxAsync(string to) =>
        WithDbAsync(db => db.EmailOutbox.Where(e => e.To == to).OrderBy(e => e.CreatedAtUtc).ToListAsync(TestContext.Current.CancellationToken));

    public async Task<int> ProcessOutboxBatchAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(TestContext.Current.CancellationToken);
    }

    public async Task<string> UserEmailAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(userId.ToString()))!.Email!;
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
