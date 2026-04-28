using Microsoft.Extensions.Hosting;
using TravSocialMediaAgent.Application;
using TravSocialMediaAgent.Infrastructure;

namespace TravSocialMediaAgent.Api.Configuration;

internal static class TravSocialMediaAgentHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);

        await builder.Build().RunAsync();
    }
}
