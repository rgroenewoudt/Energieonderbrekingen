using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Energieonderbrekingen.Web.Api;

/// <summary>
/// Client voor https://energieonderbrekingen.nl/api/v1. Authenticatie via Auth0 client-credentials flow;
/// het token wordt gecachet tot kort voor het verloopt.
/// </summary>
public sealed class EnergieonderbrekingenClient(
    HttpClient http,
    IOptions<EnergieonderbrekingenOptions> options,
    TimeProvider timeProvider,
    ILogger<EnergieonderbrekingenClient> logger) : IDisruptionClient
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _token;
    private static DateTimeOffset _tokenExpiresAt;

    private readonly EnergieonderbrekingenOptions _options = options.Value;

    public async Task<IReadOnlyList<Disruption>> GetDisruptionsAsync(string postalCode, CancellationToken cancellationToken)
    {
        var since = timeProvider.GetUtcNow().AddDays(-_options.LookbackDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/disruptions?postalCode={Uri.EscapeDataString(postalCode)}&lastUpdated={since}&limit=100";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            _token = null; // volgende poll een nieuw token ophalen
        response.EnsureSuccessStatusCode();

        var disruptions = await response.Content.ReadFromJsonAsync<List<Disruption>>(DisruptionJson.Options, cancellationToken) ?? [];
        logger.LogDebug("{Count} onderbrekingen opgehaald voor postcode {PostalCode}", disruptions.Count, postalCode);
        return disruptions;
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && timeProvider.GetUtcNow() < _tokenExpiresAt)
            return _token;

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && timeProvider.GetUtcNow() < _tokenExpiresAt)
                return _token;

            using var response = await http.PostAsJsonAsync(_options.TokenUrl, new TokenRequest(
                _options.ClientId, _options.ClientSecret, _options.Audience, "client_credentials"), cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Leeg token-antwoord van Auth0.");

            _token = token.AccessToken;
            _tokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(60, token.ExpiresIn - 300));
            logger.LogInformation("Nieuw API-token opgehaald, geldig tot {ExpiresAt:u}", _tokenExpiresAt);
            return _token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private sealed record TokenRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_secret")] string ClientSecret,
        [property: JsonPropertyName("audience")] string Audience,
        [property: JsonPropertyName("grant_type")] string GrantType);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
