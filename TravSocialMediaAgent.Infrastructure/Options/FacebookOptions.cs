namespace TravSocialMediaAgent.Infrastructure.Options;

public sealed class FacebookOptions
{
    public const string SectionName = "Facebook";

    public string GraphApiVersion { get; set; } = "v24.0";

    public string? PageId { get; set; }

    public string? PageAccessToken { get; set; }
}
