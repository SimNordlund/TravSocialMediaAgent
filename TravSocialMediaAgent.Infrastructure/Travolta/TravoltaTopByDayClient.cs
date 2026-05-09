using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TravSocialMediaAgent.Infrastructure.Options;

namespace TravSocialMediaAgent.Infrastructure.Travolta;

internal sealed class TravoltaTopByDayClient(
    HttpClient httpClient,
    TravoltaTopByDayOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true
    };

    public async Task<TopByDayResponse?> GetAsync(CancellationToken cancellationToken)
    {
        var query =
            "api/trav/analysis/top-by-day"
            + $"?date={Uri.EscapeDataString(options.ResolveDate())}"
            + $"&track={Uri.EscapeDataString(options.Track.Trim())}"
            + $"&form={Uri.EscapeDataString(options.Form.Trim())}"
            + $"&topN={options.TopN}";

        using var response = await httpClient.GetAsync(query, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Travolta Top By Day API returned {(int)response.StatusCode} {response.ReasonPhrase}: {content}");
        }

        return JsonSerializer.Deserialize<TopByDayResponse>(content, JsonOptions);
    }
}
