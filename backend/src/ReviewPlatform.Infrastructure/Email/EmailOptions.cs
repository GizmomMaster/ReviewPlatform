using MailKit.Security;

namespace ReviewPlatform.Infrastructure.Email;

/// <summary>Отправитель и оформление писем.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@review.local";
    public string FromName { get; set; } = "360 Review";

    /// <summary>Часовой пояс дат в письмах (IANA). Даты хранятся в UTC, а читают их люди.</summary>
    public string TimeZone { get; set; } = "Europe/Moscow";
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTlsWhenAvailable;
}

/// <summary>Фоновый отправщик outbox.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>false — письма копятся в outbox, но не отправляются (тесты).</summary>
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 20;

    /// <summary>После стольких неудачных попыток письмо помечается Failed (паузы 1, 2, 4… 60 минут — около 2 часов на 8 попыток).</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>
    /// На сколько письмо резервируется за отправщиком. Если процесс упал посреди отправки,
    /// по истечении резерва письмо снова станет доступно (доставка «хотя бы один раз»).
    /// </summary>
    public int LeaseSeconds { get; set; } = 300;
}
