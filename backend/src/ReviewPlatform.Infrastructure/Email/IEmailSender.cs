using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ReviewPlatform.Infrastructure.Email;

internal interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken);
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> smtp, IOptions<EmailOptions> email) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.Value.FromName, email.Value.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        var options = smtp.Value;
        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, options.Security, cancellationToken);
        if (!string.IsNullOrEmpty(options.UserName))
        {
            await client.AuthenticateAsync(options.UserName, options.Password ?? "", cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
