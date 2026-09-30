namespace ReviewPlatform.Domain.Assessments;

/// <summary>Когда напоминать респонденту об анкете (ТЗ, 8.8 и 10.2).</summary>
public static class ReminderSchedule
{
    /// <summary>Не чаще одного письма со ссылкой в сутки.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// Напоминание нужно, если наступила точка «за N дней до дедлайна», а последнее письмо со ссылкой
    /// (приглашение, повторная отправка, напоминание — каждое выпускает токен) ушло раньше этой точки и не менее суток назад.
    /// Так повторный тик планировщика ничего не отправляет, а приглашение накануне точки её поглощает.
    /// </summary>
    /// <param name="lastLinkSentAtUtc">Когда респонденту последний раз выпускали ссылку; null — ещё не выпускали.</param>
    public static bool IsDue(DateTime deadlineAtUtc, IReadOnlyCollection<int> daysBeforeDeadline, DateTime? lastLinkSentAtUtc, DateTime nowUtc)
    {
        if (lastLinkSentAtUtc is not { } last || nowUtc >= deadlineAtUtc)
        {
            return false;
        }

        var points = daysBeforeDeadline.Select(d => deadlineAtUtc.AddDays(-d)).Where(p => p <= nowUtc).ToList();
        return points.Count > 0 && last < points.Max() && nowUtc - last >= MinInterval;
    }
}
