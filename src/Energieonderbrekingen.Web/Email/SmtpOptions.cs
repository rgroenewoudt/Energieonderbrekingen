namespace Energieonderbrekingen.Web.Email;

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "smtp.sendgrid.net";
    public int Port { get; set; } = 587;

    /// <summary>Bij SendGrid SMTP is de gebruikersnaam altijd letterlijk "apikey".</summary>
    public string Username { get; set; } = "apikey";

    /// <summary>De SendGrid API key.</summary>
    public string Password { get; set; } = "";

    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Storingsalert";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(FromEmail);
}
