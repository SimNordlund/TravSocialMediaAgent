using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TravSocialMediaAgent.Application.Abstractions;
using TravSocialMediaAgent.Application.Options;
using TravSocialMediaAgent.Domain.Posts;
using TravSocialMediaAgent.Infrastructure.Options;

namespace TravSocialMediaAgent.Infrastructure.OpenAI;

internal sealed partial class OpenAiPostContentGenerator(
    IChatClient chatClient,
    AiOptions aiOptions,
    PostingOptions postingOptions,
    ILogger<OpenAiPostContentGenerator> logger) : IPostContentGenerator
{
    private const int RecentPostLimit = 20;

    private readonly Queue<string> _recentPosts = new();

    public async Task<TravSocialPost> GenerateAsync(CancellationToken cancellationToken)
    {
        var seed = Random.Shared.NextInt64(1, long.MaxValue);
        var angle = Pick(postingOptions.ContentAngles, "direct reminder");
        var hashtags = string.Join(", ", postingOptions.OptionalHashtags ?? []);

        ChatMessage[] messages =
        [
            new(ChatRole.System, BuildSystemPrompt()),
            new(ChatRole.User, BuildUserPrompt(angle, hashtags, seed))
        ];

        var chatOptions = new ChatOptions
        {
            Temperature = aiOptions.Temperature,
            TopP = aiOptions.TopP,
            MaxOutputTokens = aiOptions.MaxOutputTokens,
            Seed = seed
        };

        var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        var generatedPost = Clean(response.Text);

        if (string.IsNullOrWhiteSpace(generatedPost))
        {
            logger.LogWarning("AI returned an empty post. Falling back to a local Travanalys template.");
            generatedPost = BuildFallbackPost();
        }

        generatedPost = EnsureTravanalysUrl(generatedPost);
        generatedPost = AddResponsibleGamingText(generatedPost);
        generatedPost = LimitLength(generatedPost);

        if (_recentPosts.Any(previous => string.Equals(previous, generatedPost, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("AI generated a duplicate post. Falling back to a local Travanalys template.");
            generatedPost = LimitLength(AddResponsibleGamingText(EnsureTravanalysUrl(BuildFallbackPost())));
        }

        Remember(generatedPost);
        return new TravSocialPost(generatedPost);
    }

    private static string BuildSystemPrompt() =>
        """
        You are a careful Swedish social media assistant for the Facebook page Travanalys.
        Create short, natural Facebook posts that remind followers that it is dax att tippa on travanalys.se.
        Never promise wins, guaranteed results, insider information, or risk-free betting.
        Keep the tone confident, friendly, and concise.
        """;

    private string BuildUserPrompt(string angle, string hashtags, long seed) =>
        $"""
        Write exactly one Facebook post in Swedish.
        Campaign goal: remind people that it is dax att tippa at {postingOptions.TravanalysUrl}.
        Angle: {angle}.
        Random seed: {seed}.
        Format rules:
        - 1 to 3 short sentences.
        - No headline, no markdown, no quotation marks.
        - Mention Travanalys or travanalys.se once.
        - Use the phrase "dax att tippa" or a close Swedish variation.
        - Optional hashtags, only if natural: {hashtags}.
        - Avoid repeating common openings like "Nu ar det dags" every time.
        - Do not include legal disclaimers; the app adds them when configured.
        """;

    private string EnsureTravanalysUrl(string post)
    {
        var url = string.IsNullOrWhiteSpace(postingOptions.TravanalysUrl)
            ? "https://travanalys.se"
            : postingOptions.TravanalysUrl.Trim();

        return post.Contains("travanalys.se", StringComparison.OrdinalIgnoreCase)
            ? post
            : $"{post} {url}";
    }

    private string AddResponsibleGamingText(string post)
    {
        if (!postingOptions.AppendResponsibleGamingText ||
            string.IsNullOrWhiteSpace(postingOptions.ResponsibleGamingText))
        {
            return post;
        }

        var text = postingOptions.ResponsibleGamingText.Trim();
        return post.Contains(text, StringComparison.OrdinalIgnoreCase)
            ? post
            : $"{post} {text}";
    }

    private string LimitLength(string post)
    {
        if (postingOptions.MaxCharacters <= 0 || post.Length <= postingOptions.MaxCharacters)
        {
            return post;
        }

        return post[..postingOptions.MaxCharacters].TrimEnd(' ', ',', '.', ';', ':') + "...";
    }

    private string BuildFallbackPost()
    {
        var templates = new[]
        {
            "Travdag p\u00e5 g\u00e5ng och det \u00e4r dax att tippa hos Travanalys. Kolla l\u00e4get p\u00e5 travanalys.se",
            "Dax att tippa? Travanalys har dagens uppl\u00e4gg redo p\u00e5 travanalys.se",
            "Innan loppen drar ig\u00e5ng: ta en titt hos Travanalys och l\u00e4gg ditt tips p\u00e5 travanalys.se"
        };

        return Pick(templates, templates[0]);
    }

    private void Remember(string post)
    {
        _recentPosts.Enqueue(post);

        while (_recentPosts.Count > RecentPostLimit)
        {
            _recentPosts.Dequeue();
        }
    }

    private static string Pick(IReadOnlyList<string>? values, string fallback)
    {
        if (values is null || values.Count == 0)
        {
            return fallback;
        }

        return values[Random.Shared.Next(values.Count)];
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = WhitespaceRegex().Replace(text.Trim(), " ");
        cleaned = cleaned.Trim('"', '\'', '`', ' ');

        return cleaned.StartsWith("Post:", StringComparison.OrdinalIgnoreCase)
            ? cleaned["Post:".Length..].Trim()
            : cleaned;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
