using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RuntimeCapabilityAccessPolicyBuilder
{
    public static IReadOnlyList<CapabilityAccessPolicy> BuildRuntimeCapabilityAccessPolicies(
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent,
        AgentToolPolicyCatalog? toolPolicies = null,
        IReadOnlyList<IAgentRuntimeCapabilityPolicyContributor>? contextPolicyContributors = null)
    {
        var policies = new List<CapabilityAccessPolicy>
        {
            BuildWorkspaceToolAccessPolicy(workspaceToolAccess)
        };

        if (!contextIntent.WorkspaceToolsEnabled)
        {
            policies.Add(BuildWorkspaceToolsDisabledPolicy());
        }

        if (!contextIntent.BrowserToolsAllowed)
        {
            policies.Add(new CapabilityAccessPolicy(
            [
                new CapabilityAccessRule(
                    CapabilityRuleId.Create("deny-browser-tools-disabled"),
                    CapabilityAccessEffect.Deny,
                    CapabilityAccessScope.RuntimeOverride,
                    CapabilitySelector.ByOperationClassification(CapabilityOperationClassification.BrowserAccess),
                    "Browser proof tools are disabled by execution context.")
            ]));
        }

        var ownerApplied = false;
        foreach (var contributor in contextPolicyContributors ?? []) {
            if (contributor.Contribute(contextIntent, toolPolicies ?? AgentToolPolicyCatalog.BuiltIn) is { } contribution) {
                ownerApplied = true;
                policies.AddRange(contribution);
            }
        }
        if (contextIntent.IsGovernedProcessStep && !ownerApplied) {
            throw new InvalidOperationException("This governed run has no owner runtime-capability policy.");
        }

        if (contextIntent.CapabilityScopeOverride is { IsEmpty: false } scopeOverride)
        {
            policies.AddRange(scopeOverride.Policies);
        }

        return policies;
    }

    private static CapabilityAccessPolicy BuildWorkspaceToolAccessPolicy(AgentWorkspaceToolAccessSettings workspaceToolAccess)
    {
        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);
        var rules = new List<CapabilityAccessRule>();
        foreach (var toolName in ToolContractCatalog.WorkspaceToolNames)
        {
            if (!RuntimeToolName.TryCreate(toolName, out var runtimeToolName) ||
                AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(normalized, toolName))
            {
                continue;
            }

            rules.Add(new CapabilityAccessRule(
                CapabilityRuleId.Create($"deny-runtime-tool-{runtimeToolName.Value.Replace('_', '-')}"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.AgentDefault,
                CapabilitySelector.ByRuntimeToolName(runtimeToolName),
                $"Workspace tool '{runtimeToolName.Value}' is disabled by agent workspace-tool settings."));
        }

        return new CapabilityAccessPolicy(rules);
    }

    private static CapabilityAccessPolicy BuildWorkspaceToolsDisabledPolicy()
    {
        var rules = ToolContractCatalog.WorkspaceToolNames
            .Select(name => RuntimeToolName.TryCreate(name, out var runtimeToolName) ? runtimeToolName : (RuntimeToolName?)null)
            .Where(name => name.HasValue)
            .Select(name => new CapabilityAccessRule(
                CapabilityRuleId.Create($"deny-runtime-tool-{name!.Value.Value.Replace('_', '-')}"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.RuntimeOverride,
                CapabilitySelector.ByRuntimeToolName(name.Value),
                "Workspace tools are disabled by execution context."))
            .ToList();

        return new CapabilityAccessPolicy(rules);
    }

}
