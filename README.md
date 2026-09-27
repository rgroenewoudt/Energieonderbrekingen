# Storingsalert – e-mail bij stroom- en gasonderbrekingen

Website waar bezoekers hun **postcode** en **e-mailadres** invullen. Ze krijgen automatisch een e-mail bij:

- een **storing** in stroom of gas op hun postcode;
- een **geplande onderbreking** (onderhoud);
- het **oplossen** van een onderbreking waarover ze eerder zijn gemaild.

De gegevens komen van de web-API van [energieonderbrekingen.nl](https://www.energieonderbrekingen.nl) (Enexis, Liander, Stedin). E-mails gaan via **SendGrid SMTP**.

## Hoe het werkt

- **ASP.NET Core 10** Razor Pages + **EF Core / SQLite**.
- `DisruptionMonitor` (BackgroundService) pollt elke **15 minuten** (`PollIntervalMinutes`).
- Per unieke 4-cijferige postcode (PC4) van de abonnees wordt één API-call gedaan:
  `GET /api/v1/disruptions?postalCode={PC4}&lastUpdated={vandaag-2d}&limit=100`.
- Een onderbreking raakt een abonnee als één van de postcodes in `location.features.properties.postalCode`
  (bijv. `4321AB;4321AC`) gelijk is aan de postcode van de abonnee, of als een van beide alleen een PC4 is en die overeenkomt.
- Per abonnee en onderbreking wordt in `NotificationLogs` bijgehouden welke mail (`Started` / `Resolved`) al is verstuurd,
  zodat er nooit dubbel gemaild wordt. Opgelost = `period.end` is gevuld. Mislukt het versturen, dan wordt het de volgende poll opnieuw geprobeerd.
- Elke e-mail bevat een afmeldlink (en `List-Unsubscribe` headers voor one-click unsubscribe in mailclients).
- Aanmelden is rate-limited (5 per minuut per IP) en heeft een honeypot-veld tegen bots.

## Lokaal draaien

```bash
cd src/Energieonderbrekingen.Web
dotnet run
```

Open http://localhost:5047. Zonder configuratie:

- gebruikt de app **voorbeelddata** uit `SampleData/disruptions.json` (`MockDisruptionClient`);
- worden e-mails **niet verstuurd maar gelogd** in de console (`LoggingEmailSender`).

Tip: meld je aan met postcode `4321 AB` (stroomstoring) of `3011 AA` (geplande gasonderbreking). Pas de datums in
`disruptions.json` aan als ze in het verleden liggen, en vul `period.end` om de "opgelost"-mail te testen.
Het bestand wordt bij elke poll opnieuw ingelezen. Zet `Energieonderbrekingen__PollIntervalMinutes=1` om sneller te testen.

## Configuratie

Zet secrets **nooit** in `appsettings.json` (deze repo is publiek). Gebruik user-secrets of environment variables.

### SendGrid

1. Maak in SendGrid een API key met *Mail Send*-rechten en verifieer het afzenderadres/domein (Sender Authentication).
2. Configureer:

```bash
cd src/Energieonderbrekingen.Web
dotnet user-secrets set "Smtp:Password" "SG.xxxxx"         # SendGrid API key
dotnet user-secrets set "Smtp:FromEmail" "storingen@jouwdomein.nl"
```

Host `smtp.sendgrid.net`, poort `587` (STARTTLS) en gebruikersnaam `apikey` staan al als standaard ingesteld.

### energieonderbrekingen.nl API

API-toegang (client id, client secret, audience) wordt op aanvraag verstrekt; neem contact op via een van de netbeheerders.
Zie de [API-documentatie](https://s3-eu-west-1.amazonaws.com/nl.energieonderbrekingen.document/Energieonderbrekingen_-_Beschrijving_van_de_interface.pdf).

```bash
dotnet user-secrets set "Energieonderbrekingen:UseMock" "false"
dotnet user-secrets set "Energieonderbrekingen:ClientId" "..."
dotnet user-secrets set "Energieonderbrekingen:ClientSecret" "..."
dotnet user-secrets set "Energieonderbrekingen:Audience" "https://energieonderbrekingen.nl/api/v1"
```

Het Auth0-token (client-credentials flow) wordt gecachet tot 5 minuten voor het verloopt.

### Overige instellingen

| Sleutel | Standaard | Omschrijving |
|---|---|---|
| `App:PublicBaseUrl` | `http://localhost:5047` | Publieke URL, gebruikt in afmeldlinks |
| `ConnectionStrings:Default` | `Data Source=energieonderbrekingen.db` | SQLite database |
| `Energieonderbrekingen:PollIntervalMinutes` | `15` | Poll-interval |
| `Energieonderbrekingen:LookbackDays` | `2` | `lastUpdated`-filter richting de API |

In productie als environment variables, bijv. `Smtp__Password`, `Energieonderbrekingen__ClientSecret`, `App__PublicBaseUrl`.

## Tests

```bash
dotnet test
```

## Projectstructuur

```
src/Energieonderbrekingen.Web
  Api/        API-client, mock-client, datamodel, postcode-matching
  Data/       EF Core DbContext + migraties
  Email/      SendGrid SMTP sender (MailKit) en e-mailtemplates
  Services/   DisruptionNotifier (één poll-cyclus) en DisruptionMonitor (achtergrondtaak)
  Pages/      Aanmeld-, afmeld- en privacypagina
tests/Energieonderbrekingen.Tests
```
