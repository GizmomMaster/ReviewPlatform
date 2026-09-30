using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Users;
using ReviewPlatform.Infrastructure.Persistence;

namespace ReviewPlatform.Infrastructure.Identity;

internal sealed class IdentityService(AppDbContext db, UserManager<AppUser> users, AuthService auth) : IIdentityService
{
    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken cancellationToken) =>
        await WithRoles(db.Users.OrderBy(u => u.FullName)).ToListAsync(cancellationToken);

    public async Task<UserDto?> FindUserAsync(Guid id, CancellationToken cancellationToken) =>
        await WithRoles(db.Users.Where(u => u.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<Guid> CreateUserAsync(string email, string fullName, string role, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = new AppUser { UserName = normalizedEmail, Email = normalizedEmail, FullName = fullName.Trim(), MustChangePassword = true };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        IdentityErrors.ThrowIfFailed(await users.CreateAsync(user, password), "Password");
        IdentityErrors.ThrowIfFailed(await users.AddToRoleAsync(user, role), "Role");
        await transaction.CommitAsync(cancellationToken);

        return user.Id;
    }

    public async Task UpdateUserAsync(Guid id, string fullName, string role, bool isActive, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User", id);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        user.FullName = fullName.Trim();
        user.IsActive = isActive;
        IdentityErrors.ThrowIfFailed(await users.UpdateAsync(user), "FullName");

        var currentRoles = await users.GetRolesAsync(user);
        if (!currentRoles.SequenceEqual([role]))
        {
            IdentityErrors.ThrowIfFailed(await users.RemoveFromRolesAsync(user, currentRoles), "Role");
            IdentityErrors.ThrowIfFailed(await users.AddToRoleAsync(user, role), "Role");
        }

        if (!isActive)
        {
            await auth.RevokeAllAsync(user.Id, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User", id);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        IdentityErrors.ThrowIfFailed(await users.RemovePasswordAsync(user), "NewPassword");
        IdentityErrors.ThrowIfFailed(await users.AddPasswordAsync(user, newPassword), "NewPassword");
        user.MustChangePassword = true;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await auth.RevokeAllAsync(user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

    /// <summary>Проекция в DTO — последним шагом: фильтры и сортировку EF должен применить к сущности.</summary>
    private IQueryable<UserDto> WithRoles(IQueryable<AppUser> users) =>
        users.Select(u => new UserDto(
            u.Id,
            u.Email!,
            u.FullName,
            db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).FirstOrDefault() ?? "",
            u.IsActive,
            u.MustChangePassword));
}
