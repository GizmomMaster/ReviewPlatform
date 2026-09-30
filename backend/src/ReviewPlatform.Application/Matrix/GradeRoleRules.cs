using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Matrix;

public sealed record GradeRoleRuleDto(Guid GradeId, string GradeCode, EvaluatorRole Role, int MinCount, int MaxCount);

public sealed record GetGradeRoleRulesQuery : IRequest<IReadOnlyList<GradeRoleRuleDto>>;

internal sealed class GetGradeRoleRulesHandler(IAppDbContext db) : IRequestHandler<GetGradeRoleRulesQuery, IReadOnlyList<GradeRoleRuleDto>>
{
    public async Task<IReadOnlyList<GradeRoleRuleDto>> Handle(GetGradeRoleRulesQuery request, CancellationToken cancellationToken)
    {
        var rows = await (from r in db.GradeRoleRules
                          join g in db.Grades on r.GradeId equals g.Id
                          select new { g.Order, Dto = new GradeRoleRuleDto(g.Id, g.Code, r.Role, r.MinCount, r.MaxCount) })
            .ToListAsync(cancellationToken);

        // Роль хранится строкой — сортируем по порядку enum в памяти, а не по алфавиту в БД
        return [.. rows.OrderBy(r => r.Order).ThenBy(r => r.Dto.Role).Select(r => r.Dto)];
    }
}

public sealed record GradeRoleRuleInput(Guid GradeId, EvaluatorRole Role, int MinCount, int MaxCount);

/// <summary>
/// Полная замена правил ролей (ТЗ, 5.1, п. 6): роль, которой нет в списке, для грейда недопустима.
/// Действует на черновики и на добавление респондентов; уже запущенные сессии не пересматриваются.
/// </summary>
public sealed record UpdateGradeRoleRulesCommand(IReadOnlyList<GradeRoleRuleInput> Rules) : IRequest<IReadOnlyList<GradeRoleRuleDto>>;

internal sealed class UpdateGradeRoleRulesValidator : AbstractValidator<UpdateGradeRoleRulesCommand>
{
    public UpdateGradeRoleRulesValidator()
    {
        RuleFor(c => c.Rules).NotNull();
        RuleForEach(c => c.Rules).ChildRules(rule => rule.RuleFor(r => r.Role).IsInEnum());
        RuleFor(c => c.Rules)
            .Must(rules => rules.GroupBy(r => (r.GradeId, r.Role)).All(g => g.Count() == 1))
            .WithMessage("Роль указана для грейда несколько раз.");
    }
}

internal sealed class UpdateGradeRoleRulesHandler(IAppDbContext db, ISender sender) : IRequestHandler<UpdateGradeRoleRulesCommand, IReadOnlyList<GradeRoleRuleDto>>
{
    public async Task<IReadOnlyList<GradeRoleRuleDto>> Handle(UpdateGradeRoleRulesCommand request, CancellationToken cancellationToken)
    {
        var grades = await db.Grades.ToDictionaryAsync(g => g.Id, cancellationToken);
        if (request.Rules.FirstOrDefault(r => !grades.ContainsKey(r.GradeId)) is { } unknown)
        {
            throw new NotFoundException(nameof(Grade), unknown.GradeId);
        }

        foreach (var grade in grades.Values.Where(g => !request.Rules.Any(r => r.GradeId == g.Id && r.Role == EvaluatorRole.Self)))
        {
            throw new DomainException($"Для грейда {grade.Code} не задана самооценка — она обязательна.");
        }

        var existing = await db.GradeRoleRules.ToDictionaryAsync(r => (r.GradeId, r.Role), cancellationToken);
        foreach (var input in request.Rules)
        {
            try
            {
                if (existing.Remove((input.GradeId, input.Role), out var rule))
                {
                    rule.SetLimits(input.MinCount, input.MaxCount);
                }
                else
                {
                    db.GradeRoleRules.Add(new GradeRoleRule(input.GradeId, input.Role, input.MinCount, input.MaxCount));
                }
            }
            catch (DomainException ex)
            {
                throw new DomainException($"{grades[input.GradeId].Code}, {AuditTexts.Role(input.Role)}: {ex.Message}");
            }
        }

        db.GradeRoleRules.RemoveRange(existing.Values);
        await db.SaveChangesAsync(cancellationToken);
        return await sender.Send(new GetGradeRoleRulesQuery(), cancellationToken);
    }
}
