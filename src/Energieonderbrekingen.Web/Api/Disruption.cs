using System.Text.Json.Serialization;

namespace Energieonderbrekingen.Web.Api;

/// <summary>Een onderbreking zoals geleverd door de energieonderbrekingen.nl web API (datamodel v1.4).</summary>
public sealed class Disruption
{
    public string Id { get; set; } = "";
    public string? Version { get; set; }
    public DisruptionSource? Source { get; set; }
    public DisruptionNetwork? Network { get; set; }
    public DisruptionPeriod? Period { get; set; }
    public bool Planned { get; set; }
    public bool Maintenance { get; set; }
    public DisruptionImpact? Impact { get; set; }
    public DisruptionLocation? Location { get; set; }
    public string? Cause { get; set; }
    public string? Component { get; set; }
    public string? Status { get; set; }
    public string? Plan { get; set; }

    [JsonPropertyName("titel")]
    public string? Title { get; set; }

    public string? Message { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonIgnore]
    public bool IsResolved => Period?.End is not null;

    [JsonIgnore]
    public bool IsGas => string.Equals(Network?.Type, "gas", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public DisruptionProperties? Properties => Location?.Features?.Properties;

    [JsonIgnore]
    public DateTimeOffset? Start => Period?.Begin ?? Period?.PlannedBegin;

    [JsonIgnore]
    public DateTimeOffset? ExpectedEnd => Period?.End ?? Period?.ExpectedEnd ?? Period?.PlannedEnd;

    /// <summary>Alle postcodes (4 of 6 tekens, genormaliseerd) waar deze onderbreking betrekking op heeft.</summary>
    public IEnumerable<string> PostalCodes =>
        (Properties?.PostalCode ?? "")
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(PostalCode.Normalize)
            .Where(pc => pc.Length is 4 or 6);
}

public sealed class DisruptionSource
{
    public string? Id { get; set; }
    public string? Organisation { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}

public sealed class DisruptionNetwork
{
    public bool Delivery { get; set; }
    public bool Metering { get; set; }
    public string? Type { get; set; }
}

public sealed class DisruptionPeriod
{
    public DateTimeOffset? PlannedBegin { get; set; }
    public DateTimeOffset? PlannedEnd { get; set; }
    public DateTimeOffset? Begin { get; set; }
    public DateTimeOffset? End { get; set; }
    public DateTimeOffset? ExpectedEnd { get; set; }
}

public sealed class DisruptionImpact
{
    public int? Households { get; set; }
}

public sealed class DisruptionLocation
{
    public DisruptionFeature? Features { get; set; }
}

public sealed class DisruptionFeature
{
    public DisruptionProperties? Properties { get; set; }
}

public sealed class DisruptionProperties
{
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Street { get; set; }
}
