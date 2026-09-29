using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
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
