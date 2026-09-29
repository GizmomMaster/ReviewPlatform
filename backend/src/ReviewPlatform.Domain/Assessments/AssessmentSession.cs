using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Assessments;

/// <summary>Сессия оценки одного сотрудника (ТЗ, раздел 7 — жизненный цикл).</summary>
public sealed class AssessmentSession : Entity
{
    /// <summary>Статусы, при которых у сотрудника не может быть второй сессии.</summary>
    public static readonly IReadOnlyList<SessionStatus> ActiveStatuses =
        [SessionStatus.Draft, SessionStatus.InProgress, SessionStatus.Overdue, SessionStatus.AwaitingDecision];

    private readonly List<Participant> _participants = [];
    private readonly List<SessionIndicator> _indicators = [];

    private AssessmentSession() { }

    public AssessmentSession(
        Guid employeeId,
        Guid trackId,
        Guid currentGradeId,
        Guid ownerUserId,
        SessionPlan plan,
        DateTime deadlineAtUtc,
        DateTime nowUtc)
    {
        EmployeeId = employeeId;
        TrackId = trackId;
        CurrentGradeId = currentGradeId;
        OwnerUserId = ownerUserId;
        Status = SessionStatus.Draft;
        CreatedAtUtc = nowUtc;
        Reschedule(plan, deadlineAtUtc, nowUtc);
    }

    public Guid EmployeeId { get; private set; }
    public Guid TrackId { get; private set; }
    public SessionType Type { get; private set; }
    public Guid CurrentGradeId { get; private set; }
    public Guid? TargetGradeId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public SessionStatus Status { get; private set; }
    public DateTime DeadlineAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LaunchedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>
    /// Меняется при каждой отправке анкеты: так параллельные отправки последних респондентов конфликтуют
    /// по версии строки сессии, и повтор одной из них увидит все отправки и завершит сессию.
    /// </summary>
    public DateTime? LastSubmissionAtUtc { get; private set; }

    /// <summary>Токен оптимистической блокировки (xmin в PostgreSQL).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<Participant> Participants => _participants;
    public IReadOnlyCollection<SessionIndicator> Indicators => _indicators;

    public bool IsLaunched => LaunchedAtUtc is not null;

    /// <summary>Тип, целевой грейд и дедлайн — только в черновике.</summary>
    public void Reschedule(SessionPlan plan, DateTime deadlineAtUtc, DateTime nowUtc)
    {
        EnsureStatus("изменить параметры", SessionStatus.Draft);
        EnsureFutureDeadline(deadlineAtUtc, nowUtc);
        Type = plan.Type;
        TargetGradeId = plan.TargetGradeId;
        DeadlineAtUtc = deadlineAtUtc;
    }

    /// <returns>Токен, если сессия уже запущена (ссылку нужно передать респонденту), иначе null.</returns>
    public (Participant Participant, AccessToken? Token) AddParticipant(
        string fullName, string email, EvaluatorRole role, IReadOnlyCollection<GradeRoleRule> rules, DateTime nowUtc)
    {
        EnsureStatus("добавить респондента", SessionStatus.Draft, SessionStatus.InProgress, SessionStatus.Overdue);

        var participant = new Participant(Id, fullName, email, role);
        if (_participants.Any(p => p.IsActive && p.Email == participant.Email))
        {
            throw new DomainException($"Респондент с email {participant.Email} уже есть в сессии.");
        }

        var rule = rules.SingleOrDefault(r => r.Role == role)
            ?? throw new DomainException($"Роль {role} не предусмотрена для грейда сотрудника.");
        if (_participants.Count(p => p.IsActive && p.Role == role) >= rule.MaxCount)
        {
            throw new DomainException($"Для роли {role} уже достигнут максимум респондентов ({rule.MaxCount}).");
        }

        _participants.Add(participant);
        return (participant, IsLaunched ? participant.IssueToken(nowUtc) : null);
    }

    public void RemoveParticipant(Guid participantId)
    {
        EnsureStatus("удалить респондента", SessionStatus.Draft, SessionStatus.InProgress, SessionStatus.Overdue);
        var participant = ActiveParticipant(participantId);

        if (participant.Role == EvaluatorRole.Self)
        {
            throw new DomainException("Самооценку нельзя удалить из сессии.");
        }

        if (participant.Status == ParticipantStatus.Submitted)
        {
            throw new DomainException("Нельзя удалить респондента, который уже отправил анкету.");
        }

        if (IsLaunched)
        {
            participant.Remove();
        }
        else
        {
            _participants.Remove(participant);
        }
    }

    /// <summary>Запуск: проверка состава, снимок индикаторов, выпуск токенов.</summary>
    /// <returns>Токены по Id респондента — показываются один раз.</returns>
    public IReadOnlyDictionary<Guid, AccessToken> Launch(
        IReadOnlyCollection<GradeRoleRule> rules, IReadOnlyCollection<IndicatorSnapshot> indicators, DateTime nowUtc)
    {
        EnsureStatus("запустить", SessionStatus.Draft);
        EnsureFutureDeadline(DeadlineAtUtc, nowUtc);

        var unmet = RoleRequirements.Evaluate(rules, _participants).Where(r => !r.IsSatisfied).ToList();
        if (unmet.Count > 0)
        {
            var details = string.Join("; ", unmet.Select(r => $"{r.Role}: {r.Count} (нужно {r.MinCount}–{r.MaxCount})"));
            throw new DomainException($"Состав респондентов не соответствует правилам грейда: {details}.");
        }

        if (indicators.Count == 0)
        {
            throw new DomainException("В матрице нет индикаторов для выбранных грейдов.");
        }

        _indicators.AddRange(indicators.Select(i => new SessionIndicator(Id, i)));
        Status = SessionStatus.InProgress;
        LaunchedAtUtc = nowUtc;

        return _participants.ToDictionary(p => p.Id, p => p.IssueToken(nowUtc));
    }

    /// <summary>Новый токен респонденту; старая ссылка перестаёт работать.</summary>
    public AccessToken ReissueToken(Guid participantId, DateTime nowUtc)
    {
        EnsureStatus("перевыпустить ссылку", SessionStatus.InProgress, SessionStatus.Overdue);
        var participant = ActiveParticipant(participantId);
        if (participant.Status == ParticipantStatus.Submitted)
        {
            throw new DomainException("Респондент уже отправил анкету.");
        }

        return participant.IssueToken(nowUtc);
    }

    public void Cancel(DateTime nowUtc)
    {
        EnsureStatus("отменить", SessionStatus.Draft, SessionStatus.InProgress, SessionStatus.Overdue);
        Status = SessionStatus.Cancelled;
        ClosedAtUtc = nowUtc;
    }

    public void EnsureCanDelete() => EnsureStatus("удалить", SessionStatus.Draft);

    /// <summary>Анкета принимает ответы: опрос идёт и дедлайн не наступил.</summary>
    public bool AcceptsAnswers(DateTime nowUtc) => Status == SessionStatus.InProgress && nowUtc <= DeadlineAtUtc;

    public void OpenSurvey(Participant participant, DateTime nowUtc)
    {
        if (AcceptsAnswers(nowUtc) && participant.Status is ParticipantStatus.Pending)
        {
            participant.MarkOpened(nowUtc);
        }
    }

    /// <summary>Автосохранение: частичный набор ответов.</summary>
    public void SaveDraft(Participant participant, IReadOnlyCollection<AnswerInput> answers, DateTime nowUtc)
    {
        EnsureAcceptsAnswers(participant, nowUtc);
        EnsureKnownIndicators(answers);
        participant.SaveAnswers(answers, nowUtc);
    }

    /// <summary>Финальная отправка: все индикаторы отвечены, комментарии к 0 и 3 заполнены.</summary>
    /// <returns>Ошибки по индикаторам; пустой список — анкета принята.</returns>
    public IReadOnlyList<SurveyAnswerError> Submit(Participant participant, IReadOnlyCollection<AnswerInput> answers, DateTime nowUtc)
    {
        EnsureAcceptsAnswers(participant, nowUtc);
        EnsureKnownIndicators(answers);
        participant.SaveAnswers(answers, nowUtc);

        var byIndicator = participant.Answers.ToDictionary(a => a.SessionIndicatorId);
        var errors = new List<SurveyAnswerError>();
        foreach (var indicator in _indicators)
        {
            if (!byIndicator.TryGetValue(indicator.Id, out var answer) || !answer.IsAnswered)
            {
                errors.Add(new SurveyAnswerError(indicator.Id, "Выберите оценку или «Не могу оценить»."));
            }
            else if (answer.RequiresComment && answer.Comment is null)
            {
                errors.Add(new SurveyAnswerError(indicator.Id, "Для оценок 0 и 3 приведите пример в комментарии."));
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        participant.MarkSubmitted(nowUtc);
        LastSubmissionAtUtc = nowUtc;
        if (_participants.Where(p => p.IsActive).All(p => p.Status == ParticipantStatus.Submitted))
        {
            Status = SessionStatus.AwaitingDecision;
            CompletedAtUtc = nowUtc;
        }

        return [];
    }

    private void EnsureAcceptsAnswers(Participant participant, DateTime nowUtc)
    {
        if (!participant.IsActive || participant.Status == ParticipantStatus.Submitted)
        {
            throw new DomainException("Анкета уже отправлена или ссылка больше не действует.");
        }

        if (!AcceptsAnswers(nowUtc))
        {
            throw new DomainException("Опрос завершён: ответы больше не принимаются.");
        }
    }

    private void EnsureKnownIndicators(IEnumerable<AnswerInput> answers)
    {
        var known = _indicators.Select(i => i.Id).ToHashSet();
        if (answers.Any(a => !known.Contains(a.IndicatorId)))
        {
            throw new DomainException("Ответ относится к индикатору не из этой анкеты.");
        }
    }

    private Participant ActiveParticipant(Guid participantId) =>
        _participants.SingleOrDefault(p => p.Id == participantId && p.IsActive)
            ?? throw new DomainException("Респондент не найден в сессии.");

    private void EnsureStatus(string action, params SessionStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException($"Нельзя {action}: сессия в статусе {Status}.");
        }
    }

    private static void EnsureFutureDeadline(DateTime deadlineAtUtc, DateTime nowUtc)
    {
        if (deadlineAtUtc <= nowUtc)
        {
            throw new DomainException("Дедлайн должен быть в будущем.");
        }
    }
}

public sealed record SurveyAnswerError(Guid IndicatorId, string Message);

/// <summary>Тип сессии с целевым грейдом. Создаётся через <see cref="For"/>, который проверяет допустимость.</summary>
public sealed record SessionPlan(SessionType Type, Guid? TargetGradeId)
{
    /// <param name="nextGrade">Следующий грейд после текущего; null — текущий последний (E8).</param>
    public static SessionPlan For(SessionType type, Grade? nextGrade) => type switch
    {
        SessionType.Confirmation => new SessionPlan(type, null),
        SessionType.Transition when nextGrade is not null => new SessionPlan(type, nextGrade.Id),
        _ => throw new DomainException("Для последнего грейда доступно только подтверждение уровня."),
    };
}
