using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory.Tests.Configuration;

public sealed class AgentMemoryConfigurationTests
{
    [Fact]
    public void Codec_roundtrip_preserves_binding_order_and_requirement()
    {
        var zeta = Binding("zeta", "memory.zeta", AgentMemoryProviderRequirement.Required);
        var alpha = Binding("alpha", "memory.alpha", AgentMemoryProviderRequirement.Optional);
        var settings = new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            AllowedProviderInstanceIds = [zeta.ProviderInstanceId, alpha.ProviderInstanceId],
            ProviderBindings = [zeta, alpha]
        };

        var json = AgentMemoryAccessMetadata.Write("{}", settings);
        var roundTripped = AgentMemoryAccessMetadata.Read(json);

        Assert.Equal(["zeta", "alpha"], roundTripped.ProviderBindings.Select(binding => binding.Alias.Value));
        Assert.Equal(AgentMemoryProviderRequirement.Required, roundTripped.ProviderBindings[0].Requirement);
        Assert.Equal(AgentMemoryProviderRequirement.Optional, roundTripped.ProviderBindings[1].Requirement);
        Assert.Contains("\"requirement\":\"Required\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Codec_rejects_unknown_binding_requirement()
    {
        const string json =
            """{"memory":{"invocationMode":"Automatic","providerBindings":[{"alias":"primary","providerInstanceId":"memory.primary","requirement":"BestEffort"}]}}""";

        var exception = Assert.Throws<AgentMemoryConfigurationException>(() => AgentMemoryAccessMetadata.Read(json));

        Assert.Contains("Unknown memory provider requirement", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_preserves_configured_order_when_directives_name_multiple_providers()
    {
        var settings = new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.ExplicitDirective,
            ProviderBindings =
            [
                Binding("alpha", "memory.alpha", AgentMemoryProviderRequirement.Optional),
                Binding("zeta", "memory.zeta", AgentMemoryProviderRequirement.Optional)
            ]
        };

        var plan = AgentMemoryInvocationPlanner.Plan(settings, "/mem:zeta /mem:alpha recall this");

        Assert.Equal(AgentMemoryInvocationPlanDecision.Query, plan.Decision);
        Assert.Equal(["alpha", "zeta"], plan.Providers.Select(binding => binding.Alias.Value));
        Assert.True(plan.TransformRequestMessages);
    }

    [Fact]
    public void Configuration_rejects_two_aliases_for_the_same_provider()
    {
        var settings = new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            ProviderBindings =
            [
                Binding("first", "memory.shared", AgentMemoryProviderRequirement.Optional),
                Binding("second", "memory.shared", AgentMemoryProviderRequirement.Optional)
            ]
        };

        var exception = Assert.Throws<AgentMemoryConfigurationException>(() =>
            AgentMemoryAccessMetadata.Normalize(settings));

        Assert.Contains("bound more than once", exception.Message, StringComparison.Ordinal);
    }

    private static AgentMemoryProviderBindingSetting Binding(
        string alias,
        string providerId,
        AgentMemoryProviderRequirement requirement) =>
        new(
            AgentMemoryProviderAlias.Parse(alias),
            MemoryProviderInstanceId.Parse(providerId),
            IncludeInAutomaticContext: true,
            Requirement: requirement);
}
