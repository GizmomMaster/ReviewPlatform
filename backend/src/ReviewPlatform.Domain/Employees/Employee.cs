using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Employees;

public sealed class Employee : Entity
{
    private Employee() { }

    public Employee(string fullName, string email, Guid trackId, Guid currentGradeId, Guid managerUserId)
    {
        Update(fullName, email, trackId, currentGradeId, managerUserId);
        IsActive = true;
    }

    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public Guid TrackId { get; private set; }
    public Guid CurrentGradeId { get; private set; }
    public Guid ManagerUserId { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string fullName, string email, Guid trackId, Guid currentGradeId, Guid managerUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        FullName = fullName.Trim();
        Email = NormalizeEmail(email);
        TrackId = trackId;
        CurrentGradeId = currentGradeId;
        ManagerUserId = managerUserId;
    }

    public void Archive() => IsActive = false;

    public void Restore() => IsActive = true;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
