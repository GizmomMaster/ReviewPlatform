namespace ReviewPlatform.Api.Auth;

/// <summary>Refresh-токен живёт в httpOnly-cookie, доступной только эндпоинтам /api/auth.</summary>
internal static class RefreshCookie
{
    public const string Name = "rp_refresh";
    private const string Path = "/api/auth";

    public static void Set(HttpContext context, string token, DateTime expiresAtUtc) =>
        context.Response.Cookies.Append(Name, token, Options(context, expiresAtUtc));

    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(Name, Options(context, null));

    public static string? Read(HttpContext context) => context.Request.Cookies[Name];

    private static CookieOptions Options(HttpContext context, DateTime? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Auth:SecureCookies", true),
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAtUtc,
    };
}
