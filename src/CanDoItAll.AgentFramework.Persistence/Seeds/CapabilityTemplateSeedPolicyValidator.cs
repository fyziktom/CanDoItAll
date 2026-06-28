using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Templates;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class CapabilityTemplateSeedPolicyValidator
{
    public static IReadOnlyList<CapabilityValidationIssue> ValidatePolicyReferences(
        CapabilityAccessPolicyTemplateDto template,
        TemplatePath templatePath,
        IReadOnlyList<CapabilitySeedTemplateDescriptor> capabilities)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(capabilities);

        var issues = new List<CapabilityValidationIssue>();
        var capabilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runtimeToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var implementationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mcpServerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mcpToolsByServer = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in capabilities)
        {
            if (CapabilityKey.TryCreate(capability.Key, out var capabilityKey))
            {
                capabilityKeys.Add(capabilityKey.Value);
            }

            if (RuntimeToolName.TryCreate(capability.RuntimeToolName, out var runtimeToolName))
            {
                runtimeToolNames.Add(runtimeToolName.Value);
            }

            if (ImplementationKey.TryCreate(capability.ImplementationKey, out var implementationKey))
            {
                implementationKeys.Add(implementationKey.Value);
            }

            if (!McpServerKey.TryCreate(capability.McpServerKey, out var mcpServerKey))
            {
                continue;
            }

            mcpServerKeys.Add(mcpServerKey.Value);
            if (!mcpToolsByServer.TryGetValue(mcpServerKey.Value, out var allowedTools))
            {
                allowedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                mcpToolsByServer.Add(mcpServerKey.Value, allowedTools);
            }

            foreach (var toolName in capability.McpTransport?.AllowedTools ?? [])
            {
                allowedTools.Add(toolName);
            }
        }

        for (var index = 0; index < template.Rules.Count; index++)
        {
            var rule = template.Rules[index];
            var selector = template.Rules[index].Selector;
            if (selector is null)
            {
                continue;
            }

            if (string.Equals(selector.Kind, "capabilityKey", StringComparison.OrdinalIgnoreCase) &&
                CapabilityKey.TryCreate(selector.Value, out var key) &&
                !capabilityKeys.Contains(key.Value))
            {
                issues.Add(Issue(
                    CapabilityKind.Tool,
                    key,
                    templatePath,
                    $"$.rules[{index}].selector.value",
                    $"Capability key selector '{key.Value}' in {DescribeRule(rule)} does not resolve to a template-backed capability.",
                    "Choose a capability key from Templates/Capabilities before granting or denying access."));
            }

            if (string.Equals(selector.Kind, "runtimeToolName", StringComparison.OrdinalIgnoreCase) &&
                RuntimeToolName.TryCreate(selector.Value, out var runtimeToolName) &&
                !runtimeToolNames.Contains(runtimeToolName.Value))
            {
                issues.Add(Issue(
                    CapabilityKind.Tool,
                    null,
                    templatePath,
                    $"$.rules[{index}].selector.value",
                    $"Runtime tool selector '{runtimeToolName.Value}' in {DescribeRule(rule)} does not resolve to a template-backed tool.",
                    "Choose a runtime tool name declared by Templates/Capabilities tool templates."));
            }

            if (string.Equals(selector.Kind, "implementationKey", StringComparison.OrdinalIgnoreCase) &&
                ImplementationKey.TryCreate(selector.Value, out var implementationKey) &&
                !implementationKeys.Contains(implementationKey.Value))
            {
                issues.Add(Issue(
                    null,
                    null,
                    templatePath,
                    $"$.rules[{index}].selector.value",
                    $"Implementation key selector '{implementationKey.Value}' in {DescribeRule(rule)} does not resolve to a template-backed implementation.",
                    "Choose an implementation key declared by a capability template or remove the implementation selector."));
            }

            if (string.Equals(selector.Kind, "mcpServerKey", StringComparison.OrdinalIgnoreCase) &&
                McpServerKey.TryCreate(selector.Value, out var selectedServerKey) &&
                !mcpServerKeys.Contains(selectedServerKey.Value))
            {
                issues.Add(Issue(
                    CapabilityKind.McpServer,
                    null,
                    templatePath,
                    $"$.rules[{index}].selector.value",
                    $"MCP server selector '{selectedServerKey.Value}' in {DescribeRule(rule)} does not resolve to a template-backed MCP server.",
                    "Choose an MCP server key declared by Templates/Capabilities MCP templates."));
            }

            if (string.Equals(selector.Kind, "mcpToolName", StringComparison.OrdinalIgnoreCase) &&
                McpServerKey.TryCreate(selector.ServerKey, out var serverKey) &&
                McpToolName.TryCreate(selector.Value, out var toolName) &&
                (!mcpToolsByServer.TryGetValue(serverKey.Value, out var allowedTools) || !allowedTools.Contains(toolName.Value)))
            {
                issues.Add(new CapabilityValidationIssue(
                    CapabilityDiagnosticCategory.AccessPolicy,
                    CapabilityValidationSeverity.Error,
                    CapabilityKind.McpTool,
                    null,
                    templatePath,
                    $"$.rules[{index}].selector.value",
                    $"MCP tool selector '{toolName.Value}' in {DescribeRule(rule)} does not resolve under server '{serverKey.Value}'.",
                    "Use an allowed MCP tool declared on the selected server template."));
            }
        }

        return issues;
    }

    private static CapabilityValidationIssue Issue(
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        string fieldPath,
        string message,
        string repairHint)
    {
        return new CapabilityValidationIssue(
            CapabilityDiagnosticCategory.AccessPolicy,
            CapabilityValidationSeverity.Error,
            kind,
            key,
            templatePath,
            fieldPath,
            message,
            repairHint);
    }

    private static string DescribeRule(CapabilityAccessRuleTemplateDto rule)
        => $"effect '{rule.Effect ?? "<empty>"}' and scope '{rule.Scope ?? "<empty>"}'";
}
