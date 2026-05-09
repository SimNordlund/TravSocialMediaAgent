using System.Globalization;

namespace TravSocialMediaAgent.Infrastructure.Options;

internal sealed class TravoltaTopByDayOptions
{
    public const string SectionName = "TravoltaTopByDay";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:8081";

    public string Date { get; set; } = string.Empty;

    public string Track { get; set; } = string.Empty;

    public string Form { get; set; } = "vinnare";

    public int TopN { get; set; } = 3;

    public string ResolveDate() =>
        string.IsNullOrWhiteSpace(Date)
            ? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Date.Trim();

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("TravoltaTopByDay:BaseUrl must be an absolute URL.");
        }

        if (TopN is < 1 or > 20)
        {
            throw new InvalidOperationException("TravoltaTopByDay:TopN must be between 1 and 20.");
        }

        if (Enabled && string.IsNullOrWhiteSpace(Track))
        {
            throw new InvalidOperationException("TravoltaTopByDay:Track is required when TravoltaTopByDay:Enabled is true.");
        }
    }
}
