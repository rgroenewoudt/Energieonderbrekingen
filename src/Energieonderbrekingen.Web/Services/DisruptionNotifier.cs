using Energieonderbrekingen.Web.Api;
using Energieonderbrekingen.Web.Data;
using Energieonderbrekingen.Web.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Energieonderbrekingen.Web.Services;

/// <summary>
/// Eén poll-cyclus: haalt per PC4 de onderbrekingen op en mailt abonnees bij een nieuwe
/// onderbreking (<see cref="NotificationKind.Started"/>) en wanneer die is opgelost
/// (<see cref="NotificationKind.Resolved"/>).
/// </summary>
public sealed class DisruptionNotifier(
    AppDbContext db,
    IDisruptionClient client,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider,
    ILogger<DisruptionNotifier> logger)
{
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await db.Subscriptions.AsNoTracking().ToListAsync(cancellationToken);
        if (subscriptions.Count == 0)
            return 0;

        var sent = (await db.NotificationLogs.AsNoTracking()
                .Select(n => new { n.SubscriptionId, n.DisruptionId, n.Kind })
                .ToListAsync(cancellationToken))
            .Select(n => (n.SubscriptionId, n.DisruptionId, n.Kind))
            .ToHashSet();

        var emailsSent = 0;

        // Eén API-call per PC4 i.p.v. per abonnee.
        foreach (var group in subscriptions.GroupBy(s => PostalCode.Pc4(s.PostalCode)))
        {
            IReadOnlyList<Disruption> disruptions;
            try
            {
                disruptions = await client.GetDisruptionsAsync(group.Key, cancellationToken);
            }
            // Een HTTP-time-out is ook een OperationCanceledException; alleen doorgooien bij afsluiten.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ophalen van onderbrekingen voor {Pc4} mislukt", group.Key);
                continue;
            }

            foreach (var subscription in group)
            {
                foreach (var disruption in disruptions.Where(d => PostalCode.Affects(d, subscription.PostalCode)))
                {
                    var kind = DetermineNotification(subscription, disruption, sent);
                    if (kind is null)
                        continue;

                    if (await TrySendAsync(subscription, disruption, kind.Value, cancellationToken))
                    {
                        sent.Add((subscription.Id, disruption.Id, kind.Value));
                        emailsSent++;
                    }
                }
            }
        }

        return emailsSent;
    }

    private NotificationKind? DetermineNotification(
        Subscription subscription, Disruption disruption,
        HashSet<(int, string, NotificationKind)> sent)
    {
        var startedSent = sent.Contains((subscription.Id, disruption.Id, NotificationKind.Started));

        if (!disruption.IsResolved)
        {
            if (startedSent)
                return null;

            // Geplande onderbreking die al voorbij is maar nooit is afgemeld: niet meer melden.
            if (disruption.ExpectedEnd is { } end && end < timeProvider.GetUtcNow())
                return null;

            return NotificationKind.Started;
        }

        // Alleen een "opgelost"-mail als we de abonnee eerder over deze onderbreking hebben gemaild.
        if (startedSent && !sent.Contains((subscription.Id, disruption.Id, NotificationKind.Resolved)))
            return NotificationKind.Resolved;

        return null;
    }

    private async Task<bool> TrySendAsync(Subscription subscription, Disruption disruption, NotificationKind kind, CancellationToken cancellationToken)
    {
        var unsubscribeUrl = appOptions.Value.UnsubscribeUrl(subscription.UnsubscribeToken);
        var message = kind == NotificationKind.Started
            ? EmailTemplates.Started(subscription.Email, subscription.PostalCode, disruption, unsubscribeUrl)
            : EmailTemplates.Resolved(subscription.Email, subscription.PostalCode, disruption, unsubscribeUrl);

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Niet loggen als verstuurd: volgende poll opnieuw proberen.
            logger.LogError(ex, "Versturen {Kind}-mail voor onderbreking {DisruptionId} naar abonnement {SubscriptionId} mislukt",
                kind, disruption.Id, subscription.Id);
            return false;
        }

        var log = new NotificationLog
        {
            SubscriptionId = subscription.Id,
            DisruptionId = disruption.Id,
            Kind = kind,
            SentAt = timeProvider.GetUtcNow(),
        };
        db.NotificationLogs.Add(log);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Bijv. abonnee heeft zich tijdens deze poll afgemeld (foreign key). Loskoppelen zodat
            // volgende SaveChanges niet opnieuw op deze regel falen, en doorgaan met de rest.
            db.Entry(log).State = EntityState.Detached;
            logger.LogWarning(ex, "Vastleggen {Kind}-mail voor onderbreking {DisruptionId} bij abonnement {SubscriptionId} mislukt",
                kind, disruption.Id, subscription.Id);
        }
        return true;
    }
}
