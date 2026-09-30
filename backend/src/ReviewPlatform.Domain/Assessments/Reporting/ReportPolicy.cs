namespace ReviewPlatform.Domain.Assessments.Reporting;

/// <summary>Параметры расчёта отчёта (ТЗ, 5.3). Значения по умолчанию — из ТЗ; переопределяются секцией AssessmentPolicy.</summary>
public sealed class ReportPolicy
{
    public const string SectionName = "AssessmentPolicy";

    /// <summary>Итог индикатора не ниже порога — индикатор «выполнен».</summary>
    public double MetThreshold { get; set; } = 2.0;

    /// <summary>Минимум оценок окружения (без самооценки и N/A), чтобы индикатор участвовал в расчётах.</summary>
    public int Quorum { get; set; } = 2;

    public double CurrentConfirmationHint { get; set; } = 0.90;
    public double TargetReadinessHint { get; set; } = 0.70;
    public double GroupReadinessHint { get; set; } = 0.50;

    /// <summary>Разброс оценок окружения, при котором индикатор спорный.</summary>
    public int DisagreementSpread { get; set; } = 2;

    /// <summary>Разница «самооценка − окружение», при которой это слепое пятно.</summary>
    public double BlindSpotGap { get; set; } = 1.5;
}
