using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentProviderPresentationMapperTests
{
    [Fact]
    public void Provider_profile_maps_to_neutral_presentation_and_round_trips_its_identifier()
    {
        var provider = CreateProvider();

        var presentation = AgentProviderPresentationMapper.Map(provider);

        Assert.Equal(provider.Id.ToString("D"), presentation.Key.Value);
        Assert.Equal(provider.Name, presentation.Name);
        Assert.Equal(provider.IsEnabled, presentation.IsEnabled);
        Assert.Equal(provider.DefaultModel, presentation.DefaultModel);
        Assert.Equal(provider.SuggestedModels, presentation.SuggestedModels);
        Assert.Equal(provider.Id, AgentProviderPresentationMapper.ToProviderId(presentation.Key));
        Assert.Null(AgentProviderPresentationMapper.ToProviderId(null));
    }

    [Fact]
    public void Invalid_provider_presentation_key_fails_explicitly()
    {
        var key = new ConversationPresentationKey("not-a-provider-id");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderPresentationMapper.ToProviderId(key));

        Assert.Contains("canonical provider identifier", exception.Message, StringComparison.Ordinal);
    }

    private static ProviderProfile CreateProvider()
        => new(
            Guid.NewGuid(),
            "Mapped provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-5-mini", "gpt-5.4"]);
}
