namespace ReviewPlatform.Application.Employees;

public sealed record EmployeeDto(
    Guid Id,
    string FullName,
    string Email,
    Guid TrackId,
    string TrackName,
    Guid GradeId,
    string GradeCode,
    string GradeName,
    Guid ManagerUserId,
    string ManagerName,
    bool IsActive);
