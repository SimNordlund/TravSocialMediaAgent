namespace TravSocialMediaAgent.Infrastructure.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string ModelId { get; set; } = "gpt-4o-mini";

    public string? ApiKey { get; set; }

    public float Temperature { get; set; } = 0.9f;

    public float TopP { get; set; } = 0.95f;

    public int MaxOutputTokens { get; set; } = 180;
}
