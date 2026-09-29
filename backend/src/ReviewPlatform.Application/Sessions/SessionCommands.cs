using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Employees;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Sessions;

public sealed record NewParticipant(string FullName, string Email, EvaluatorRole Role);

internal sealed class NewParticipantValidator : AbstractValidator<NewParticipant>
{
    public NewParticipantValidator()
    {
        RuleFor(p => p.FullName).NotEmpty().MaximumLength(200);
        RuleFor(p => p.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(p => p.Role).IsInEnum().NotEqual(EvaluatorRole.Self).WithMessage("Самооценка добавляется автоматически.");
    }
}

// ---------- Создание ----------

/// <summary>Черновик сессии. Самооценка добавляется автоматически; владелец — руководитель сотрудника.</summary>
public sealed record CreateSessionCommand(Guid EmployeeId, SessionType Type, DateTime DeadlineAtUtc, IReadOnlyList<NewParticipant> Participants)
    : IRequest<SessionDetailsDto>;

internal sealed class CreateSessionValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        RuleFor(c => c.EmployeeId).NotEmpty();
        RuleFor(c => c.Type).IsInEnum();
        RuleForEach(c => c.Participants).SetValidator(new NewParticipantValidator());
    }
}

internal sealed class CreateSessionHandler(IAppDbContext db, EmployeeReader employees, SessionReader reader, TimeProvider time)
    : IRequestHandler<CreateSessionCommand, SessionDetailsDto>
{
    public async Task<SessionDetailsDto> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var employee = await employees.Visible().SingleOrDefaultAsync(e => e.Id == request.EmployeeId && e.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        if (await db.AssessmentSessions.AnyAsync(s => s.EmployeeId == employee.Id && AssessmentSession.ActiveStatuses.Contains(s.Status), cancellationToken))
        {
            throw new DomainException($"У сотрудника {employee.FullName} уже есть незавершённая сессия оценки.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var plan = await SessionPlanning.PlanAsync(db, employee.CurrentGradeId, request.Type, cancellationToken);
        var rules = await reader.RulesForAsync(employee.CurrentGradeId, cancellationToken);

        var session = new AssessmentSession(employee.Id, employee.TrackId, employee.CurrentGradeId, employee.ManagerUserId, plan, request.DeadlineAtUtc, now);
        session.AddParticipant(employee.FullName, employee.Email, EvaluatorRole.Self, rules, now);
        foreach (var p in request.Participants)
        {
            session.AddParticipant(p.FullName, p.Email, p.Role, rules, now);
        }

        db.AssessmentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return await reader.ToDetailsAsync(session, cancellationToken);
    }
}

internal static class SessionPlanning
{
    public static async Task<SessionPlan> PlanAsync(IAppDbContext db, Guid currentGradeId, SessionType type, CancellationToken cancellationToken)
    {
        var currentOrder = await db.Grades.Where(g => g.Id == currentGradeId).Select(g => g.Order).SingleAsync(cancellationToken);
        var next = await db.Grades.Where(g => g.Order > currentOrder).OrderBy(g => g.Order).FirstOrDefaultAsync(cancellationToken);
        return SessionPlan.For(type, next);
    }
}

// ---------- Черновик ----------

public sealed record UpdateSessionCommand(Guid Id, SessionType Type, DateTime DeadlineAtUtc) : IRequest<SessionDetailsDto>;

internal sealed class UpdateSessionHandler(IAppDbContext db, SessionAccess access, SessionReader reader, TimeProvider time)
    : IRequestHandler<UpdateSessionCommand, SessionDetailsDto>
{
    public async Task<SessionDetailsDto> Handle(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.Id, cancellationToken);
        var plan = await SessionPlanning.PlanAsync(db, session.CurrentGradeId, request.Type, cancellationToken);
        session.Reschedule(plan, request.DeadlineAtUtc, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        return await reader.ToDetailsAsync(session, cancellationToken);
    }
}

public sealed record DeleteSessionCommand(Guid Id) : IRequest;

internal sealed class DeleteSessionHandler(IAppDbContext db, SessionAccess access) : IRequestHandler<DeleteSessionCommand>
{
    public async Task Handle(DeleteSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.Id, cancellationToken);
        session.EnsureCanDelete();
        db.AssessmentSessions.Remove(session);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---------- Респонденты ----------

public sealed record AddParticipantResult(ParticipantDto Participant, ParticipantLinkDto? Link);

public sealed record AddParticipantCommand(Guid SessionId, string FullName, string Email, EvaluatorRole Role) : IRequest<AddParticipantResult>;

internal sealed class AddParticipantValidator : AbstractValidator<AddParticipantCommand>
{
    public AddParticipantValidator() =>
        RuleFor(c => new NewParticipant(c.FullName, c.Email, c.Role)).SetValidator(new NewParticipantValidator()).OverridePropertyName("");
}

internal sealed class AddParticipantHandler(IAppDbContext db, SessionAccess access, SessionReader reader, ISurveyLinks links, TimeProvider time)
    : IRequestHandler<AddParticipantCommand, AddParticipantResult>
{
    public async Task<AddParticipantResult> Handle(AddParticipantCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.SessionId, cancellationToken);
        var rules = await reader.RulesForAsync(session.CurrentGradeId, cancellationToken);
        var (p, token) = session.AddParticipant(request.FullName, request.Email, request.Role, rules, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return new AddParticipantResult(
            new ParticipantDto(p.Id, p.FullName, p.Email, p.Role, p.Status, p.TokenIssuedAtUtc, p.FirstOpenedAtUtc, p.SubmittedAtUtc),
            token is { } t ? new ParticipantLinkDto(p.Id, p.FullName, p.Role, links.Build(t.Value)) : null);
    }
}

public sealed record RemoveParticipantCommand(Guid SessionId, Guid ParticipantId) : IRequest;

internal sealed class RemoveParticipantHandler(IAppDbContext db, SessionAccess access) : IRequestHandler<RemoveParticipantCommand>
{
    public async Task Handle(RemoveParticipantCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.SessionId, cancellationToken);
        session.RemoveParticipant(request.ParticipantId);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---------- Запуск и ссылки ----------

public sealed record LaunchSessionCommand(Guid Id) : IRequest<IReadOnlyList<ParticipantLinkDto>>;

internal sealed class LaunchSessionHandler(
    IAppDbContext db, SessionAccess access, SessionReader reader, IndicatorSnapshotBuilder snapshots, ISurveyLinks links, TimeProvider time)
    : IRequestHandler<LaunchSessionCommand, IReadOnlyList<ParticipantLinkDto>>
{
    public async Task<IReadOnlyList<ParticipantLinkDto>> Handle(LaunchSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.Id, cancellationToken);
        var rules = await reader.RulesForAsync(session.CurrentGradeId, cancellationToken);
        var indicators = await snapshots.BuildAsync(session.TrackId, session.CurrentGradeId, session.TargetGradeId, cancellationToken);

        var tokens = session.Launch(rules, indicators, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return [.. session.Participants
            .OrderBy(p => p.Role).ThenBy(p => p.FullName)
            .Select(p => new ParticipantLinkDto(p.Id, p.FullName, p.Role, links.Build(tokens[p.Id].Value)))];
    }
}

public sealed record ReissueLinkCommand(Guid SessionId, Guid ParticipantId) : IRequest<ParticipantLinkDto>;

internal sealed class ReissueLinkHandler(IAppDbContext db, SessionAccess access, ISurveyLinks links, TimeProvider time)
    : IRequestHandler<ReissueLinkCommand, ParticipantLinkDto>
{
    public async Task<ParticipantLinkDto> Handle(ReissueLinkCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.SessionId, cancellationToken);
        var token = session.ReissueToken(request.ParticipantId, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        var p = session.Participants.Single(x => x.Id == request.ParticipantId);
        return new ParticipantLinkDto(p.Id, p.FullName, p.Role, links.Build(token.Value));
    }
}

public sealed record CancelSessionCommand(Guid Id) : IRequest;

internal sealed class CancelSessionHandler(IAppDbContext db, SessionAccess access, TimeProvider time) : IRequestHandler<CancelSessionCommand>
{
    public async Task Handle(CancelSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.Id, cancellationToken);
        session.Cancel(time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
    }
}
