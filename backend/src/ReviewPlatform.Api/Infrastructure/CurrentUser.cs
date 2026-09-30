using ReviewPlatform.Application.Common;
using ReviewPlatform.Infrastructure.Identity;

namespace ReviewPlatform.Api.Infrastructure;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(ClaimNames.Subject)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("Request is not authenticated.");

    public bool IsAdmin => accessor.HttpContext?.User.IsInRole(Roles.Admin) ?? false;
}
