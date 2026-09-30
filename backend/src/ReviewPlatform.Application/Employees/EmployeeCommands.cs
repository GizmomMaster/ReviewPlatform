using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Employees;

namespace ReviewPlatform.Application.Employees;

/// <param name="ManagerUserId">Обязателен для администратора; руководитель всегда назначается сам.</param>
public sealed record CreateEmployeeCommand(string FullName, string Email, Guid TrackId, Guid GradeId, Guid? ManagerUserId) : IRequest<EmployeeDto>;

public sealed record UpdateEmployeeCommand(Guid Id, string FullName, string Email, Guid TrackId, Guid GradeId, Guid? ManagerUserId, bool IsActive)
    : IRequest<EmployeeDto>;

public sealed record ArchiveEmployeeCommand(Guid Id) : IRequest;

internal sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.TrackId).NotEmpty();
        RuleFor(c => c.GradeId).NotEmpty();
    }
}

internal sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.TrackId).NotEmpty();
        RuleFor(c => c.GradeId).NotEmpty();
    }
}

/// <summary>Общие проверки ссылок сотрудника: направление, грейд, руководитель, уникальность email.</summary>
internal sealed class EmployeeRules(IAppDbContext db, IIdentityService identity, ICurrentUser currentUser)
{
    public async Task<Guid> ResolveManagerAsync(Guid? requested, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            return currentUser.UserId;
        }

        if (requested is not { } managerId)
        {
            throw Invalid(nameof(CreateEmployeeCommand.ManagerUserId), "Укажите руководителя.");
        }

        var manager = await identity.FindUserAsync(managerId, cancellationToken);
        if (manager is not { IsActive: true })
        {
            throw Invalid(nameof(CreateEmployeeCommand.ManagerUserId), "Руководитель не найден или заблокирован.");
        }

        return managerId;
    }

    public async Task EnsureValidAsync(Guid? employeeId, string email, Guid trackId, Guid gradeId, CancellationToken cancellationToken)
    {
        if (!await db.Tracks.AnyAsync(t => t.Id == trackId && t.IsActive, cancellationToken))
        {
            throw Invalid(nameof(CreateEmployeeCommand.TrackId), "Направление не найдено.");
        }

        if (!await db.Grades.AnyAsync(g => g.Id == gradeId, cancellationToken))
        {
            throw Invalid(nameof(CreateEmployeeCommand.GradeId), "Грейд не найден.");
        }

        var normalized = Employee.NormalizeEmail(email);
        if (await db.Employees.AnyAsync(e => e.Email == normalized && e.Id != employeeId, cancellationToken))
        {
            throw new DomainException($"Сотрудник с email {normalized} уже есть в системе.");
        }
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new FluentValidation.Results.ValidationFailure(property, message)]);
}

internal sealed class CreateEmployeeHandler(IAppDbContext db, EmployeeRules rules, EmployeeReader reader)
    : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var managerId = await rules.ResolveManagerAsync(request.ManagerUserId, cancellationToken);
        await rules.EnsureValidAsync(null, request.Email, request.TrackId, request.GradeId, cancellationToken);

        var employee = db.Employees.Add(new Employee(request.FullName, request.Email, request.TrackId, request.GradeId, managerId)).Entity;
        await db.SaveChangesAsync(cancellationToken);

        return await reader.GetAsync(employee.Id, cancellationToken);
    }
}

internal sealed class UpdateEmployeeHandler(IAppDbContext db, EmployeeRules rules, EmployeeReader reader, ICurrentUser currentUser)
    : IRequestHandler<UpdateEmployeeCommand, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await reader.Visible().SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        var managerId = currentUser.IsAdmin
            ? await rules.ResolveManagerAsync(request.ManagerUserId, cancellationToken)
            : employee.ManagerUserId;
        await rules.EnsureValidAsync(employee.Id, request.Email, request.TrackId, request.GradeId, cancellationToken);

        employee.Update(request.FullName, request.Email, request.TrackId, request.GradeId, managerId);
        if (request.IsActive) employee.Restore(); else employee.Archive();
        await db.SaveChangesAsync(cancellationToken);

        return await reader.GetAsync(employee.Id, cancellationToken);
    }
}

internal sealed class ArchiveEmployeeHandler(IAppDbContext db, EmployeeReader reader) : IRequestHandler<ArchiveEmployeeCommand>
{
    public async Task Handle(ArchiveEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await reader.Visible().SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        employee.Archive();
        await db.SaveChangesAsync(cancellationToken);
    }
}
