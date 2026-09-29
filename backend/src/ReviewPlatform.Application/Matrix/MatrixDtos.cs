namespace ReviewPlatform.Application.Matrix;

public sealed record TrackDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record GradeDto(Guid Id, string Code, string Name, int Order);

public sealed record IndicatorDto(Guid Id, Guid GradeId, string Text, int Order);

public sealed record CompetencyGroupDto(
    Guid Id,
    string Name,
    string? Description,
    int Order,
    IReadOnlyList<IndicatorDto> Indicators);

public sealed record MatrixDto(TrackDto Track, IReadOnlyList<GradeDto> Grades, IReadOnlyList<CompetencyGroupDto> Groups);
