namespace Energieonderbrekingen.Web.Services;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Publieke URL van de site, gebruikt voor afmeldlinks in e-mails.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5047";

    public string UnsubscribeUrl(string token) =>
        $"{PublicBaseUrl.TrimEnd('/')}/afmelden?token={Uri.EscapeDataString(token)}";
}
