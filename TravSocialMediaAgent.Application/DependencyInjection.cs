using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TravSocialMediaAgent.Application.Options;
using TravSocialMediaAgent.Application.Workers;

namespace TravSocialMediaAgent.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var postingOptions = configuration.GetSection(PostingOptions.SectionName).Get<PostingOptions>() ?? new PostingOptions();
        postingOptions.Validate();

        services.AddSingleton(postingOptions);
        services.AddHostedService<TravPostingWorker>();

        return services;
    }
}
