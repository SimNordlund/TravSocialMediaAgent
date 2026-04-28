using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TravSocialMediaAgent.Application.Abstractions;
using TravSocialMediaAgent.Application.Options;

namespace TravSocialMediaAgent.Application.Workers;

internal sealed class TravPostingWorker(
    IPostContentGenerator postContentGenerator,
    ISocialPostPublisher socialPostPublisher,
    PostingOptions postingOptions,
    ILogger<TravPostingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (postingOptions.DryRun)
        {
            logger.LogWarning("Posting:DryRun is enabled. Generated posts will be logged but not published to Facebook.");
        }

        var firstRun = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = firstRun && postingOptions.PostOnStartup
                ? TimeSpan.Zero
                : GetRandomDelay();

            firstRun = false;

            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("Next Travanalys post is scheduled in {Delay}.", delay);
                await Task.Delay(delay, stoppingToken);
            }

            var published = await TryPublishOnceAsync(stoppingToken);

            if (!published && postingOptions.RetryDelay > TimeSpan.Zero && !stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Waiting {RetryDelay} before continuing after failed publish attempt.", postingOptions.RetryDelay);
                await Task.Delay(postingOptions.RetryDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> TryPublishOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var post = await postContentGenerator.GenerateAsync(cancellationToken);

            if (postingOptions.DryRun)
            {
                logger.LogInformation("Dry-run Travanalys Facebook post: {Post}", post.Message);
                return true;
            }

            var postId = await socialPostPublisher.PublishAsync(post, cancellationToken);
            logger.LogInformation("Published Travanalys Facebook post with id {PostId}.", postId);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to generate or publish the Travanalys Facebook post.");
            return false;
        }
    }

    private TimeSpan GetRandomDelay()
    {
        var minimumMilliseconds = (long)postingOptions.MinimumDelay.TotalMilliseconds;
        var maximumMilliseconds = (long)postingOptions.MaximumDelay.TotalMilliseconds;

        if (minimumMilliseconds == maximumMilliseconds)
        {
            return postingOptions.MinimumDelay;
        }

        return TimeSpan.FromMilliseconds(Random.Shared.NextInt64(minimumMilliseconds, maximumMilliseconds + 1));
    }
}
