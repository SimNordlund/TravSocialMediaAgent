using TravSocialMediaAgent.Domain.Posts;

namespace TravSocialMediaAgent.Application.Abstractions;

public interface ISocialPostPublisher
{
    Task<string> PublishAsync(TravSocialPost post, CancellationToken cancellationToken);
}
