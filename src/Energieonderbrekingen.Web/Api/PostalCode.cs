using System.Text.RegularExpressions;

namespace Energieonderbrekingen.Web.Api;

public static partial class PostalCode
{
    [GeneratedRegex(@"^[1-9]\d{3}([A-Z]{2})?$")]
    private static partial Regex ValidPattern();

    /// <summary>Verwijdert spaties en zet om naar hoofdletters: "1234 ab" -> "1234AB".</summary>
    public static string Normalize(string? value) =>
        new string((value ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    /// <summary>Geldig is een 4-cijferige postcode (PC4) of een volledige postcode (PC6).</summary>
    public static bool IsValid(string? value) => ValidPattern().IsMatch(Normalize(value));

    public static string Pc4(string normalized) => normalized[..4];

    /// <summary>
    /// Bepaalt of een onderbreking-postcode (PC4 of PC6) de postcode van een abonnee raakt.
    /// </summary>
    public static bool Matches(string subscriberPostalCode, string disruptionPostalCode)
    {
        var subscriber = Normalize(subscriberPostalCode);
        var disruption = Normalize(disruptionPostalCode);

        if (subscriber.Length < 4 || disruption.Length < 4)
            return false;

        // Onderbreking of abonnee op PC4-niveau: vergelijk alleen de cijfers.
        if (disruption.Length == 4 || subscriber.Length == 4)
            return Pc4(subscriber) == Pc4(disruption);

        return subscriber == disruption;
    }

    public static bool Affects(Disruption disruption, string subscriberPostalCode) =>
        disruption.PostalCodes.Any(pc => Matches(subscriberPostalCode, pc));
}
