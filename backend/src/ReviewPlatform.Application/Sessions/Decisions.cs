using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Audit;
using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Application.Sessions;

public sealed record PlanItemDto(string Text, Guid? SessionIndicatorId, DateOnly? DueDate);

public sealed record DecisionDto(
    DecisionOutcome Outcome,
    GradeDto NewGrade,
    string Comment,
    DateTime DecidedAtUtc,
    string DecidedByName,
    IReadOnlyList<PlanItemDto> PlanItems);

// ---------- Досрочное завершение ----------

public sealed record CloseSessionEarlyCommand(Guid Id) : IRequest;

internal sealed class CloseSessionEarlyHandler(IAppDbContext db, SessionAccess access, IAuditLog audit, TimeProvider time)
    : IRequestHandler<CloseSessionEarlyCommand>
{
    public async Task Handle(CloseSessionEarlyCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.Id, cancellationToken);
        session.CloseEarly(time.GetUtcNow().UtcDateTime);
        var pending = session.Participants.Count(p => p.IsActive && p.Status != ParticipantStatus.Submitted);
        audit.Record(AuditActions.SessionClosedEarly, nameof(AssessmentSession), session.Id, $"Не отправили анкету: {pending}");
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---------- Решение ----------

public sealed record DecideSessionCommand(Guid SessionId, DecisionOutcome Outcome, Guid NewGradeId, string Comment, IReadOnlyList<PlanItemDto> PlanItems)
    : IRequest<DecisionDto>;

internal sealed class DecideSessionValidator : AbstractValidator<DecideSessionCommand>
{
    public DecideSessionValidator()
    {
        RuleFor(c => c.Outcome).IsInEnum();
        RuleFor(c => c.NewGradeId).NotEmpty();
        RuleFor(c => c.Comment).NotEmpty().WithMessage("Опишите обоснование решения.").MaximumLength(AssessmentDecision.MaxCommentLength);
        RuleFor(c => c.PlanItems).NotNull().Must(p => p.Count <= AssessmentDecision.MaxPlanItems)
            .WithMessage($"В плане развития не больше {AssessmentDecision.MaxPlanItems} пунктов.");
        RuleForEach(c => c.PlanItems).ChildRules(item =>
            item.RuleFor(i => i.Text).NotEmpty().WithMessage("Пункт плана не может быть пустым.").MaximumLength(DevelopmentPlanItem.MaxTextLength));
    }
}

internal sealed class DecideSessionHandler(IAppDbContext db, SessionAccess access, SessionReader reader, ICurrentUser currentUser, IAuditLog audit, TimeProvider time)
    : IRequestHandler<DecideSessionCommand, DecisionDto>
{
    public async Task<DecisionDto> Handle(DecideSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.SessionId, cancellationToken);
        await db.SessionIndicators.Where(i => i.SessionId == session.Id).LoadAsync(cancellationToken);

        var grades = await db.Grades.ToDictionaryAsync(g => g.Id, cancellationToken);
        if (!grades.TryGetValue(request.NewGradeId, out var newGrade))
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.NewGradeId), "Грейд не найден.")]);
        }

        var currentOrder = grades[session.CurrentGradeId].Order;
        var consistent = request.Outcome switch
        {
            DecisionOutcome.Promoted => newGrade.Order > currentOrder,
            DecisionOutcome.GradeConfirmed => newGrade.Order == currentOrder,
            _ => newGrade.Order <= currentOrder,
        };
        if (!consistent)
        {
            throw new DomainException(request.Outcome switch
            {
                DecisionOutcome.Promoted => "При повышении новый грейд должен быть выше текущего.",
                DecisionOutcome.GradeConfirmed => "При подтверждении грейд не меняется.",
                _ => "Если грейд не подтверждён, новый грейд не может быть выше текущего.",
            });
        }

        var decision = session.Decide(
            currentUser.UserId,
            request.Outcome,
            newGrade.Id,
            request.Comment,
            [.. request.PlanItems.Select(p => new PlanItemInput(p.Text, p.SessionIndicatorId, p.DueDate))],
            time.GetUtcNow().UtcDateTime);

        var employee = await db.Employees.SingleAsync(e => e.Id == session.EmployeeId, cancellationToken);
        var oldGradeCode = grades[employee.CurrentGradeId].Code;
        if (employee.CurrentGradeId != newGrade.Id)
        {
            employee.ChangeGrade(newGrade.Id);
        }

        audit.Record(AuditActions.SessionDecided, nameof(AssessmentSession), session.Id,
            AuditTexts.Decision(request.Outcome, oldGradeCode, newGrade.Code, decision.PlanItems.Count));
        await db.SaveChangesAsync(cancellationToken);

        return (await reader.DecisionAsync(session, cancellationToken))!;
    }
}

// ---------- История действий ----------

public sealed record AuditEntryDto(DateTime OccurredAtUtc, string? UserName, string Action, string? Details);

public sealed record GetSessionAuditQuery(Guid SessionId) : IRequest<IReadOnlyList<AuditEntryDto>>;

internal sealed class GetSessionAuditHandler(IAppDbContext db, SessionAccess access, IIdentityService identity)
    : IRequestHandler<GetSessionAuditQuery, IReadOnlyList<AuditEntryDto>>
{
    public async Task<IReadOnlyList<AuditEntryDto>> Handle(GetSessionAuditQuery request, CancellationToken cancellationToken)
    {
        var session = await access.LoadAsync(request.SessionId, cancellationToken);
        var entries = await db.AuditEntries
            .Where(e => e.EntityType == nameof(AssessmentSession) && e.EntityId == session.Id)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);
        var users = await identity.GetUserNamesAsync([.. entries.Where(e => e.UserId is not null).Select(e => e.UserId!.Value).Distinct()], cancellationToken);

        return [.. entries.Select(e => new AuditEntryDto(e.OccurredAtUtc, e.UserId is { } u ? users.GetValueOrDefault(u) : null, e.Action, e.Details))];
    }
}
