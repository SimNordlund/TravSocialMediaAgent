using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TravSocialMediaAgent.Application.Abstractions;
using TravSocialMediaAgent.Application.Options;
using TravSocialMediaAgent.Domain.Posts;
using TravSocialMediaAgent.Infrastructure.Options;
using TravSocialMediaAgent.Infrastructure.Travolta;

namespace TravSocialMediaAgent.Infrastructure.OpenAI;

internal sealed partial class OpenAiPostContentGenerator(
    IChatClient chatClient,
    AiOptions aiOptions,
    PostingOptions postingOptions,
    TravoltaTopByDayOptions travoltaTopByDayOptions,
    TravoltaTopByDayClient travoltaTopByDayClient,
    ILogger<OpenAiPostContentGenerator> logger) : IPostContentGenerator
{
    private const int RecentPostLimit = 20;

    private readonly Queue<string> _recentPosts = new();

    public async Task<TravSocialPost?> GenerateAsync(CancellationToken cancellationToken)
    {
        var topByDay = await GetTopByDayAsync(cancellationToken);

        if (travoltaTopByDayOptions.Enabled && topByDay is null)
        {
            return null;
        }

        var seed = Random.Shared.NextInt64(1, long.MaxValue);
        var angle = Pick(postingOptions.ContentAngles, "direct reminder");
        var hashtags = string.Join(", ", postingOptions.OptionalHashtags ?? []);
        var userPrompt = topByDay is null
            ? BuildUserPrompt(angle, hashtags, seed)
            : BuildTopByDayIntroPrompt(topByDay, angle, seed);

        ChatMessage[] messages =
        [
            new(ChatRole.System, BuildSystemPrompt()),
            new(ChatRole.User, userPrompt)
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
            generatedPost = topByDay is null ? BuildFallbackPost() : BuildTopByDayFallbackIntro(topByDay);
        }

        if (topByDay is not null)
        {
            generatedPost = $"{generatedPost}{Environment.NewLine}{Environment.NewLine}{BuildTopByDayBlock(topByDay)}";
        }

        generatedPost = EnsureTravanalysUrl(generatedPost);
        generatedPost = AddAndelsspelLink(generatedPost);
        generatedPost = AddResponsibleGamingText(generatedPost);
        generatedPost = LimitLength(generatedPost);

        if (_recentPosts.Any(previous => string.Equals(previous, generatedPost, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("AI generated a duplicate post. Falling back to a local Travanalys template.");
            var fallbackPost = topByDay is null
                ? BuildFallbackPost()
                : $"{BuildTopByDayFallbackIntro(topByDay)}{Environment.NewLine}{Environment.NewLine}{BuildTopByDayBlock(topByDay)}";

            generatedPost = LimitLength(AddResponsibleGamingText(AddAndelsspelLink(EnsureTravanalysUrl(fallbackPost))));
        }

        Remember(generatedPost);
        return new TravSocialPost(generatedPost);
    }

    private async Task<TopByDayResponse?> GetTopByDayAsync(CancellationToken cancellationToken)
    {
        if (!travoltaTopByDayOptions.Enabled)
        {
            return null;
        }

        var response = await travoltaTopByDayClient.GetAsync(cancellationToken);

        if (response is null || !response.Found || response.Races.Count == 0)
        {
            logger.LogWarning(
                "No Travolta top-by-day data found for {Form} at {Track} on {Date}. Facebook publish will be skipped.",
                travoltaTopByDayOptions.Form,
                travoltaTopByDayOptions.Track,
                travoltaTopByDayOptions.ResolveDate());

            return null;
        }

        return response;
    }

    private static string BuildSystemPrompt()
    {
        return """
        You are a careful Swedish social media assistant for the Facebook page Travanalys.
        Create short, natural Facebook posts that remind followers that it is "dax att tippa on travanalys.se."
        Never promise wins, guaranteed results, insider information, or risk-free betting.
        Keep the tone confident, friendly, and concise. Never write anything about age restrictions like 18+. 
        """;
    }

    private string BuildUserPrompt(string angle, string hashtags, long seed)
    {
        return $"""
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
        - Never write anything about age restrictions like 18+. 
        """;
    }

    private string BuildTopByDayIntroPrompt(TopByDayResponse response, string angle, long seed)
    {
        var date = FirstNonWhiteSpace(response.Date, travoltaTopByDayOptions.ResolveDate());
        var track = FirstNonWhiteSpace(response.Track, travoltaTopByDayOptions.Track);
        var form = FirstNonWhiteSpace(response.Form, travoltaTopByDayOptions.Form);

        return $"""
        Write exactly one short opening sentence in Swedish for a Facebook post.
        The app will append a fixed race list after your sentence.
        Campaign goal: remind people that it is dax att tippa at {postingOptions.TravanalysUrl}.
        Context: top {travoltaTopByDayOptions.TopN} horses by regular Analys for {form} at {track} on {date}.
        Angle: {angle}.
        Random seed: {seed}.
        Format rules:
        - One sentence only.
        - Mention Travanalys or travanalys.se once.
        - Use the phrase "dax att tippa" or a close Swedish variation.
        - No horse names, horse numbers, rankings, analysis percentages, hashtags, markdown, or quotation marks.
        - Never promise wins, guaranteed results, insider information, or risk-free betting.
        - Never write anything about age restrictions like 18+.
        """;
    }

    private string EnsureTravanalysUrl(string post)
    {
        var url = string.IsNullOrWhiteSpace(postingOptions.TravanalysUrl)
            ? "https://travanalys.se"
            : postingOptions.TravanalysUrl.Trim();

        return post.Contains("travanalys.se", StringComparison.OrdinalIgnoreCase)
            ? post
            : $"{post}{GetAppendSeparator(post)}{url}";
    }

    private string BuildTopByDayBlock(TopByDayResponse response)
    {
        var date = FirstNonWhiteSpace(response.Date, travoltaTopByDayOptions.ResolveDate());
        var track = FirstNonWhiteSpace(response.Track, travoltaTopByDayOptions.Track);
        var form = FirstNonWhiteSpace(response.Form, travoltaTopByDayOptions.Form);
        var raceLabel = UsesAvdelningLabel(form) ? "Avd" : "Lopp";
        var builder = new StringBuilder();

        builder
            .Append("Topp ")
            .Append(travoltaTopByDayOptions.TopN.ToString(CultureInfo.InvariantCulture))
            .Append(" i samtliga ")
            .Append(form)
            .Append("-lopp p\u00e5 ")
            .Append(track)
            .Append(' ')
            .Append(date)
            .AppendLine()
            .AppendLine();

        foreach (var race in response.Races)
        {
            builder
                .Append(raceLabel)
                .Append(' ')
                .Append(FormatLopp(race.DisplayLopp))
                .AppendLine();

            foreach (var horse in race.Top.Take(travoltaTopByDayOptions.TopN))
            {
                builder
                    .Append(horse.Rank.ToString(CultureInfo.InvariantCulture))
                    .Append(") ")
                    .Append(horse.Number.ToString(CultureInfo.InvariantCulture))
                    .Append(". ")
                    .Append(horse.Name)
                    .Append(" - Analys ")
                    .Append(FormatPercent(horse.Analysis))
                    .Append('%')
                    .AppendLine();
            }

            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private string AddResponsibleGamingText(string post)
    {
        if (!postingOptions.AppendResponsibleGamingText
            || string.IsNullOrWhiteSpace(postingOptions.ResponsibleGamingText))
        {
            return post;
        }

        var text = postingOptions.ResponsibleGamingText.Trim();
        return post.Contains(text, StringComparison.OrdinalIgnoreCase)
            ? post
            : $"{post}{GetAppendSeparator(post)}{text}";
    }

    private string AddAndelsspelLink(string post)
    {
        if (string.IsNullOrWhiteSpace(postingOptions.AndelsspelUrl))
        {
            return post;
        }

        var url = postingOptions.AndelsspelUrl.Trim();

        if (post.Contains(url, StringComparison.OrdinalIgnoreCase))
        {
            return post;
        }

        var callToAction = string.IsNullOrWhiteSpace(postingOptions.AndelsspelCallToAction)
            ? "Vill du vara med p\u00e5 v\u00e5rt andelsspel?"
            : postingOptions.AndelsspelCallToAction.Trim();

        return $"{post}{GetAppendSeparator(post)}{callToAction} {url}";
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

    private string BuildTopByDayFallbackIntro(TopByDayResponse response)
    {
        var form = FirstNonWhiteSpace(response.Form, travoltaTopByDayOptions.Form);
        var track = FirstNonWhiteSpace(response.Track, travoltaTopByDayOptions.Track);

        return $"Dax att tippa? H\u00e4r \u00e4r Travanalys topp {travoltaTopByDayOptions.TopN} f\u00f6r {form} p\u00e5 {track} p\u00e5 travanalys.se.";
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

    private static string FirstNonWhiteSpace(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string FormatLopp(int lopp) =>
        lopp <= 0 ? "?" : lopp.ToString(CultureInfo.InvariantCulture);

    private static string FormatPercent(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool UsesAvdelningLabel(string form)
    {
        var trimmed = form.Trim();
        return trimmed.Length > 1
            && trimmed.StartsWith("V", StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(trimmed[1]);
    }

    private static string GetAppendSeparator(string post) =>
        post.Contains(Environment.NewLine, StringComparison.Ordinal)
            ? $"{Environment.NewLine}{Environment.NewLine}"
            : " ";

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
