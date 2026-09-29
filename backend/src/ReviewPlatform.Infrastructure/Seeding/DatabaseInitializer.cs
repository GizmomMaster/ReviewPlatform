using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Matrix;
using ReviewPlatform.Infrastructure.Identity;
using ReviewPlatform.Infrastructure.Persistence;

namespace ReviewPlatform.Infrastructure.Seeding;

/// <summary>
/// Применяет миграции и заполняет справочники. Идемпотентен: существующие данные не трогает.
/// Выполняется под advisory-lock, чтобы несколько экземпляров приложения не заполняли БД одновременно.
/// </summary>
public sealed partial class DatabaseInitializer(
    AppDbContext db,
    IOptions<SeedOptions> options,
    IOptions<BootstrapAdminOptions> adminOptions,
    RoleManager<IdentityRole<Guid>> roles,
    UserManager<AppUser> users,
    ILogger<DatabaseInitializer> logger)
{
    private const long InitializationLockKey = 0x5245_5649_4557; // "REVIEW"

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({InitializationLockKey})", cancellationToken);
            try
            {
                await db.Database.MigrateAsync(cancellationToken);

                var grades = await SeedGradesAsync(cancellationToken);
                await SeedRoleRulesAsync(grades, cancellationToken);
                var track = await SeedBackendTrackAsync(cancellationToken);
                await SeedMatrixAsync(track, grades, cancellationToken);
                await SeedRolesAsync();
                await SeedBootstrapAdminAsync();
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({InitializationLockKey})", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<Dictionary<string, Grade>> SeedGradesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Grades.ToDictionaryAsync(g => g.Code, cancellationToken);
        for (var i = 0; i < ReferenceData.Grades.Length; i++)
        {
            var (code, name) = ReferenceData.Grades[i];
            if (!existing.ContainsKey(code))
            {
                existing[code] = db.Grades.Add(new Grade(code, name, i + 1)).Entity;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task SeedRoleRulesAsync(Dictionary<string, Grade> grades, CancellationToken cancellationToken)
    {
        if (await db.GradeRoleRules.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var grade in grades.Values)
        {
            foreach (var (role, min, max) in ReferenceData.RoleRulesFor(grade.Code))
            {
                db.GradeRoleRules.Add(new GradeRoleRule(grade.Id, role, min, max));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Track> SeedBackendTrackAsync(CancellationToken cancellationToken)
    {
        var track = await db.Tracks.SingleOrDefaultAsync(t => t.Code == ReferenceData.BackendTrackCode, cancellationToken);
        if (track is null)
        {
            track = db.Tracks.Add(new Track(ReferenceData.BackendTrackCode, "Backend")).Entity;
            await db.SaveChangesAsync(cancellationToken);
        }

        return track;
    }

    private async Task SeedMatrixAsync(Track track, Dictionary<string, Grade> grades, CancellationToken cancellationToken)
    {
        if (await db.CompetencyGroups.AnyAsync(g => g.TrackId == track.Id, cancellationToken))
        {
            return;
        }

        var path = Path.IsPathRooted(options.Value.MatrixFile)
            ? options.Value.MatrixFile
            : Path.Combine(AppContext.BaseDirectory, options.Value.MatrixFile);
        if (!File.Exists(path))
        {
            LogMatrixFileMissing(path);
            return;
        }

        MatrixReadResult result;
        await using (var stream = File.OpenRead(path))
        {
            result = MatrixSpreadsheet.Read(stream);
        }

        var unknownGrades = result.Rows.Select(r => r.GradeCode).Distinct().Where(c => !grades.ContainsKey(c)).ToList();
        var foreignTracks = result.Rows.Select(r => r.TrackCode).Distinct().Where(c => c != track.Code).ToList();
        if (result.Errors.Count > 0 || unknownGrades.Count > 0 || foreignTracks.Count > 0)
        {
            var problems = result.Errors
                .Concat(unknownGrades.Select(c => $"Неизвестный грейд {c}."))
                .Concat(foreignTracks.Select(c => $"Неожиданное направление {c}."));
            throw new InvalidOperationException($"Начальная матрица {path} содержит ошибки:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        }

        foreach (var groupRows in result.Rows.GroupBy(r => (r.GroupName, r.GroupOrder)).OrderBy(g => g.Key.GroupOrder))
        {
            var group = new CompetencyGroup(track.Id, groupRows.Key.GroupName, groupRows.Key.GroupOrder);
            foreach (var row in groupRows)
            {
                group.AddIndicator(grades[row.GradeCode].Id, row.Text, row.Order);
            }

            db.CompetencyGroups.Add(group);
        }

        await db.SaveChangesAsync(cancellationToken);
        LogMatrixSeeded(result.Rows.Count, track.Code);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                ThrowIfFailed(await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }));
            }
        }
    }

    /// <summary>Первый администратор — только если пользователей ещё нет и заданы учётные данные.</summary>
    private async Task SeedBootstrapAdminAsync()
    {
        if (await users.Users.AnyAsync())
        {
            return;
        }

        var admin = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            LogNoBootstrapAdmin();
            return;
        }

        var email = admin.Email.Trim().ToLowerInvariant();
        var user = new AppUser { UserName = email, Email = email, FullName = admin.FullName, MustChangePassword = true };
        ThrowIfFailed(await users.CreateAsync(user, admin.Password));
        ThrowIfFailed(await users.AddToRoleAsync(user, Roles.Admin));
        LogBootstrapAdminCreated(email);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No users and BootstrapAdmin is not configured: nobody can sign in.")]
    private partial void LogNoBootstrapAdmin();

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrap admin {Email} created; password change is required on first sign-in.")]
    private partial void LogBootstrapAdminCreated(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed matrix file not found: {Path}. Matrix left empty.")]
    private partial void LogMatrixFileMissing(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} indicators for track {Track}.")]
    private partial void LogMatrixSeeded(int count, string track);
}
