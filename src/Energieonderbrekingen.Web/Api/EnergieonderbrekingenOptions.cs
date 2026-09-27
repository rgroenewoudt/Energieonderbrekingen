namespace Energieonderbrekingen.Web.Api;

public sealed class EnergieonderbrekingenOptions
{
    public const string Section = "Energieonderbrekingen";

    /// <summary>Gebruik voorbeelddata i.p.v. de echte API (handig zolang er nog geen credentials zijn).</summary>
    public bool UseMock { get; set; } = true;

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Audience { get; set; } = "https://energieonderbrekingen.nl/api/v1";
    public string BaseUrl { get; set; } = "https://energieonderbrekingen.nl/api/v1/";
    public string TokenUrl { get; set; } = "https://energieonderbrekingen.eu.auth0.com/oauth/token";

    public int PollIntervalMinutes { get; set; } = 15;

    /// <summary>Hoe ver terug (op basis van lastUpdated) onderbrekingen worden opgehaald.</summary>
    public int LookbackDays { get; set; } = 2;

    public string MockDataPath { get; set; } = "SampleData/disruptions.json";

    public bool HasCredentials => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
