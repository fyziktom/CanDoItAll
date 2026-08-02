using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RuntimeCapabilityAccessPolicyBuilder
{
    public static IReadOnlyList<CapabilityAccessPolicy> BuildRuntimeCapabilityAccessPolicies(
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent)
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

        if (contextIntent.IsGovernedProcessStep)
        {
            var templatePath = TemplatePath.Create(
                $"runtime/context/{NormalizeTemplatePathSegment(contextIntent.SourceKind)}/{NormalizeTemplatePathSegment(contextIntent.SourceId)}");
            if (!RuntimeToolProcessIntentPolicy.ShouldExposeConfiguredWorkspaceToolsForProcessIntent(contextIntent))
            {
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
            if (!processPolicy.ValidationResult.IsValid)
            {
                var message = string.Join(" ", processPolicy.ValidationResult.Issues.Select(issue => issue.Message));
                throw new InvalidOperationException($"Runtime process operation capability policy is invalid. {message}");
            }

            policies.Add(processPolicy.Policy);
            policies.Add(BuildRuntimeToolOperationRequirementPolicy(contextIntent, templatePath));
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

        AddStorageAccessRules(rules, normalized);
        return new CapabilityAccessPolicy(rules);
    }

    private static void AddStorageAccessRules(
        List<CapabilityAccessRule> rules,
        AgentWorkspaceToolAccessSettings normalized)
    {
        if (!normalized.CanReadStorage)
        {
            AddStorageRule(rules, RuntimeStorageToolNames.CatalogList, "read storage");
            AddStorageRule(rules, RuntimeStorageToolNames.Browse, "read storage");
            AddStorageRule(rules, RuntimeStorageToolNames.ReadTextFile, "read storage");
        }

        if (!normalized.CanWriteStorage)
        {
            AddStorageRule(rules, RuntimeStorageToolNames.WriteTextFile, "write storage");
            AddStorageRule(rules, RuntimeStorageToolNames.DeleteObject, "write storage");
        }
    }

    private static void AddStorageRule(
        List<CapabilityAccessRule> rules,
        RuntimeToolName runtimeToolName,
        string permission)
    {
        rules.Add(new CapabilityAccessRule(
            CapabilityRuleId.Create($"deny-runtime-tool-{runtimeToolName.Value.Replace('_', '-')}"),
            CapabilityAccessEffect.Deny,
            CapabilityAccessScope.AgentDefault,
            CapabilitySelector.ByRuntimeToolName(runtimeToolName),
            $"Storage tool '{runtimeToolName.Value}' is disabled because agent settings do not allow {permission}."));
    }

    private static CapabilityAccessPolicy BuildWorkspaceToolsDisabledPolicy()
    {
        var rules = ToolContractCatalog.WorkspaceToolNames
            .Select(name => RuntimeToolName.TryCreate(name, out var runtimeToolName) ? runtimeToolName : (RuntimeToolName?)null)
            .Concat(RuntimeStorageToolNames.All.Select(name => (RuntimeToolName?)name))
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

    private static CapabilityAccessPolicy BuildRuntimeToolOperationRequirementPolicy(
        AgentRuntimeContextIntent contextIntent,
        TemplatePath templatePath)
    {
        var rules = new List<CapabilityAccessRule>();
        foreach (var capability in ToolCapabilityRegistry.Capabilities)
        {
            if (!RuntimeToolName.TryCreate(capability.Name, out var runtimeToolName) ||
                RuntimeToolProcessIntentPolicy.IsToolCapabilityAllowedForProcessIntent(capability, contextIntent))
            {
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

    private static string NormalizeTemplatePathSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : string.Concat(value.Trim().Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_'
                    ? char.ToLowerInvariant(character)
                    : '-'));
    }
}
