# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

"Storingsalert": an ASP.NET Core 10 Razor Pages site (EF Core + SQLite) where visitors subscribe with a postal code and email address and get emailed about power/gas disruptions from the energieonderbrekingen.nl API. The README (Dutch) covers configuration in detail. UI text, email templates, code comments and log messages are in **Dutch**, so keep new ones in Dutch too.

## Migrations

Migrations (requires the `dotnet-ef` tool; there's no tool manifest in the repo):
```bash
dotnet ef migrations add <Name> --project src/Energieonderbrekingen.Web
```
The app calls `Database.Migrate()` at startup, and the tests migrate an in-memory SQLite DB. After any model change you must add a migration, or both will break.

## Architecture

- **Implementation selection in `Program.cs`**: `IDisruptionClient` is the real `EnergieonderbrekingenClient` (Auth0 client-credentials, token cached) only when `Energieonderbrekingen:UseMock=false` *and* credentials are set. Otherwise it's `MockDisruptionClient`, which re-reads `SampleData/disruptions.json` on every poll. `IEmailSender` is `SmtpEmailSender` (MailKit → SendGrid) when SMTP is configured, otherwise `LoggingEmailSender`. So with no configuration the app runs fully offline.
- **Polling**: `DisruptionMonitor` (BackgroundService) creates a scope each interval and calls `DisruptionNotifier.RunOnceAsync`, which holds all of the notification logic:
  - It makes one API call per unique PC4 (the first 4 digits of the postal code) across all subscriptions, then filters the results per subscriber with `PostalCode.Affects`. Disruption postal codes can be PC4 or PC6; if either side is PC4, only the digits are compared.
  - Dedup is done through `NotificationLogs`, which has a unique `(SubscriptionId, DisruptionId, Kind)` constraint with `Kind` = `Started` | `Resolved`. A `Resolved` mail only goes out if `Started` was sent earlier. An unresolved disruption whose expected end has already passed gets no mail. A send failure isn't logged, so the next poll retries it.
  - "Resolved" means `period.end` is set (`Disruption.IsResolved`).
- **API JSON gotcha**: dates arrive as `2008-04-06T15:58:12+0200` (offset without colon) and empty fields as `""` instead of null, so always deserialize with `DisruptionJson.Options` (it registers `FlexibleDateTimeOffsetConverter`). `location.features.properties.postalCode` holds `;`-separated values; use `Disruption.PostalCodes`.
- **Time**: code depends on the injected `TimeProvider` (never `DateTime.Now`), so tests can use a fixed clock.
## Tests

`DisruptionNotifierTests` runs the notifier against a real in-memory SQLite connection (migrated) with fake client, sender and `TimeProvider` classes defined in the test file. Follow that pattern rather than mocking `AppDbContext`.

## Configuration

The repo is public, so secrets (`Smtp:Password`, `Energieonderbrekingen:ClientSecret`, …) belong in user-secrets (`UserSecretsId=energieonderbrekingen-web`) or environment variables (`Smtp__Password`), never in `appsettings*.json`.
