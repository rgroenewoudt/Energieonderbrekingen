using System.Text.Json;
using Energieonderbrekingen.Web.Api;

namespace Energieonderbrekingen.Tests;

public class DisruptionJsonTests
{
    // Voorbeeld uit de officiële API-documentatie (5 maart 2019).
    private const string DocumentationSample = """
        [{
          "version": "1.4", "id": "5ac77d245a7ffb0001ed0551",
          "source": { "EAN13": "", "id": "4829726", "organisation": "Liander", "origin": "", "removed": "", "timestamp": "2008-04-06T15:58:12+0200" },
          "network": { "delivery": true, "metering": false, "type": "electricity" },
          "period": { "plannedBegin": "", "plannedEnd": "", "begin": "2008-04-06T15:03:38+0200", "end": "", "expectedEnd": "2008-04-06T18:03:38+0200" },
          "planned": false, "maintenance": false,
          "impact": { "EANs": [], "households": 25, "min": false, "max": true },
          "location": { "features": { "geometry": { "coordinates": [ 53.07865, 5.796473 ], "type": "Point" },
            "properties": { "city": "Amsterdam", "postalCode": "4321AB;4321AC", "radius": 100, "street": "Dorpstraat" }, "type": "Feature" },
            "type": "FeatureCollection" },
          "cause": "spontane storing", "component": "kabel", "status": "monteur ter plaatse", "plan": "",
          "titel": "Stroomstoring in Amsterdam", "message": "Er is sinds 15:03 een stroomstoring in Amsterdam.",
          "lastUpdated": "2008-04-06T15:58:59.546515+0200"
        }]
        """;

    [Fact]
    public void Deserializes_documentation_sample()
    {
        var d = Assert.Single(JsonSerializer.Deserialize<List<Disruption>>(DocumentationSample, DisruptionJson.Options)!);

        Assert.Equal("5ac77d245a7ffb0001ed0551", d.Id);
        Assert.Equal("Liander", d.Source?.Organisation);
        Assert.Equal("Stroomstoring in Amsterdam", d.Title);
        Assert.False(d.IsGas);
        Assert.False(d.IsResolved);
        Assert.Null(d.Period!.PlannedBegin);
        Assert.Null(d.Period.End);
        Assert.Equal(new DateTimeOffset(2008, 4, 6, 15, 3, 38, TimeSpan.FromHours(2)), d.Period.Begin);
        Assert.Equal(new DateTimeOffset(2008, 4, 6, 18, 3, 38, TimeSpan.FromHours(2)), d.ExpectedEnd);
        Assert.NotNull(d.LastUpdated);
        Assert.Equal(25, d.Impact?.Households);
        Assert.Equal(["4321AB", "4321AC"], d.PostalCodes);
    }

    [Fact]
    public void Filled_end_date_means_resolved()
    {
        var json = DocumentationSample.Replace("\"end\": \"\"", "\"end\": \"2008-04-06T17:00:00+0200\"");
        var d = Assert.Single(JsonSerializer.Deserialize<List<Disruption>>(json, DisruptionJson.Options)!);

        Assert.True(d.IsResolved);
        Assert.Equal(new DateTimeOffset(2008, 4, 6, 17, 0, 0, TimeSpan.FromHours(2)), d.ExpectedEnd);
    }

    [Fact]
    public void Sample_data_file_is_valid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SampleData", "disruptions.json");
        var all = JsonSerializer.Deserialize<List<Disruption>>(File.ReadAllText(path), DisruptionJson.Options)!;

        Assert.Contains(all, d => d.Planned && d.IsGas);
        Assert.Contains(all, d => !d.Planned && !d.IsGas);
    }
}
