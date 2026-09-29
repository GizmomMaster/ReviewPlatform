using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Domain.Assessments;

/// <summary>Ответ респондента по индикатору. Черновик и финальный ответ — одна запись; финальность — статус респондента.</summary>
public sealed class SurveyAnswer : Entity
{
    public const int MinScore = 0;
    public const int MaxScore = 3;
    public const int MaxCommentLength = 4000;

    private SurveyAnswer() { }

    internal SurveyAnswer(Guid participantId, Guid sessionIndicatorId)
    {
        ParticipantId = participantId;
        SessionIndicatorId = sessionIndicatorId;
    }

    public Guid ParticipantId { get; private set; }
    public Guid SessionIndicatorId { get; private set; }

    /// <summary>0–3; null — не выбрано или «не могу оценить».</summary>
    public int? Score { get; private set; }

    /// <summary>«Не могу оценить» — в расчётах не участвует.</summary>
    public bool NotApplicable { get; private set; }

    public string? Comment { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsAnswered => Score is not null || NotApplicable;

    /// <summary>Для оценок 0 и 3 нужен пример в комментарии.</summary>
    public bool RequiresComment => Score is MinScore or MaxScore;

    internal void Set(AnswerInput input, DateTime nowUtc)
    {
        if (input.Score is { } score && (score < MinScore || score > MaxScore))
        {
            throw new DomainException($"Оценка должна быть от {MinScore} до {MaxScore}.");
        }

        if (input.Score is not null && input.NotApplicable)
        {
            throw new DomainException("Нельзя одновременно поставить оценку и «не могу оценить».");
        }

        var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        if (comment?.Length > MaxCommentLength)
        {
            throw new DomainException($"Комментарий длиннее {MaxCommentLength} символов.");
        }

        Score = input.Score;
        NotApplicable = input.NotApplicable;
        Comment = comment;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed record AnswerInput(Guid IndicatorId, int? Score, bool NotApplicable, string? Comment);
