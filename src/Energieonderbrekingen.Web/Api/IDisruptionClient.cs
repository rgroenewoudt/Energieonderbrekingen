namespace Energieonderbrekingen.Web.Api;

public interface IDisruptionClient
{
    /// <summary>Haalt open en recent bijgewerkte onderbrekingen op voor een postcode (PC4 of PC6).</summary>
    Task<IReadOnlyList<Disruption>> GetDisruptionsAsync(string postalCode, CancellationToken cancellationToken);
}
