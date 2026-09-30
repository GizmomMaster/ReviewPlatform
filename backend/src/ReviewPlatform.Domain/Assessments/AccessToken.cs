using System.Security.Cryptography;
using System.Text;

namespace ReviewPlatform.Domain.Assessments;

/// <summary>Персональный токен респондента: 32 случайных байта в base64url. В БД хранится только SHA-256.</summary>
public readonly record struct AccessToken(string Value, string Hash)
{
    public static AccessToken Create()
    {
        var value = Base64Url(RandomNumberGenerator.GetBytes(32));
        return new AccessToken(value, HashOf(value));
    }

    public static string HashOf(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
