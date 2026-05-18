using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Voice;

public static class AgentFrameworkVoiceServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkVoice(this IServiceCollection services)
    {
        services.TryAddScoped<OpenAiVoiceDriver>(serviceProvider =>
            new OpenAiVoiceDriver(
                new HttpClient(),
                serviceProvider.GetRequiredService<CanDoItAll.AgentFramework.Core.IAgentProviderCredentialResolver>()));
        services.TryAddScoped<IAgentVoiceSpeechTextPreprocessor, AgentVoiceSpeechTextPreprocessor>();
        services.TryAddScoped<IAgentVoiceDriverFactory, AgentVoiceDriverFactory>();
        services.TryAddScoped<IAgentVoiceService, AgentVoiceService>();
        return services;
    }
}
