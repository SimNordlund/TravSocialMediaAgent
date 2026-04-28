using TravSocialMediaAgent.Domain.Posts;

namespace TravSocialMediaAgent.Application.Abstractions;

public interface IPostContentGenerator
{
    Task<TravSocialPost> GenerateAsync(CancellationToken cancellationToken);
}
