namespace TravSocialMediaAgent.Application.Options;

public sealed class PostingOptions
{
    public const string SectionName = "Posting";

    public bool DryRun { get; set; } = true;

    public bool PostOnStartup { get; set; }

    public TimeSpan MinimumDelay { get; set; } = TimeSpan.FromHours(4);

    public TimeSpan MaximumDelay { get; set; } = TimeSpan.FromHours(8);

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(15);

    public string TravanalysUrl { get; set; } = "https://travanalys.se";

    public bool AppendResponsibleGamingText { get; set; } = true;

    public string ResponsibleGamingText { get; set; } = "Spela ansvarsfullt. 18+";

    public int MaxCharacters { get; set; } = 500;

    public string[] OptionalHashtags { get; set; } = ["#trav", "#travtips", "#Travanalys"];

    public string[] ContentAngles { get; set; } =
    [
        "direct reminder",
        "friendly nudge",
        "analysis-focused",
        "race day mood",
        "last-call before first start",
    ];

    public void Validate()
    {
        if (MinimumDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Posting:MinimumDelay must be zero or greater.");
        }

        if (MaximumDelay < MinimumDelay)
        {
            throw new InvalidOperationException(
                "Posting:MaximumDelay must be greater than or equal to Posting:MinimumDelay."
            );
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Posting:RetryDelay must be zero or greater.");
        }

        if (MaxCharacters < 0)
        {
            throw new InvalidOperationException("Posting:MaxCharacters must be zero or greater.");
        }
    }
}
