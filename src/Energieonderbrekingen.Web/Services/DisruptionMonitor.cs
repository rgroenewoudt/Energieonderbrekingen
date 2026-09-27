using Energieonderbrekingen.Web.Api;
using Microsoft.Extensions.Options;

namespace Energieonderbrekingen.Web.Services;

/// <summary>Draait <see cref="DisruptionNotifier"/> periodiek (standaard elke 15 minuten).</summary>
public sealed class DisruptionMonitor(
    IServiceScopeFactory scopeFactory,
    IOptions<EnergieonderbrekingenOptions> options,
    ILogger<DisruptionMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.PollIntervalMinutes));
        logger.LogInformation("Storingsmonitor gestart, interval {Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<DisruptionNotifier>();
                var sent = await notifier.RunOnceAsync(stoppingToken);
                if (sent > 0)
                    logger.LogInformation("{Count} storingsmail(s) verstuurd", sent);
            }
            // Alleen doorgooien bij afsluiten; een HTTP-time-out (TaskCanceledException) mag de host niet stoppen.
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Poll-cyclus mislukt");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
