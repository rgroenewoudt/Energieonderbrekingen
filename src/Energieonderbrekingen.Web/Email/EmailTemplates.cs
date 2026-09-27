using System.Globalization;
using System.Net;
using System.Text;
using Energieonderbrekingen.Web.Api;

namespace Energieonderbrekingen.Web.Email;

public static class EmailTemplates
{
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");
    private static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

    public static EmailMessage Welcome(string to, string postalCode, string unsubscribeUrl) => Build(
        to,
        $"Aangemeld voor storingsmeldingen in {FormatPostalCode(postalCode)}",
        "Je bent aangemeld",
        [$"Je ontvangt vanaf nu een e-mail zodra er een stroom- of gasstoring of een geplande onderbreking is in postcode {FormatPostalCode(postalCode)}."],
        [],
        unsubscribeUrl);

    public static EmailMessage Started(string to, string postalCode, Disruption d, string unsubscribeUrl)
    {
        var what = d.Planned ? $"Geplande {NetworkName(d)}onderbreking" : $"{Capitalize(NetworkName(d))}storing";
        var where = Place(d);
        return Build(
            to,
            $"{what}{(where is null ? "" : $" in {where}")}",
            d.Title ?? what,
            [d.Message ?? $"Er is een {what.ToLowerInvariant()} die postcode {FormatPostalCode(postalCode)} raakt."],
            Details(d),
            unsubscribeUrl);
    }

    public static EmailMessage Resolved(string to, string postalCode, Disruption d, string unsubscribeUrl)
    {
        var what = d.Planned ? $"Geplande {NetworkName(d)}onderbreking" : $"{Capitalize(NetworkName(d))}storing";
        var where = Place(d);
        return Build(
            to,
            $"Opgelost: {what.ToLowerInvariant()}{(where is null ? "" : $" in {where}")}",
            $"{what} is opgelost",
            [$"De {what.ToLowerInvariant()} in postcode {FormatPostalCode(postalCode)} is opgelost."],
            Details(d),
            unsubscribeUrl);
    }

    private static List<(string Label, string Value)> Details(Disruption d)
    {
        var rows = new List<(string, string)>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                rows.Add((label, value));
        }

        Add("Type", d.IsGas ? "Gas" : "Stroom");
        Add("Locatie", string.Join(", ", new[] { d.Properties?.Street, d.Properties?.City }.Where(s => !string.IsNullOrWhiteSpace(s))));
        Add(d.Planned && d.Period?.Begin is null ? "Gepland vanaf" : "Begonnen", FormatDate(d.Start));
        Add(d.IsResolved ? "Opgelost om" : "Verwacht einde", FormatDate(d.ExpectedEnd));
        Add("Status", d.Status);
        Add("Oorzaak", d.Cause);
        Add("Netbeheerder", d.Source?.Organisation);
        Add("Getroffen huishoudens", d.Impact?.Households?.ToString(Dutch));
        return rows;
    }

    private static EmailMessage Build(string to, string subject, string heading, string[] paragraphs,
        List<(string Label, string Value)> details, string unsubscribeUrl)
    {
        static string e(string value) => WebUtility.HtmlEncode(value);

        var html = new StringBuilder();
        html.Append("""
            <!doctype html><html lang="nl"><body style="margin:0;padding:24px;background:#f4f6f8;font-family:Segoe UI,Arial,sans-serif;color:#1f2933">
            <table role="presentation" width="100%" style="max-width:560px;margin:0 auto;background:#fff;border-radius:8px;padding:24px">
            <tr><td>
            """);
        html.Append($"<h1 style=\"font-size:20px;margin:0 0 16px\">{e(heading)}</h1>");
        foreach (var p in paragraphs)
            html.Append($"<p style=\"line-height:1.5\">{e(p)}</p>");
        if (details.Count > 0)
        {
            html.Append("<table role=\"presentation\" style=\"border-collapse:collapse;width:100%;margin:16px 0\">");
            foreach (var (label, value) in details)
                html.Append($"<tr><td style=\"padding:6px 12px 6px 0;color:#52606d;white-space:nowrap;vertical-align:top\">{e(label)}</td><td style=\"padding:6px 0\">{e(value)}</td></tr>");
            html.Append("</table>");
        }
        html.Append($"""
            <p style="font-size:12px;color:#7b8794;margin-top:24px">Bron: energieonderbrekingen.nl. Geen meldingen meer ontvangen? <a href="{e(unsubscribeUrl)}" style="color:#7b8794">Afmelden</a>.</p>
            </td></tr></table></body></html>
            """);

        var text = new StringBuilder();
        text.AppendLine(heading).AppendLine();
        foreach (var p in paragraphs)
            text.AppendLine(p).AppendLine();
        foreach (var (label, value) in details)
            text.AppendLine($"{label}: {value}");
        text.AppendLine().AppendLine("Bron: energieonderbrekingen.nl").AppendLine($"Afmelden: {unsubscribeUrl}");

        return new EmailMessage(to, subject, html.ToString(), text.ToString(), unsubscribeUrl);
    }

    private static string NetworkName(Disruption d) => d.IsGas ? "gas" : "stroom";

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    private static string? Place(Disruption d) =>
        string.IsNullOrWhiteSpace(d.Properties?.City) ? null : d.Properties!.City;

    public static string FormatPostalCode(string pc) => pc.Length == 6 ? $"{pc[..4]} {pc[4..]}" : pc;

    private static string? FormatDate(DateTimeOffset? value) =>
        value is null ? null : TimeZoneInfo.ConvertTime(value.Value, Amsterdam).ToString("dddd d MMMM yyyy 'om' HH:mm", Dutch);
}
