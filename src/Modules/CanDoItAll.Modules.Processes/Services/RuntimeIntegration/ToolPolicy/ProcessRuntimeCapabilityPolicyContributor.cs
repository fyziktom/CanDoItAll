using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeCapabilityPolicyContributor : IAgentRuntimeCapabilityPolicyContributor {
    public IReadOnlyList<CapabilityAccessPolicy>? Contribute(AgentRuntimeContextIntent contextIntent,
        AgentToolPolicyCatalog toolPolicies) {
        ArgumentNullException.ThrowIfNull(contextIntent);
        ArgumentNullException.ThrowIfNull(toolPolicies);
        if (!contextIntent.IsGovernedProcessStep) {
            return null;
        }
        var policies = new List<CapabilityAccessPolicy>();
        var templatePath = TemplatePath.Create(
            $"runtime/context/{NormalizeTemplatePathSegment(contextIntent.SourceKind)}/{NormalizeTemplatePathSegment(contextIntent.SourceId)}");
        if (!RuntimeToolProcessIntentPolicy.ShouldExposeConfiguredWorkspaceToolsForProcessIntent(contextIntent)) {
            policies.Add(new CapabilityAccessPolicy(
            [
                new CapabilityAccessRule(
                    CapabilityRuleId.Create("deny-configured-workspace-tools-for-process-step"),
                    CapabilityAccessEffect.Deny,
                    CapabilityAccessScope.ProcessStep,
                    CapabilitySelector.ByTag(CapabilityTag.Create("configured")),
                    "Governed process step did not declare a workspace tool operation requiring configured workspace tools.")
            ]));
        }

        var processPolicy = ProcessAllowedOperationsCapabilityPolicyCompiler.Compile(
            contextIntent.AllowedOperations,
            templatePath,
            "$.allowedOperations");
        if (!processPolicy.ValidationResult.IsValid) {
            var message = string.Join(" ", processPolicy.ValidationResult.Issues.Select(issue => issue.Message));
            throw new InvalidOperationException($"Runtime process operation capability policy is invalid. {message}");
        }

        policies.Add(processPolicy.Policy);
        policies.Add(BuildRuntimeToolOperationRequirementPolicy(contextIntent, templatePath, toolPolicies));
        return policies;
    }

    private static CapabilityAccessPolicy BuildRuntimeToolOperationRequirementPolicy(
        AgentRuntimeContextIntent contextIntent,
        TemplatePath templatePath,
        AgentToolPolicyCatalog toolPolicies) {
        var rules = new List<CapabilityAccessRule>();
        foreach (var capability in toolPolicies.Capabilities) {
            if (!RuntimeToolName.TryCreate(capability.Name, out var runtimeToolName) ||
                RuntimeToolProcessIntentPolicy.IsToolCapabilityAllowedForProcessIntent(capability, contextIntent)) {
                continue;
            }

            rules.Add(new CapabilityAccessRule(
                CapabilityRuleId.Create($"deny-operation-contract-{runtimeToolName.Value.Replace('_', '-')}"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByRuntimeToolName(runtimeToolName),
                $"Runtime tool '{runtimeToolName.Value}' requires an operation contract that is not present in process allowed operations at {templatePath.Value}."));
        }

        return new CapabilityAccessPolicy(rules);
    }

    private static string NormalizeTemplatePathSegment(string? value) {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : string.Concat(value.Trim().Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_'
                    ? char.ToLowerInvariant(character)
                    : '-'));
    }
}
