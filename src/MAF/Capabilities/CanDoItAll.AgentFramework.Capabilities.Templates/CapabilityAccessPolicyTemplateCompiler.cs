using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Capabilities.Templates;

public sealed class CapabilityAccessPolicyTemplateCompiler
{
    public CapabilityAccessPolicyCompilationResult Compile(CapabilityAccessPolicyTemplateDto template, TemplatePath templatePath)
    {
        ArgumentNullException.ThrowIfNull(template);

        var issues = new List<CapabilityValidationIssue>();
        var rules = new List<CapabilityAccessRule>();
        var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var defaultEffect = CapabilityAccessDefaultEffect.Inherit;
        if (!string.IsNullOrWhiteSpace(template.DefaultEffect))
        {
            var readDefaultEffect = ReadEnum<CapabilityAccessDefaultEffect>(
                template.DefaultEffect,
                "$.defaultEffect",
                templatePath,
                issues);
            if (readDefaultEffect is not null)
            {
                defaultEffect = readDefaultEffect.Value;
            }
        }

        for (var index = 0; index < template.Rules.Count; index++)
        {
            var rule = template.Rules[index];
            var id = ReadRuleId(rule.Id, index, templatePath, ruleIds, issues);
            var effect = ReadEnum<CapabilityAccessEffect>(rule.Effect, $"$.rules[{index}].effect", templatePath, issues);
            var scope = ReadEnum<CapabilityAccessScope>(rule.Scope, $"$.rules[{index}].scope", templatePath, issues);
            var selector = ReadSelector(rule.Selector, index, templatePath, issues);

            if (string.IsNullOrWhiteSpace(rule.Reason))
            {
                issues.Add(Issue(
                    $"$.rules[{index}].reason",
                    "Policy rule reason is required.",
                    "Add a short reason so suppressed capability diagnostics are actionable.",
                    templatePath));
            }

            if (effect == CapabilityAccessEffect.Inherit)
            {
                issues.Add(Issue(
                    $"$.rules[{index}].effect",
                    "Policy rule effect 'inherit' is invalid.",
                    "Use inherit only as defaultEffect; concrete rules must use allow, deny, or require.",
                    templatePath));
            }

            if (id is null ||
                effect is null ||
                effect == CapabilityAccessEffect.Inherit ||
                scope is null ||
                selector is null ||
                string.IsNullOrWhiteSpace(rule.Reason))
            {
                continue;
            }

            rules.Add(new CapabilityAccessRule(id.Value, effect.Value, scope.Value, selector, rule.Reason.Trim()));
        }

        return new CapabilityAccessPolicyCompilationResult(
            issues.Count == 0 ? new CapabilityAccessPolicy(rules, defaultEffect) : null,
            new CapabilityValidationResult(issues));
    }

    private static CapabilityRuleId? ReadRuleId(
        string? value,
        int index,
        TemplatePath templatePath,
        HashSet<string> ruleIds,
        List<CapabilityValidationIssue> issues)
    {
        if (!CapabilityRuleId.TryCreate(value, out var id))
        {
            issues.Add(Issue(
                $"$.rules[{index}].id",
                "Policy rule id is invalid.",
                "Use lower kebab-case rule ids.",
                templatePath));
            return null;
        }

        if (!ruleIds.Add(id.Value))
        {
            issues.Add(Issue(
                $"$.rules[{index}].id",
                $"Duplicate policy rule id '{id.Value}'.",
                "Use a stable unique id for every rule.",
                templatePath));
            return null;
        }

        return id;
    }

    private static TEnum? ReadEnum<TEnum>(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (CapabilityText.TryParseEnum<TEnum>(value, out var parsed))
        {
            return parsed;
        }

        issues.Add(Issue(
            fieldPath,
            $"{typeof(TEnum).Name} value '{value ?? "<empty>"}' is invalid.",
            $"Use a known {typeof(TEnum).Name} value.",
            templatePath));
        return null;
    }

    private static CapabilitySelector? ReadSelector(
        CapabilitySelectorTemplateDto? selector,
        int ruleIndex,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (selector is null)
        {
            issues.Add(Issue(
                $"$.rules[{ruleIndex}].selector",
                "Policy selector is required.",
                "Choose a typed selector such as kind, capabilityKey, tag, operationClassification, runtimeToolName, mcpServerKey, mcpToolName, or implementationKey.",
                templatePath));
            return null;
        }

        var kind = ReadEnum<CapabilitySelectorKind>(selector.Kind, $"$.rules[{ruleIndex}].selector.kind", templatePath, issues);
        if (kind is null)
        {
            return null;
        }

        var valuePath = $"$.rules[{ruleIndex}].selector.value";
        return kind.Value switch
        {
            CapabilitySelectorKind.All => CapabilitySelector.All,
            CapabilitySelectorKind.Kind => ReadCapabilityKindSelector(selector.Value, valuePath, templatePath, issues),
            CapabilitySelectorKind.CapabilityKey => ReadCapabilityKeySelector(selector.Value, valuePath, templatePath, issues),
            CapabilitySelectorKind.Tag => ReadTagSelector(selector.Value, valuePath, templatePath, issues),
            CapabilitySelectorKind.OperationClassification => ReadOperationClassificationSelector(selector.Value, valuePath, templatePath, issues),
            CapabilitySelectorKind.RuntimeToolName => ReadRuntimeToolNameSelector(selector.Value, valuePath, templatePath, issues),
            CapabilitySelectorKind.McpServerKey => ReadMcpServerKeySelector(selector.Value, valuePath, templatePath, issues),
            CapabilitySelectorKind.McpToolName => ReadMcpToolNameSelector(selector.ServerKey, selector.Value, ruleIndex, templatePath, issues),
            CapabilitySelectorKind.ImplementationKey => ReadImplementationKeySelector(selector.Value, valuePath, templatePath, issues),
            _ => null
        };
    }

    private static CapabilitySelector? ReadCapabilityKindSelector(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (CapabilityText.TryParseEnum<CapabilityKind>(value, out var kind))
        {
            return CapabilitySelector.ByKind(kind);
        }

        issues.Add(Issue(fieldPath, "Capability kind selector value is invalid.", "Use skill, tool, mcpServer, or mcpTool.", templatePath));
        return null;
    }

    private static CapabilitySelector? ReadCapabilityKeySelector(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (CapabilityKey.TryCreate(value, out var key))
        {
            return CapabilitySelector.ByCapabilityKey(key);
        }

        issues.Add(Issue(fieldPath, "Capability key selector value is invalid.", "Use lower kebab-case capability keys.", templatePath));
        return null;
    }

    private static CapabilitySelector? ReadTagSelector(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (CapabilityTag.TryCreate(value, out var tag))
        {
            return CapabilitySelector.ByTag(tag);
        }

        issues.Add(Issue(fieldPath, "Capability tag selector value is invalid.", "Use lower kebab-case tags.", templatePath));
        return null;
    }

    private static CapabilitySelector? ReadOperationClassificationSelector(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (CapabilityText.TryParseEnum<CapabilityOperationClassification>(value, out var classification))
        {
            return CapabilitySelector.ByOperationClassification(classification);
        }

        issues.Add(Issue(fieldPath, "Operation classification selector value is invalid.", "Use a known operation classification.", templatePath));
        return null;
    }

    private static CapabilitySelector? ReadRuntimeToolNameSelector(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (RuntimeToolName.TryCreate(value, out var name))
        {
            return CapabilitySelector.ByRuntimeToolName(name);
        }

        issues.Add(Issue(fieldPath, "Runtime tool name selector value is invalid.", "Use lower snake_case runtime tool names.", templatePath));
        return null;
    }

    private static CapabilitySelector? ReadMcpServerKeySelector(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (McpServerKey.TryCreate(value, out var key))
        {
            return CapabilitySelector.ByMcpServerKey(key);
        }

        issues.Add(Issue(fieldPath, "MCP server key selector value is invalid.", "Use lower kebab-case MCP server keys.", templatePath));
        return null;
    }

    private static CapabilitySelector? ReadMcpToolNameSelector(
        string? serverKeyValue,
        string? toolNameValue,
        int ruleIndex,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        var hasServer = McpServerKey.TryCreate(serverKeyValue, out var serverKey);
        var hasTool = McpToolName.TryCreate(toolNameValue, out var toolName);
        if (hasServer && hasTool)
        {
            return CapabilitySelector.ByMcpToolName(serverKey, toolName);
        }

        if (!hasServer)
        {
            issues.Add(Issue(
                $"$.rules[{ruleIndex}].selector.serverKey",
                "MCP tool selectors require a valid server key.",
                "Add the MCP server key to disambiguate server-provided tool names.",
                templatePath));
        }

        if (!hasTool)
        {
            issues.Add(Issue(
                $"$.rules[{ruleIndex}].selector.value",
                "MCP tool selector value is invalid.",
                "Use a server-provided MCP tool name with ASCII letters, digits, '.', '_' or '-'.",
                templatePath));
        }

        return null;
    }

    private static CapabilitySelector? ReadImplementationKeySelector(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (ImplementationKey.TryCreate(value, out var key))
        {
            return CapabilitySelector.ByImplementationKey(key);
        }

        issues.Add(Issue(fieldPath, "Implementation key selector value is invalid.", "Use lower ASCII implementation key segments.", templatePath));
        return null;
    }

    private static CapabilityValidationIssue Issue(
        string fieldPath,
        string message,
        string repairHint,
        TemplatePath templatePath)
    {
        return new CapabilityValidationIssue(
            CapabilityDiagnosticCategory.AccessPolicy,
            CapabilityValidationSeverity.Error,
            null,
            null,
            templatePath,
            fieldPath,
            message,
            repairHint);
    }
}
