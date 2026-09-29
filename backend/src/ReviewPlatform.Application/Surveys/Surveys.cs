using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Notifications;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Application.Surveys;

public enum SurveyState
{
    /// <summary>Можно заполнять.</summary>
    Open,

    /// <summary>Респондент уже отправил анкету.</summary>
    Submitted,

    /// <summary>Сессия завершена, отменена или дедлайн прошёл.</summary>
    Closed,
}

public sealed record SurveyIndicatorDto(Guid Id, string Text);

public sealed record SurveyGroupDto(string Name, IReadOnlyList<SurveyIndicatorDto> Indicators);

public sealed record SurveyAnswerDto(Guid IndicatorId, int? Score, bool NotApplicable, string? Comment);

/// <summary>Анкета для респондента. Грейд индикаторов и других респондентов не раскрываем.</summary>
public sealed record SurveyDto(
    SurveyState State,
    string EmployeeName,
    string RespondentName,
    EvaluatorRole Role,
    DateTime DeadlineAtUtc,
    IReadOnlyList<SurveyGroupDto> Groups,
    IReadOnlyList<SurveyAnswerDto> Answers);

/// <summary>Сессия и респондент по токену из ссылки.</summary>
internal sealed class SurveyLoader(IAppDbContext db)
{
    public async Task<(AssessmentSession Session, Participant Participant)> LoadAsync(string token, CancellationToken cancellationToken)
    {
        var hash = AccessToken.HashOf(token);
        var session = await db.AssessmentSessions
            .Include(s => s.Participants)
            .Include(s => s.Indicators)
            .SingleOrDefaultAsync(s => s.Participants.Any(p => p.TokenHash == hash), cancellationToken)
            ?? throw new NotFoundException("Survey", Guid.Empty);

        var participant = session.Participants.Single(p => p.TokenHash == hash);
        await db.SurveyAnswers.Where(a => a.ParticipantId == participant.Id).LoadAsync(cancellationToken);
        return (session, participant);
    }
}

internal static class AnswerMapping
{
    public static AnswerInput[] ToInputs(IReadOnlyList<SurveyAnswerDto> answers) =>
        [.. answers.Select(a => new AnswerInput(a.IndicatorId, a.Score, a.NotApplicable, a.Comment))];
}

internal sealed class AnswersValidator : AbstractValidator<IReadOnlyList<SurveyAnswerDto>>
{
    public AnswersValidator()
    {
        RuleFor(a => a).NotNull().Must(a => a.Count <= 500).WithMessage("Слишком много ответов в одном запросе.");
        RuleFor(a => a).Must(a => a.Select(x => x.IndicatorId).Distinct().Count() == a.Count).WithMessage("Индикаторы в ответах повторяются.");
    }
}

// ---------- Получение ----------

public sealed record GetSurveyQuery(string Token) : IRequest<SurveyDto>;

internal sealed class GetSurveyHandler(IAppDbContext db, SurveyLoader loader, TimeProvider time) : IRequestHandler<GetSurveyQuery, SurveyDto>
{
    public async Task<SurveyDto> Handle(GetSurveyQuery request, CancellationToken cancellationToken)
    {
        var (session, participant) = await loader.LoadAsync(request.Token, cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        var employeeName = await db.Employees.Where(e => e.Id == session.EmployeeId).Select(e => e.FullName).SingleAsync(cancellationToken);

        var state = participant.Status == ParticipantStatus.Submitted ? SurveyState.Submitted
            : session.AcceptsAnswers(now) ? SurveyState.Open
            : SurveyState.Closed;

        if (state != SurveyState.Open)
        {
            return new SurveyDto(state, employeeName, participant.FullName, participant.Role, session.DeadlineAtUtc, [], []);
        }

        session.OpenSurvey(participant, now);
        await db.SaveChangesAsync(cancellationToken);

        var groups = session.Indicators
            .OrderBy(i => i.GroupOrder).ThenBy(i => i.LevelKind).ThenBy(i => i.Order)
            .GroupBy(i => i.GroupName)
            .Select(g => new SurveyGroupDto(g.Key, [.. g.Select(i => new SurveyIndicatorDto(i.Id, i.Text))]))
            .ToList();
        var answers = participant.Answers
            .Select(a => new SurveyAnswerDto(a.SessionIndicatorId, a.Score, a.NotApplicable, a.Comment))
            .ToList();

        return new SurveyDto(state, employeeName, participant.FullName, participant.Role, session.DeadlineAtUtc, groups, answers);
    }
}

// ---------- Черновик ----------

public sealed record SaveSurveyDraftCommand(string Token, IReadOnlyList<SurveyAnswerDto> Answers) : IRequest;

internal sealed class SaveSurveyDraftValidator : AbstractValidator<SaveSurveyDraftCommand>
{
    public SaveSurveyDraftValidator() => RuleFor(c => c.Answers).SetValidator(new AnswersValidator());
}

internal sealed class SaveSurveyDraftHandler(IAppDbContext db, SurveyLoader loader, TimeProvider time) : IRequestHandler<SaveSurveyDraftCommand>
{
    public async Task Handle(SaveSurveyDraftCommand request, CancellationToken cancellationToken)
    {
        var (session, participant) = await loader.LoadAsync(request.Token, cancellationToken);
        session.SaveDraft(participant, AnswerMapping.ToInputs(request.Answers), time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---------- Отправка ----------

public sealed record SubmitSurveyCommand(string Token, IReadOnlyList<SurveyAnswerDto> Answers) : IRequest;

internal sealed class SubmitSurveyValidator : AbstractValidator<SubmitSurveyCommand>
{
    public SubmitSurveyValidator() => RuleFor(c => c.Answers).SetValidator(new AnswersValidator());
}

internal sealed class SubmitSurveyHandler(IAppDbContext db, SurveyLoader loader, SessionNotifier notifier, TimeProvider time) : IRequestHandler<SubmitSurveyCommand>
{
    private const int MaxAttempts = 5;

    public async Task Handle(SubmitSurveyCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (session, participant) = await loader.LoadAsync(request.Token, cancellationToken);
            var errors = session.Submit(participant, AnswerMapping.ToInputs(request.Answers), time.GetUtcNow().UtcDateTime);
            if (errors.Count > 0)
            {
                // Ответы не сохраняем: отправка не состоялась, черновик сохраняется отдельным запросом
                db.ChangeTracker.Clear();
                throw new ValidationException(errors.Select(e => new ValidationFailure(e.IndicatorId.ToString(), e.Message)));
            }

            if (session.Status == SessionStatus.AwaitingDecision)
            {
                // Последняя анкета: письмо владельцу сохраняется в той же транзакции, что и смена статуса
                await notifier.SurveyCompletedAsync(session, cancellationToken);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Параллельно отправил другой респондент — перечитываем сессию и повторяем со случайной паузой,
                // чтобы одновременные повторы снова не столкнулись
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(10, 50) * attempt), time, cancellationToken);
            }
        }
    }
}
