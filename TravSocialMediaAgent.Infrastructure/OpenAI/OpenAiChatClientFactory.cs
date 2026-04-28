using Microsoft.Extensions.AI;
using TravSocialMediaAgent.Infrastructure.Options;
using TravSocialMediaAgent.Infrastructure.Shared;
using OpenAiSdkChatClient = global::OpenAI.Chat.ChatClient;

namespace TravSocialMediaAgent.Infrastructure.OpenAI;

internal sealed class OpenAiChatClientFactory(AiOptions aiOptions)
{
    public IChatClient Create()
    {
        var apiKey = ConfigurationValue.Require(aiOptions.ApiKey, "Ai:ApiKey or OPENAI_API_KEY");
        var modelId = ConfigurationValue.Require(aiOptions.ModelId, "Ai:ModelId");

        IChatClient openAiClient = new OpenAiSdkChatClient(modelId, apiKey).AsIChatClient();

        return new ChatClientBuilder(openAiClient)
            .ConfigureOptions(options =>
            {
                options.ModelId ??= modelId;
                options.Temperature ??= aiOptions.Temperature;
                options.TopP ??= aiOptions.TopP;
                options.MaxOutputTokens ??= aiOptions.MaxOutputTokens;
            })
            .Build();
    }
}
