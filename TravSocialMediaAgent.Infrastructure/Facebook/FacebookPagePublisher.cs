using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TravSocialMediaAgent.Application.Abstractions;
using TravSocialMediaAgent.Application.Options;
using TravSocialMediaAgent.Domain.Posts;
using TravSocialMediaAgent.Infrastructure.Options;
using TravSocialMediaAgent.Infrastructure.Shared;

namespace TravSocialMediaAgent.Infrastructure.Facebook;

internal sealed class FacebookPagePublisher(
    HttpClient httpClient,
    FacebookOptions facebookOptions,
    PostingOptions postingOptions,
    ILogger<FacebookPagePublisher> logger
) : ISocialPostPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> PublishAsync(TravSocialPost post, CancellationToken cancellationToken)
    {
        var pageId = ConfigurationValue.Require(
            facebookOptions.PageId,
            "Facebook:PageId or FACEBOOK_PAGE_ID"
        );
        var pageAccessToken = ConfigurationValue.Require(
            facebookOptions.PageAccessToken,
            "Facebook:PageAccessToken or FACEBOOK_PAGE_ACCESS_TOKEN"
        );

        var formFields = new List<KeyValuePair<string, string>>
        {
            new("message", post.Message),
            new("access_token", pageAccessToken),
        };

        if (Uri.TryCreate(postingOptions.TravanalysUrl, UriKind.Absolute, out var linkUri))
        {
            formFields.Add(new("link", linkUri.ToString()));
        }

        using var content = new FormUrlEncodedContent(formFields);
        using var response = await httpClient.PostAsync(
            $"{Uri.EscapeDataString(pageId)}/feed",
            content,
            cancellationToken
        );
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Facebook Graph API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}"
            );
        }

        var postResponse = JsonSerializer.Deserialize<FacebookPostResponse>(
            responseBody,
            JsonOptions
        );
        if (!string.IsNullOrWhiteSpace(postResponse?.Id))
        {
            return postResponse.Id;
        }

        logger.LogWarning(
            "Facebook Graph API accepted the post but did not return a post id. Status: {StatusCode}",
            response.StatusCode
        );
        return response.StatusCode == HttpStatusCode.OK
            ? "unknown"
            : response.StatusCode.ToString();
    }

    private sealed class FacebookPostResponse
    {
        public string? Id { get; set; }
    }
}
