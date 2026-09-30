namespace ReviewPlatform.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";

    public static readonly IReadOnlyList<string> All = [Admin, Manager];
}
