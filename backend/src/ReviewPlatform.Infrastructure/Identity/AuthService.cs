using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using ValidationFailure = FluentValidation.Results.ValidationFailure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReviewPlatform.Infrastructure.Persistence;

namespace ReviewPlatform.Infrastructure.Identity;

public sealed record AuthUser(Guid Id, string Email, string FullName, string Role, bool MustChangePassword);

public sealed record AuthTokens(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc, AuthUser User);

/// <summary>Вход, обновление и отзыв токенов, смена пароля.</summary>
public sealed class AuthService(AppDbContext db, UserManager<AppUser> users, IOptions<JwtOptions> jwtOptions, TimeProvider time)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    /// <returns>null — неверный email/пароль, пользователь заблокирован или временно заблокирован за перебор.</returns>
    public async Task<AuthTokens?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is not { IsActive: true } || await users.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }

        await users.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <returns>null — токен неизвестен, отозван, истёк или пользователь заблокирован.</returns>
    public async Task<AuthTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null || !stored.IsActive(now))
        {
            return null;
        }

        var user = await users.FindByIdAsync(stored.UserId.ToString());
        if (user is not { IsActive: true })
        {
            return null;
        }

        stored.RevokedAtUtc = now;
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, time.GetUtcNow().UtcDateTime), cancellationToken);
    }

    /// <summary>Меняет пароль, отзывает все refresh-токены пользователя и выдаёт новые.</summary>
    public async Task<AuthTokens> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException($"User {userId} not found.");

        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        IdentityErrors.ThrowIfFailed(result, "NewPassword");

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await RevokeAllAsync(user.Id, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthUser?> GetUserAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is { IsActive: true } ? await ToAuthUserAsync(user) : null;
    }

    internal async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, time.GetUtcNow().UtcDateTime), cancellationToken);

    private async Task<AuthTokens> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var authUser = await ToAuthUserAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.Id.ToString()),
            new(ClaimNames.Email, user.Email!),
            new(ClaimNames.Name, user.FullName),
            new(ClaimNames.Role, authUser.Role),
        };
        if (user.MustChangePassword)
        {
            claims.Add(new Claim(ClaimNames.PasswordChangeRequired, "true"));
        }

        var accessExpires = now + _jwt.AccessTokenLifetime;
        var accessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            _jwt.Issuer,
            _jwt.Audience,
            claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: new SigningCredentials(SigningKey(_jwt), SecurityAlgorithms.HmacSha256)));

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var refreshExpires = now + _jwt.RefreshTokenLifetime;
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = Hash(refreshToken), CreatedAtUtc = now, ExpiresAtUtc = refreshExpires });
        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokens(accessToken, accessExpires, refreshToken, refreshExpires, authUser);
    }

    private async Task<AuthUser> ToAuthUserAsync(AppUser user)
    {
        var role = (await users.GetRolesAsync(user)).FirstOrDefault() ?? "";
        return new AuthUser(user.Id, user.Email!, user.FullName, role, user.MustChangePassword);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.SigningKey));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal static class IdentityErrors
{
    public static void ThrowIfFailed(IdentityResult result, string property)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure(property, Translate(e))));
        }
    }

    private static string Translate(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) => "Пароль слишком короткий (минимум 8 символов).",
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "Пароль должен содержать цифру.",
        nameof(IdentityErrorDescriber.PasswordRequiresLower) => "Пароль должен содержать строчную букву.",
        nameof(IdentityErrorDescriber.PasswordRequiresUpper) => "Пароль должен содержать заглавную букву.",
        nameof(IdentityErrorDescriber.PasswordMismatch) => "Текущий пароль указан неверно.",
        nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) => "Пользователь с таким email уже существует.",
        nameof(IdentityErrorDescriber.InvalidEmail) => "Некорректный email.",
        _ => error.Description,
    };
}
