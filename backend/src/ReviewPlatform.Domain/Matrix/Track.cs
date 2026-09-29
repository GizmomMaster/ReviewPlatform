using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Matrix;

/// <summary>Направление (Backend, Frontend, ...) со своей матрицей компетенций.</summary>
public sealed class Track : Entity
{
    private Track() { }

    public Track(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim().ToLowerInvariant();
        Rename(name);
        IsActive = true;
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
}
