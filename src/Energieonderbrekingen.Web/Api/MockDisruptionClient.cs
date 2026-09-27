using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Energieonderbrekingen.Web.Api;

/// <summary>
/// Leest onderbrekingen uit een lokaal JSON-bestand (zelfde formaat als de echte API).
/// Het bestand wordt bij elke aanroep opnieuw gelezen, zodat je tijdens het draaien storingen
/// kunt toevoegen of oplossen (vul period.end) om de e-mails te testen.
/// </summary>
public sealed class MockDisruptionClient(
    IOptions<EnergieonderbrekingenOptions> options,
    IWebHostEnvironment environment) : IDisruptionClient
{
    public async Task<IReadOnlyList<Disruption>> GetDisruptionsAsync(string postalCode, CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.ContentRootPath, options.Value.MockDataPath);
        await using var stream = File.OpenRead(path);
        var all = await JsonSerializer.DeserializeAsync<List<Disruption>>(stream, DisruptionJson.Options, cancellationToken) ?? [];
        return all.Where(d => PostalCode.Affects(d, postalCode)).ToList();
    }
}
