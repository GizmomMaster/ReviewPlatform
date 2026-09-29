using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Sessions;

public sealed record SessionListItemDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    SessionType Type,
    string CurrentGradeCode,
    string? TargetGradeCode,
    SessionStatus Status,
    DateTime DeadlineAtUtc,
    int ParticipantsTotal,
    int ParticipantsSubmitted,
    Guid OwnerUserId,
    string OwnerName,
    DateTime CreatedAtUtc);

public sealed record ParticipantDto(
    Guid Id,
    string FullName,
    string Email,
    EvaluatorRole Role,
    ParticipantStatus Status,
    DateTime? TokenIssuedAtUtc,
    DateTime? FirstOpenedAtUtc,
    DateTime? SubmittedAtUtc);

public sealed record RoleRequirementDto(EvaluatorRole Role, int MinCount, int MaxCount, int Count, bool IsSatisfied);

public sealed record SessionDetailsDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeEmail,
    string TrackName,
    SessionType Type,
    GradeDto CurrentGrade,
    GradeDto? TargetGrade,
    SessionStatus Status,
    DateTime DeadlineAtUtc,
    DateTime CreatedAtUtc,
    DateTime? LaunchedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ClosedAtUtc,
    Guid OwnerUserId,
    string OwnerName,
    int IndicatorCount,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<RoleRequirementDto> RoleRequirements);

/// <summary>Ссылка на анкету. Показывается один раз — токен в открытом виде не хранится.</summary>
public sealed record ParticipantLinkDto(Guid ParticipantId, string FullName, EvaluatorRole Role, string Url);

public sealed record PreviewIndicatorDto(string Text, string GradeCode, LevelKind LevelKind);

public sealed record PreviewGroupDto(string Name, IReadOnlyList<PreviewIndicatorDto> Indicators);

public sealed record SurveyPreviewDto(int IndicatorCount, IReadOnlyList<PreviewGroupDto> Groups);
