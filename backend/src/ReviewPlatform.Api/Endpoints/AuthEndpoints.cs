using Microsoft.AspNetCore.Http.HttpResults;
using ReviewPlatform.Api.Auth;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Infrastructure.Identity;

namespace ReviewPlatform.Api.Endpoints;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, AuthUser User);

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> (LoginRequest request, AuthService service, HttpContext http, CancellationToken ct) =>
            await service.LoginAsync(request.Email ?? "", request.Password ?? "", ct) is { } tokens
                ? TypedResults.Ok(Respond(http, tokens))
                : TypedResults.Problem(title: "Неверный email или пароль", statusCode: StatusCodes.Status401Unauthorized))
            .AllowAnonymous()
            .WithName("Login");

        auth.MapPost("/refresh", async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> (AuthService service, HttpContext http, CancellationToken ct) =>
        {
            if (RefreshCookie.Read(http) is { } token && await service.RefreshAsync(token, ct) is { } tokens)
            {
                return TypedResults.Ok(Respond(http, tokens));
            }

            RefreshCookie.Clear(http);
            return TypedResults.Problem(title: "Сессия истекла", statusCode: StatusCodes.Status401Unauthorized);
        })
            .AllowAnonymous()
            .WithName("RefreshToken");

        auth.MapPost("/logout", async (AuthService service, HttpContext http, CancellationToken ct) =>
        {
            if (RefreshCookie.Read(http) is { } token)
            {
                await service.LogoutAsync(token, ct);
            }

            RefreshCookie.Clear(http);
            return TypedResults.NoContent();
        })
            .AllowAnonymous()
            .WithName("Logout");

        auth.MapPost("/change-password", async (ChangePasswordRequest request, AuthService service, ICurrentUser user, HttpContext http, CancellationToken ct) =>
            TypedResults.Ok(Respond(http, await service.ChangePasswordAsync(user.UserId, request.CurrentPassword ?? "", request.NewPassword ?? "", ct))))
            .RequireAuthorization(Policies.PasswordChangeAllowed)
            .WithName("ChangePassword");

        auth.MapGet("/me", async Task<Results<Ok<AuthUser>, UnauthorizedHttpResult>> (AuthService service, ICurrentUser user) =>
            await service.GetUserAsync(user.UserId) is { } me ? TypedResults.Ok(me) : TypedResults.Unauthorized())
            .RequireAuthorization(Policies.PasswordChangeAllowed)
            .WithName("GetCurrentUser");

        return app;
    }

    private static AuthResponse Respond(HttpContext http, AuthTokens tokens)
    {
        RefreshCookie.Set(http, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
        return new AuthResponse(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.User);
    }
}
