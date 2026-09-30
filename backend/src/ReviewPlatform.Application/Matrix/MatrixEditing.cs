using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Matrix;

// Редактирование матрицы (ТЗ, 8.1). Права — Admin, проверяются политикой эндпоинтов.
// Запущенные сессии не затрагиваются: у них свой снимок индикаторов.

// ---------- Направления ----------

public sealed record CreateTrackCommand(string Code, string Name) : IRequest<TrackDto>;

internal sealed class CreateTrackValidator : AbstractValidator<CreateTrackCommand>
{
    public CreateTrackValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(64).Matches("^[a-zA-Z0-9-]+$").WithMessage("Код — латинские буквы, цифры и дефис.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateTrackHandler(IAppDbContext db) : IRequestHandler<CreateTrackCommand, TrackDto>
{
    public async Task<TrackDto> Handle(CreateTrackCommand request, CancellationToken cancellationToken)
    {
        var track = new Track(request.Code, request.Name);
        if (await db.Tracks.AnyAsync(t => t.Code == track.Code, cancellationToken))
        {
            throw new DomainException($"Направление с кодом {track.Code} уже есть.");
        }

        db.Tracks.Add(track);
        await db.SaveChangesAsync(cancellationToken);
        return new TrackDto(track.Id, track.Code, track.Name, track.IsActive);
    }
}

public sealed record UpdateTrackCommand(Guid Id, string Name, bool IsActive) : IRequest<TrackDto>;

internal sealed class UpdateTrackValidator : AbstractValidator<UpdateTrackCommand>
{
    public UpdateTrackValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
}

internal sealed class UpdateTrackHandler(IAppDbContext db) : IRequestHandler<UpdateTrackCommand, TrackDto>
{
    public async Task<TrackDto> Handle(UpdateTrackCommand request, CancellationToken cancellationToken)
    {
        var track = await db.Tracks.SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Track), request.Id);
        track.Rename(request.Name);
        track.SetActive(request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return new TrackDto(track.Id, track.Code, track.Name, track.IsActive);
    }
}

// ---------- Грейды ----------

public sealed record RenameGradeCommand(Guid Id, string Name) : IRequest<GradeDto>;

internal sealed class RenameGradeValidator : AbstractValidator<RenameGradeCommand>
{
    public RenameGradeValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
}

internal sealed class RenameGradeHandler(IAppDbContext db) : IRequestHandler<RenameGradeCommand, GradeDto>
{
    public async Task<GradeDto> Handle(RenameGradeCommand request, CancellationToken cancellationToken)
    {
        var grade = await db.Grades.SingleOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Grade), request.Id);
        grade.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);
        return new GradeDto(grade.Id, grade.Code, grade.Name, grade.Order);
    }
}

// ---------- Группы ----------

public sealed record CreateGroupCommand(Guid TrackId, string Name, string? Description) : IRequest<CompetencyGroupDto>;

internal sealed class CreateGroupValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(CompetencyGroup.MaxNameLength);
        RuleFor(c => c.Description).MaximumLength(2000);
    }
}

internal sealed class CreateGroupHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<CreateGroupCommand, CompetencyGroupDto>
{
    public async Task<CompetencyGroupDto> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Tracks.AnyAsync(t => t.Id == request.TrackId, cancellationToken))
        {
            throw new NotFoundException(nameof(Track), request.TrackId);
        }

        await editor.EnsureUniqueGroupNameAsync(request.TrackId, request.Name, exceptId: null, cancellationToken);
        var order = await db.CompetencyGroups.Where(g => g.TrackId == request.TrackId).MaxAsync(g => (int?)g.Order, cancellationToken) ?? 0;
        var group = new CompetencyGroup(request.TrackId, request.Name, order + 1, request.Description);
        db.CompetencyGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return MatrixEditor.ToDto(group);
    }
}

public sealed record UpdateGroupCommand(Guid Id, string Name, string? Description) : IRequest<CompetencyGroupDto>;

internal sealed class UpdateGroupValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(CompetencyGroup.MaxNameLength);
        RuleFor(c => c.Description).MaximumLength(2000);
    }
}

internal sealed class UpdateGroupHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<UpdateGroupCommand, CompetencyGroupDto>
{
    public async Task<CompetencyGroupDto> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await editor.LoadGroupAsync(request.Id, cancellationToken);
        await editor.EnsureUniqueGroupNameAsync(group.TrackId, request.Name, group.Id, cancellationToken);
        group.Update(request.Name, request.Description);
        await db.SaveChangesAsync(cancellationToken);
        return MatrixEditor.ToDto(group);
    }
}

/// <summary>Удаляется только пустая группа, индикаторы которой не попадали в сессии (архивные неиспользованные удаляются вместе с ней).</summary>
public sealed record DeleteGroupCommand(Guid Id) : IRequest;

internal sealed class DeleteGroupHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<DeleteGroupCommand>
{
    public async Task Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await editor.LoadGroupAsync(request.Id, cancellationToken);
        if (group.Indicators.Any(i => !i.IsArchived))
        {
            throw new DomainException("В группе есть индикаторы — сначала удалите или архивируйте их.");
        }

        var ids = group.Indicators.Select(i => i.Id).ToList();
        if (await db.SessionIndicators.AnyAsync(s => s.SourceIndicatorId != null && ids.Contains(s.SourceIndicatorId.Value), cancellationToken))
        {
            throw new DomainException("Индикаторы группы использовались в сессиях оценки — группу нельзя удалить.");
        }

        db.CompetencyGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ReorderGroupsCommand(Guid TrackId, IReadOnlyList<Guid> GroupIds) : IRequest;

internal sealed class ReorderGroupsHandler(IAppDbContext db) : IRequestHandler<ReorderGroupsCommand>
{
    public async Task Handle(ReorderGroupsCommand request, CancellationToken cancellationToken)
    {
        var groups = await db.CompetencyGroups.Where(g => g.TrackId == request.TrackId).ToListAsync(cancellationToken);
        if (groups.Count != request.GroupIds.Count || !groups.Select(g => g.Id).ToHashSet().SetEquals(request.GroupIds))
        {
            throw new DomainException("Список для сортировки не совпадает с группами направления — обновите страницу.");
        }

        var byId = groups.ToDictionary(g => g.Id);
        for (var i = 0; i < request.GroupIds.Count; i++)
        {
            byId[request.GroupIds[i]].SetOrder(i + 1);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---------- Индикаторы ----------

public sealed record CreateIndicatorCommand(Guid GroupId, Guid GradeId, string Text) : IRequest<IndicatorDto>;

internal sealed class CreateIndicatorValidator : AbstractValidator<CreateIndicatorCommand>
{
    public CreateIndicatorValidator()
    {
        RuleFor(c => c.GradeId).NotEmpty();
        RuleFor(c => c.Text).NotEmpty().MaximumLength(Indicator.MaxTextLength);
    }
}

internal sealed class CreateIndicatorHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<CreateIndicatorCommand, IndicatorDto>
{
    public async Task<IndicatorDto> Handle(CreateIndicatorCommand request, CancellationToken cancellationToken)
    {
        var group = await editor.LoadGroupAsync(request.GroupId, cancellationToken);
        if (!await db.Grades.AnyAsync(g => g.Id == request.GradeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Grade), request.GradeId);
        }

        var indicator = group.AddIndicator(request.GradeId, request.Text);
        await db.SaveChangesAsync(cancellationToken);
        return MatrixEditor.ToDto(indicator);
    }
}

public sealed record UpdateIndicatorCommand(Guid Id, string Text) : IRequest<IndicatorDto>;

internal sealed class UpdateIndicatorValidator : AbstractValidator<UpdateIndicatorCommand>
{
    public UpdateIndicatorValidator() => RuleFor(c => c.Text).NotEmpty().MaximumLength(Indicator.MaxTextLength);
}

internal sealed class UpdateIndicatorHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<UpdateIndicatorCommand, IndicatorDto>
{
    public async Task<IndicatorDto> Handle(UpdateIndicatorCommand request, CancellationToken cancellationToken)
    {
        var group = await editor.LoadGroupOfIndicatorAsync(request.Id, cancellationToken);
        var indicator = group.EditIndicator(request.Id, request.Text);
        await db.SaveChangesAsync(cancellationToken);
        return MatrixEditor.ToDto(indicator);
    }
}

public sealed record ArchiveIndicatorCommand(Guid Id) : IRequest;

internal sealed class ArchiveIndicatorHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<ArchiveIndicatorCommand>
{
    public async Task Handle(ArchiveIndicatorCommand request, CancellationToken cancellationToken)
    {
        var group = await editor.LoadGroupOfIndicatorAsync(request.Id, cancellationToken);
        group.ArchiveIndicator(request.Id);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Физическое удаление — только если индикатор не попадал ни в одну сессию (ТЗ, 8.1.3), иначе — архив.</summary>
public sealed record DeleteIndicatorCommand(Guid Id) : IRequest;

internal sealed class DeleteIndicatorHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<DeleteIndicatorCommand>
{
    public async Task Handle(DeleteIndicatorCommand request, CancellationToken cancellationToken)
    {
        var group = await editor.LoadGroupOfIndicatorAsync(request.Id, cancellationToken);
        if (await db.SessionIndicators.AnyAsync(s => s.SourceIndicatorId == request.Id, cancellationToken))
        {
            throw new DomainException("Индикатор использовался в сессиях оценки — его можно только архивировать.");
        }

        group.RemoveIndicator(request.Id);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ReorderIndicatorsCommand(Guid GroupId, Guid GradeId, IReadOnlyList<Guid> IndicatorIds) : IRequest;

internal sealed class ReorderIndicatorsHandler(IAppDbContext db, MatrixEditor editor) : IRequestHandler<ReorderIndicatorsCommand>
{
    public async Task Handle(ReorderIndicatorsCommand request, CancellationToken cancellationToken)
    {
        var group = await editor.LoadGroupAsync(request.GroupId, cancellationToken);
        group.ReorderIndicators(request.GradeId, request.IndicatorIds);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---------- Общее ----------

internal sealed class MatrixEditor(IAppDbContext db)
{
    public async Task<CompetencyGroup> LoadGroupAsync(Guid id, CancellationToken cancellationToken) =>
        await db.CompetencyGroups.Include(g => g.Indicators).SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(CompetencyGroup), id);

    public async Task<CompetencyGroup> LoadGroupOfIndicatorAsync(Guid indicatorId, CancellationToken cancellationToken)
    {
        var groupId = await db.Indicators.Where(i => i.Id == indicatorId).Select(i => (Guid?)i.GroupId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Indicator), indicatorId);
        return await LoadGroupAsync(groupId, cancellationToken);
    }

    public async Task EnsureUniqueGroupNameAsync(Guid trackId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        // Групп в направлении единицы — сравниваем без учёта регистра в памяти
        var names = await db.CompetencyGroups.Where(g => g.TrackId == trackId && g.Id != exceptId).Select(g => g.Name).ToListAsync(cancellationToken);
        if (names.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Группа «{name.Trim()}» уже есть в этом направлении.");
        }
    }

    public static CompetencyGroupDto ToDto(CompetencyGroup group) =>
        new(group.Id, group.Name, group.Description, group.Order, [.. group.Indicators.Where(i => !i.IsArchived).OrderBy(i => i.Order).Select(ToDto)]);

    public static IndicatorDto ToDto(Indicator indicator) => new(indicator.Id, indicator.GradeId, indicator.Text, indicator.Order);
}
