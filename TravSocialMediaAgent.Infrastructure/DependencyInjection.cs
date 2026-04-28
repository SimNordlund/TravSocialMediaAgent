using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TravSocialMediaAgent.Application.Abstractions;
using TravSocialMediaAgent.Infrastructure.Facebook;
using TravSocialMediaAgent.Infrastructure.OpenAI;
using TravSocialMediaAgent.Infrastructure.Options;
using TravSocialMediaAgent.Infrastructure.Shared;

namespace TravSocialMediaAgent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var aiOptions = LoadAiOptions(configuration);
        var facebookOptions = LoadFacebookOptions(configuration);

        services.AddSingleton(aiOptions);
        services.AddSingleton(facebookOptions);

        services.AddSingleton<OpenAiChatClientFactory>();
        services.AddSingleton<IChatClient>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenAiChatClientFactory>().Create());
        services.AddSingleton<IPostContentGenerator, OpenAiPostContentGenerator>();

        services.AddHttpClient<ISocialPostPublisher, FacebookPagePublisher>((serviceProvider, httpClient) =>
        {
            var facebook = serviceProvider.GetRequiredService<FacebookOptions>();
            var graphApiVersion = ConfigurationValue.Require(facebook.GraphApiVersion, "Facebook:GraphApiVersion").Trim('/');

            httpClient.BaseAddress = new Uri($"https://graph.facebook.com/{graphApiVersion}/");
        });

        return services;
    }

    private static AiOptions LoadAiOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        options.ApiKey = ConfigurationValue.FirstNonWhiteSpace(
            options.ApiKey,
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        return options;
    }

    private static FacebookOptions LoadFacebookOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(FacebookOptions.SectionName).Get<FacebookOptions>() ?? new FacebookOptions();
        options.PageId = ConfigurationValue.FirstNonWhiteSpace(
            options.PageId,
            Environment.GetEnvironmentVariable("FACEBOOK_PAGE_ID"));
        options.PageAccessToken = ConfigurationValue.FirstNonWhiteSpace(
            options.PageAccessToken,
            Environment.GetEnvironmentVariable("FACEBOOK_PAGE_ACCESS_TOKEN"));

        return options;
    }
}
