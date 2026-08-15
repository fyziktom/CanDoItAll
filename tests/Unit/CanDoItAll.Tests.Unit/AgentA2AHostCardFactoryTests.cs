using A2A;
using CanDoItAll.AgentFramework.Hosting;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentA2AHostCardFactoryTests
{
    [Fact]
    public void CreateAgentCardRequiresExplicitHosting()
    {
        var agent = CreateAgent(AgentA2AMetadata.Write(null, new AgentA2ASettings()));
        var factory = new AgentA2AHostCardFactory();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateAgentCard(agent));

        Assert.Contains("not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateAgentCardMapsHostingSettingsToA2ACard()
    {
        var configurationJson = AgentA2AMetadata.Write(
            null,
            new AgentA2ASettings
            {
                Hosting = new AgentA2AHostingSettings
                {
                    Enabled = true,
                    PublicBaseUri = "https://agents.example.test/",
                    PathPrefix = "delivery",
                    SkillName = "Delivery QA",
                    SkillDescription = "Reviews delivery artifacts before QA starts.",
                    Tags = ["qa", "delivery"],
                    ProtocolBindings =
                    [
                        AgentA2AProtocolBindingPreference.JsonRpc
                    ]
                }
            });
        var agent = CreateAgent(configurationJson);
        var factory = new AgentA2AHostCardFactory();

        var card = factory.CreateAgentCard(agent);

        Assert.Equal(agent.Name, card.Name);
        Assert.Single(card.Skills);
        Assert.Equal("Delivery_QA", card.Skills[0].Name);
        Assert.Equal(["delivery", "qa"], card.Skills[0].Tags);
        Assert.Single(card.SupportedInterfaces);
        Assert.Equal("https://agents.example.test/delivery", card.SupportedInterfaces[0].Url);
        Assert.Equal(ProtocolBindingNames.JsonRpc, card.SupportedInterfaces[0].ProtocolBinding);
    }

    private static AgentDefinition CreateAgent(string configurationJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.Parse("1c6afc38-733b-4b6f-b8ac-973f4eba96e8"),
            "Delivery Reviewer",
            "QA Architect",
            "Reviews implementation artifacts and QA readiness.",
            "Review artifacts.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: string.Empty,
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: true,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["software-delivery"],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
