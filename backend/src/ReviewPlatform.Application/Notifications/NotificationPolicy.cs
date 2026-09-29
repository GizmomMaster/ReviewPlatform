using ReviewPlatform.Domain.Assessments.Reporting;

namespace ReviewPlatform.Application.Notifications;

/// <summary>Сроки напоминаний (ТЗ, 5.3). Секция AssessmentPolicy — общая с параметрами отчёта.</summary>
public sealed class NotificationPolicy
{
    public const string SectionName = ReportPolicy.SectionName;

    private static readonly int[] DefaultReminderDays = [3, 1];

    /// <summary>
    /// За сколько дней до дедлайна напоминать. Пустой массив — значения по умолчанию: конфигурация
    /// дописывает элементы к непустому массиву, а не заменяет его.
    /// </summary>
    public int[] ReminderDaysBeforeDeadline { get; set; } = [];

    public IReadOnlyList<int> ReminderDays => ReminderDaysBeforeDeadline.Length > 0 ? ReminderDaysBeforeDeadline : DefaultReminderDays;
}
