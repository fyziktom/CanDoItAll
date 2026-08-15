using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// Failing-first characterization: the canonical execution authority resolved
/// at turn admission must reach runtime enforcement as one immutable
/// governance snapshot. Capability planning (tool provider context) and the
/// invocation policy context must consume that snapshot instead of re-deriving
/// permissions from UI access entries, agent configuration, or default-true
/// behavior. These structural assertions fail until the governance snapshot
/// contract exists on both enforcement inputs; behavioral enforcement tests
/// accompany the production change itself.
/// </summary>
public sealed class ExecutionGovernanceSnapshotContractBaselineTests
{
    [Fact]
    public void Tool_provider_context_carries_an_execution_governance_snapshot()
    {
        var governanceProperty = typeof(AgentRuntimeToolProviderContext)
            .GetProperties()
            .FirstOrDefault(property =>
                property.Name.Contains("Governance", StringComparison.Ordinal));

        Assert.True(
            governanceProperty is not null,
            "Tool providers must receive the admitted execution governance snapshot instead of re-deriving permissions.");
    }

    [Fact]
    public void Tool_invocation_policy_context_carries_authority_derived_grants()
    {
        var contextProperties = typeof(ToolInvocationPolicyContext)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.True(
            contextProperties.Any(name => name.Contains("Governance", StringComparison.Ordinal)) ||
            contextProperties.Any(name => name.Contains("MutationAllowed", StringComparison.Ordinal)),
            "The invocation policy must evaluate the admitted authority grants, not only process and configuration facts.");
    }
}
