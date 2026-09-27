using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Energieonderbrekingen.Web.Email;

/// <summary>Verstuurt e-mail via SMTP (standaard SendGrid: smtp.sendgrid.net:587, STARTTLS, gebruiker "apikey").</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        if (message.UnsubscribeUrl is not null)
        {
            mime.Headers.Add("List-Unsubscribe", $"<{message.UnsubscribeUrl}>");
            mime.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("E-mail '{Subject}' verstuurd naar {To}", message.Subject, message.To);
    }
}

/// <summary>Voor lokaal ontwikkelen zonder SendGrid: schrijft de e-mail naar de log.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "SMTP niet geconfigureerd - e-mail niet verstuurd.\nAan: {To}\nOnderwerp: {Subject}\n\n{Body}",
            message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}
