using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class AgentWorkspaceToolAccessCapabilityPolicyCompiler
{
    public static AgentWorkspaceToolAccessCapabilityPolicyCompilationResult Compile(
        AgentWorkspaceToolAccessSettings settings,
        IReadOnlyList<CapabilitySeedTemplateDescriptor> capabilities,
        TemplatePath templatePath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(capabilities);

        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(settings);
        var rules = new List<CapabilityAccessRule>();

        foreach (var capability in capabilities)
        {
            if (!string.Equals(capability.Kind, "tool", StringComparison.OrdinalIgnoreCase) ||
                !RuntimeToolName.TryCreate(capability.RuntimeToolName, out var runtimeToolName) ||
                !AgentWorkspaceToolAccessMetadata.TryResolveWorkspaceToolPermission(
                    runtimeToolName.Value,
                    out var permission) ||
                AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(normalized, runtimeToolName.Value))
            {
                continue;
            }

            rules.Add(new CapabilityAccessRule(
                CapabilityRuleId.Create($"deny-runtime-tool-{runtimeToolName.Value.Replace('_', '-')}"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.AgentDefault,
                CapabilitySelector.ByRuntimeToolName(runtimeToolName),
                $"Workspace tool permission '{permission}' is disabled by agent settings at {templatePath.Value}."));
        }

        return new AgentWorkspaceToolAccessCapabilityPolicyCompilationResult(
            new CapabilityAccessPolicy(rules),
            CapabilityValidationResult.Passed);
    }
}

internal sealed record AgentWorkspaceToolAccessCapabilityPolicyCompilationResult(
    CapabilityAccessPolicy Policy,
    CapabilityValidationResult ValidationResult);
