using System.Collections.Concurrent;
using ReviewPlatform.Infrastructure.Email;

namespace ReviewPlatform.Api.Tests;

/// <summary>Письма в памяти. Адреса, начинающиеся с «fail-», имитируют отказ SMTP.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (to.StartsWith("fail-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SMTP недоступен");
        }

        Sent.Enqueue((to, subject, htmlBody));
        return Task.CompletedTask;
    }
}
