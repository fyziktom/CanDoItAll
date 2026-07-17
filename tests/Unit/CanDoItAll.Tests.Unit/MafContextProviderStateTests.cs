using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafContextProviderStateTests
{
    [Fact]
    public void Static_message_context_providers_are_stateless_when_composed()
    {
        AIContextProvider[] providers =
        [
            new StaticMessageContextProvider(new ChatMessage(ChatRole.System, "Configured context")),
            new StaticMessageContextProvider(new ChatMessage(ChatRole.User, "Transient UI context"))
        ];

        Assert.All(providers, provider => Assert.Empty(provider.StateKeys));
        Assert.Empty(FindDuplicateStateKeys(providers));
    }

    [Fact]
    public void Rag_context_providers_use_capability_scoped_state_keys()
    {
        var firstCapabilityId = Guid.NewGuid();
        var secondCapabilityId = Guid.NewGuid();
        var builder = new ContextCapabilityBuilder(Path.GetTempPath());
        var state = new RuntimeCapabilityState();

        builder.AddRagProvider(
            state,
            CreateRagCapability(firstCapabilityId, "first-rag"),
            new AgentRuntimeConfiguration());
        builder.AddRagProvider(
            state,
            CreateRagCapability(secondCapabilityId, "second-rag"),
            new AgentRuntimeConfiguration());

        Assert.Equal(
            [
                $"CanDoItAll.Rag.{firstCapabilityId:N}",
                $"CanDoItAll.Rag.{secondCapabilityId:N}"
            ],
            state.ContextProviders.SelectMany(provider => provider.StateKeys));
        Assert.Empty(FindDuplicateStateKeys(state.ContextProviders));
    }

    private static CapabilityCatalogItem CreateRagCapability(
        Guid capabilityId,
        string key)
    {
        return new CapabilityCatalogItem(
            capabilityId,
            CapabilityKind.Rag,
            key,
            key,
            "Test RAG capability",
            string.Empty,
            "{}",
            CapabilityProofStatus.Verified,
            string.Empty,
            DateTimeOffset.UtcNow,
            IsBuiltIn: false);
    }

    private static IReadOnlyList<string> FindDuplicateStateKeys(
        IEnumerable<AIContextProvider> providers)
    {
        return providers
            .SelectMany(provider => provider.StateKeys)
            .GroupBy(stateKey => stateKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
    }
}
