using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReviewPlatform.Api.Infrastructure;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Infrastructure.Identity;

namespace ReviewPlatform.Api.Auth;

internal static class Policies
{
    public const string Admin = "Admin";

    /// <summary>Любой вошедший пользователь, в том числе с временным паролем (смена пароля, профиль).</summary>
    public const string PasswordChangeAllowed = "PasswordChangeAllowed";
}

internal static class AuthSetup
{
    public static IServiceCollection AddApiAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = AuthService.SigningKey(jwt.Value),
                    NameClaimType = ClaimNames.Name,
                    RoleClaimType = ClaimNames.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            // По умолчанию — только вошедшие пользователи со сменённым временным паролем
            .SetFallbackPolicy(PasswordChangedPolicy().Build())
            .AddPolicy(Policies.Admin, PasswordChangedPolicy().RequireRole(Roles.Admin).Build())
            .AddPolicy(Policies.PasswordChangeAllowed, policy => policy.RequireAuthenticatedUser());

        return services;
    }

    private static AuthorizationPolicyBuilder PasswordChangedPolicy() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => !ctx.User.HasClaim(c => c.Type == ClaimNames.PasswordChangeRequired));
}
