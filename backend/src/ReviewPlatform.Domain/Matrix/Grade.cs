using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Matrix;

/// <summary>Грейд E1…E8. Общий для всех направлений.</summary>
public sealed class Grade : Entity
{
    private Grade() { }

    public Grade(string code, string name, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);
        Code = code.Trim().ToUpperInvariant();
        Order = order;
        Rename(name);
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int Order { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
}
