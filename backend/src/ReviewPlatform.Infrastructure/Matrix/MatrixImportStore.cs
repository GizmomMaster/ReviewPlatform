using Microsoft.Extensions.Caching.Memory;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Infrastructure.Matrix;

/// <summary>
/// Разобранный файл живёт в памяти процесса 30 минут. Импорт — редкое действие администратора:
/// после перезапуска приложения достаточно загрузить файл ещё раз.
/// </summary>
internal sealed class MatrixImportStore(IMemoryCache cache) : IMatrixImportStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public Guid Save(Guid trackId, IReadOnlyList<MatrixImportRow> rows)
    {
        var id = Guid.CreateVersion7();
        cache.Set(Key(id), (trackId, rows), Lifetime);
        return id;
    }

    public IReadOnlyList<MatrixImportRow>? Find(Guid importId, Guid trackId) =>
        cache.TryGetValue(Key(importId), out (Guid TrackId, IReadOnlyList<MatrixImportRow> Rows) entry) && entry.TrackId == trackId ? entry.Rows : null;

    public void Remove(Guid importId) => cache.Remove(Key(importId));

    private static string Key(Guid id) => $"matrix-import:{id}";
}
