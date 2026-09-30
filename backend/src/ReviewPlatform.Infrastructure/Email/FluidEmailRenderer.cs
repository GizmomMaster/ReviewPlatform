using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewPlatform.Application.Notifications;
using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Infrastructure.Email;

/// <summary>
/// Письма из Liquid-шаблонов (Fluid), встроенных в сборку: Email/Templates/{name}.liquid, общая обёртка — _layout.liquid.
/// В шаблонах поля модели — в snake_case, вывод экранируется как HTML, даты — фильтр <c>datetime</c> в часовом поясе из настроек.
/// </summary>
internal sealed partial class FluidEmailRenderer : IEmailRenderer
{
    private const string DateFormat = "dd.MM.yyyy HH:mm";

    private static readonly string[] TemplateNames = ["_layout", "survey-link", "survey-completed", "deadline-passed", "session-cancelled"];

    /// <summary>Экранирует HTML, но не кириллицу: HtmlEncoder.Default превратил бы ФИО в числовые сущности.</summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly TimeZoneInfo _timeZone;
    private readonly TemplateOptions _options = new();
    private readonly Dictionary<string, IFluidTemplate> _templates;

    public FluidEmailRenderer(IOptions<EmailOptions> options, ILogger<FluidEmailRenderer> logger)
    {
        _timeZone = ResolveTimeZone(options.Value.TimeZone, logger);

        _options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.SnakeCase;
        _options.MemberAccessStrategy.Register<SurveyLinkEmail>();
        _options.MemberAccessStrategy.Register<SurveyCompletedEmail>();
        _options.MemberAccessStrategy.Register<DeadlinePassedEmail>();
        _options.MemberAccessStrategy.Register<SessionCancelledEmail>();
        _options.ValueConverters.Add(value => value is Enum e ? e.ToString() : null);
        _options.Filters.AddFilter("datetime", (input, _, _) => new StringValue(FormatValue(input)));

        var parser = new FluidParser();
        _templates = TemplateNames.ToDictionary(name => name, name => Parse(parser, name));
    }

    public RenderedEmail Render(EmailMessage message)
    {
        var (template, subject) = message switch
        {
            SurveyLinkEmail m => ("survey-link", LinkSubject(m)),
            SurveyCompletedEmail m => ("survey-completed", $"Все участники завершили опрос по сотруднику {m.EmployeeName}"),
            DeadlinePassedEmail m => ("deadline-passed", $"Дедлайн опроса по сотруднику {m.EmployeeName} прошёл"),
            SessionCancelledEmail m => ("session-cancelled", $"Оценка сотрудника {m.EmployeeName} отменена"),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.GetType().Name, "Нет шаблона письма."),
        };

        var content = _templates[template].Render(new TemplateContext(message, _options), Encoder);
        var layout = new TemplateContext(_options);
        layout.SetValue("content", new StringValue(content, encode: false));
        layout.SetValue("subject", subject);
        return new RenderedEmail($"[360 Review] {subject}", _templates["_layout"].Render(layout, Encoder));
    }

    private string LinkSubject(SurveyLinkEmail m)
    {
        var survey = m.IsSelf ? "самооценка" : $"опрос по сотруднику {m.EmployeeName}";
        return m.Kind switch
        {
            EmailType.Invitation => m.IsSelf ? "Приглашение пройти самооценку" : $"Приглашение оценить сотрудника {m.EmployeeName}",
            EmailType.LinkReissued => $"Новая ссылка: {survey}",
            EmailType.Reminder => $"Напоминание: {survey}, ответьте до {Format(m.DeadlineAtUtc)}",
            EmailType.DeadlineExtended => $"Срок продлён до {Format(m.DeadlineAtUtc)}: {survey}",
            _ => throw new ArgumentOutOfRangeException(nameof(m), m.Kind, "Тип не относится к письмам со ссылкой."),
        };
    }

    private string Format(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone).ToString(DateFormat, CultureInfo.InvariantCulture)
        + $" ({FormatOffset(_timeZone.GetUtcOffset(utc))})";

    private string FormatValue(FluidValue input) => input.ToObjectValue() switch
    {
        DateTime dt => Format(dt),
        DateTimeOffset dto => Format(dto.UtcDateTime),
        var other => other?.ToString() ?? "",
    };

    private static string FormatOffset(TimeSpan offset) =>
        offset == TimeSpan.Zero ? "UTC" : $"UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset:h\\:mm}".Replace(":00", "", StringComparison.Ordinal);

    private static IFluidTemplate Parse(FluidParser parser, string name)
    {
        var resource = $"{typeof(FluidEmailRenderer).Namespace}.Templates.{name}.liquid";
        using var stream = typeof(FluidEmailRenderer).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Email template {resource} is not embedded.");
        using var reader = new StreamReader(stream);
        return parser.TryParse(reader.ReadToEnd(), out var template, out var error)
            ? template
            : throw new InvalidOperationException($"Email template {name}: {error}");
    }

    private static TimeZoneInfo ResolveTimeZone(string id, ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            LogTimeZoneNotFound(logger, id);
            return TimeZoneInfo.Utc;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Time zone {TimeZone} not found, email dates will be in UTC.")]
    private static partial void LogTimeZoneNotFound(ILogger logger, string timeZone);
}
