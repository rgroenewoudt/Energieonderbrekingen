namespace Energieonderbrekingen.Web.Email;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody, string? UnsubscribeUrl = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
