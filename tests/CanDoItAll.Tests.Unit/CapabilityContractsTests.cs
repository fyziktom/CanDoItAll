using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.Tests.Unit;

public sealed class CapabilityContractsTests
{
    [Fact]
    public void SB01_INV_NAMES_001_existing_runtime_tool_names_agent_capability_keys_and_process_operations_are_compatible()
    {
        var invalidRuntimeToolNames = ToolContractCatalog.KnownToolNames
            .Where(name => !RuntimeToolName.TryCreate(name, out _))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var invalidCapabilityKeys = ReadAgentCapabilityKeys()
            .Where(key => !CapabilityKey.TryCreate(key, out _))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        var invalidOperationKeys = ProcessOperationContractNames.AllOperations
            .Where(operation => !ProcessOperationKey.TryCreate(operation, out _))
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(invalidRuntimeToolNames);
        Assert.Empty(invalidCapabilityKeys);
        Assert.Empty(invalidOperationKeys);
    }

    [Theory]
    [InlineData("workspace write file")]
    [InlineData("Workspace-Write-File")]
    [InlineData("workspace_write_file")]
    [InlineData("workspace.write-file")]
    public void SB01_INV_NAMES_002_invalid_capability_keys_fail_before_materialization(string key)
    {
        Assert.False(CapabilityKey.TryCreate(key, out _));
    }

    [Theory]
    [InlineData("workspace write file")]
    [InlineData("WorkspaceWriteFile")]
    [InlineData("workspace-write-file")]
    [InlineData("workspace.write.file")]
    public void SB01_INV_NAMES_003_invalid_runtime_tool_names_fail_before_materialization(string name)
    {
        Assert.False(RuntimeToolName.TryCreate(name, out _));
    }

    [Fact]
    public void SB01_INV_TEMPLATE_001_validator_rejects_raw_secret_surfaces_with_template_path_key_field_and_repair_hint()
    {
        var descriptor = new CapabilityTemplateDescriptorDto
        {
            Kind = "tool",
            Key = "external-audit-tool",
            DisplayName = "External Audit Tool",
            RuntimeToolName = "external_audit",
            StableId = "tool:external-audit-tool:v1",
            ExternalProcess = new ExternalProcessToolTemplateDto
            {
                Command = "audit.exe",
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["API_KEY"] = "raw-secret"
                }
            },
            ExternalHttp = new ExternalHttpToolTemplateDto
            {
                Method = "POST",
                UrlTemplate = "https://example.test/audit",
                Headers = new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer raw-secret"
                }
            }
        };

        var result = new CapabilityTemplateValidator()
            .Validate(descriptor, TemplatePath.Create("Templates/Capabilities/tools/external-audit-tool.json"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Category == CapabilityDiagnosticCategory.SecretBinding &&
            issue.CapabilityKey?.Value == "external-audit-tool" &&
            issue.TemplatePath?.Value == "Templates/Capabilities/tools/external-audit-tool.json" &&
            issue.FieldPath == "$.externalProcess.environmentVariables.API_KEY" &&
            issue.RepairHint.Contains("secret binding", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue =>
            issue.Category == CapabilityDiagnosticCategory.SecretBinding &&
            issue.FieldPath == "$.externalHttp.headers.Authorization");
    }

    [Fact]
    public void SB01_INV_ACCESS_001_deny_wins_over_allow_and_allow_does_not_grant_missing_candidates()
    {
        var validationTool = Tool(
            "workspace-dotnet-test",
            "workspace_dotnet_test",
            new HashSet<CapabilityTag> { CapabilityTag.Create("validation") },
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Validation });
        var mutationTool = Tool(
            "workspace-write-file",
            "workspace_write_file",
            new HashSet<CapabilityTag> { CapabilityTag.Create("mutation") },
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Mutation });
        var missingTool = Tool(
            "workspace-delete-path",
            "workspace_delete_path",
            new HashSet<CapabilityTag> { CapabilityTag.Create("mutation") },
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Mutation });
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("allow-mutation-tools"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByTag(CapabilityTag.Create("mutation")),
                "Existing mutation assignments stay candidates."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-mutation-tools"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(CapabilityOperationClassification.Mutation),
                "Read-only validation step."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("allow-delete-path"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByCapabilityKey(missingTool.Identity.Key),
                "Allow must not grant missing assignments.")
        ]);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [validationTool, mutationTool],
            [],
            [policy],
            "SB01_INV_ACCESS_001"));

        Assert.Contains(result.AllowedCapabilities, item => item.Identity == validationTool.Identity);
        Assert.DoesNotContain(result.AllowedCapabilities, item => item.Identity == mutationTool.Identity);
        Assert.DoesNotContain(result.AllowedCapabilities, item => item.Identity == missingTool.Identity);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Identity == mutationTool.Identity &&
            diagnostic.RuleId == CapabilityRuleId.Create("deny-mutation-tools") &&
            diagnostic.Category == CapabilityDiagnosticCategory.AccessPolicy &&
            diagnostic.CorrelationId == "SB01_INV_ACCESS_001");
    }

    [Fact]
    public void SB01_INV_ACCESS_002_required_capability_denied_by_policy_emits_typed_denied_required_diagnostic()
    {
        var skill = Skill("aspnet-core-skill", new HashSet<CapabilityTag> { CapabilityTag.Create("implementation") });
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-skills"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByKind(CapabilityKind.Skill),
                "This process step must not execute skills.")
        ]);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [skill],
            [skill.Identity],
            [policy],
            "SB01_INV_ACCESS_002"));

        Assert.DoesNotContain(result.AllowedCapabilities, item => item.Identity == skill.Identity);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Identity == skill.Identity &&
            diagnostic.Category == CapabilityDiagnosticCategory.RequiredCapabilityDenied &&
            diagnostic.RepairHint.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SB01_INV_ACCESS_003_new_descriptor_participates_in_tag_policy_without_evaluator_code_changes()
    {
        var externalMcp = new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.McpServer, CapabilityKey.Create("playwright-local-mcp")),
            "Playwright Local MCP",
            "Local browser automation MCP.",
            ImplementationKey.Create("mcp.playwright.local"),
            null,
            McpServerKey.Create("playwright-local-mcp"),
            null,
            new HashSet<CapabilityTag> { CapabilityTag.Create("external") },
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.BrowserAccess },
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.LocalProcessExecution, true, true),
            CapabilityAvailabilityState.Available,
            TemplatePath.Create("Templates/Capabilities/mcps/local/playwright-local-mcp.json"));
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-external"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.WorkflowNode,
                CapabilitySelector.ByTag(CapabilityTag.Create("external")),
                "Workflow node forbids external servers.")
        ]);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [externalMcp],
            [],
            [policy],
            "SB01_INV_ACCESS_003"));

        Assert.Empty(result.AllowedCapabilities);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Identity == externalMcp.Identity &&
            diagnostic.SelectorKind == CapabilitySelectorKind.Tag);
    }

    [Fact]
    public void SB11_INV_ACCESS_001_process_workflow_restrictions_deny_all_capability_families_with_required_diagnostics()
    {
        var skill = Skill("architecture-review-skill", new HashSet<CapabilityTag> { CapabilityTag.Create("process") });
        var tool = Tool(
            "external-audit-tool",
            "external_audit",
            new HashSet<CapabilityTag> { CapabilityTag.Create("external") },
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.ExternalAction });
        var mcpServer = McpServer("browser-mcp");
        var mcpTool = McpTool("search-mcp", "web_search");
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-process-skills"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByKind(CapabilityKind.Skill),
                "Process step forbids skill execution."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-external-action-tools"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(CapabilityOperationClassification.ExternalAction),
                "Process step forbids external actions."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-browser-mcp-server"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.WorkflowNode,
                CapabilitySelector.ByMcpServerKey(McpServerKey.Create("browser-mcp")),
                "Workflow node forbids browser MCP server attachment."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-search-mcp-tool"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.WorkflowNode,
                CapabilitySelector.ByMcpToolName(McpServerKey.Create("search-mcp"), McpToolName.Create("web_search")),
                "Workflow node forbids search MCP tool attachment.")
        ]);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [skill, tool, mcpServer, mcpTool],
            [skill.Identity, tool.Identity, mcpServer.Identity, mcpTool.Identity],
            [policy],
            "SB11_INV_ACCESS_001"));

        Assert.Empty(result.AllowedCapabilities);
        AssertRequiredDenied(result, skill.Identity, CapabilitySelectorKind.Kind, "deny-process-skills");
        AssertRequiredDenied(result, tool.Identity, CapabilitySelectorKind.OperationClassification, "deny-external-action-tools");
        AssertRequiredDenied(result, mcpServer.Identity, CapabilitySelectorKind.McpServerKey, "deny-browser-mcp-server");
        AssertRequiredDenied(result, mcpTool.Identity, CapabilitySelectorKind.McpToolName, "deny-search-mcp-tool");
    }

    [Fact]
    public void SB01_INV_POLICY_001_policy_template_compiler_rejects_invalid_selectors_and_duplicate_rule_ids()
    {
        var template = new CapabilityAccessPolicyTemplateDto
        {
            Rules =
            [
                new CapabilityAccessRuleTemplateDto
                {
                    Id = "deny-one",
                    Effect = "deny",
                    Scope = "processStep",
                    Selector = new CapabilitySelectorTemplateDto
                    {
                        Kind = "capabilityKey",
                        Value = "workspace write file"
                    },
                    Reason = "Bad key must fail."
                },
                new CapabilityAccessRuleTemplateDto
                {
                    Id = "deny-one",
                    Effect = "deny",
                    Scope = "processStep",
                    Selector = new CapabilitySelectorTemplateDto
                    {
                        Kind = "kind",
                        Value = "tool"
                    },
                    Reason = "Duplicate id must fail."
                }
            ]
        };

        var result = new CapabilityAccessPolicyTemplateCompiler()
            .Compile(template, TemplatePath.Create("Templates/Capabilities/policies/capability-access-policy.json"));

        Assert.False(result.ValidationResult.IsValid);
        Assert.Contains(result.ValidationResult.Issues, issue =>
            issue.Category == CapabilityDiagnosticCategory.AccessPolicy &&
            issue.FieldPath == "$.rules[0].selector.value" &&
            issue.RepairHint.Contains("capability key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationResult.Issues, issue =>
            issue.Category == CapabilityDiagnosticCategory.AccessPolicy &&
            issue.FieldPath == "$.rules[1].id" &&
            issue.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    private static CapabilityExposureDescriptor Tool(
        string key,
        string runtimeToolName,
        IReadOnlySet<CapabilityTag> tags,
        IReadOnlySet<CapabilityOperationClassification> classifications)
    {
        return new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.Tool, CapabilityKey.Create(key)),
            key,
            string.Empty,
            ImplementationKey.Create(key.Replace('-', '.')),
            RuntimeToolName.Create(runtimeToolName),
            null,
            null,
            tags,
            classifications,
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.WorkspaceRead, false, false),
            CapabilityAvailabilityState.Available,
            TemplatePath.Create($"Templates/Capabilities/tools/{key}.json"));
    }

    private static CapabilityExposureDescriptor Skill(string key, IReadOnlySet<CapabilityTag> tags)
    {
        return new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.Skill, CapabilityKey.Create(key)),
            key,
            string.Empty,
            ImplementationKey.Create(key.Replace('-', '.')),
            null,
            null,
            null,
            tags,
            new HashSet<CapabilityOperationClassification>(),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false),
            CapabilityAvailabilityState.Available,
            TemplatePath.Create($"Templates/Capabilities/skills/{key}.json"));
    }

    private static CapabilityExposureDescriptor McpServer(string key)
    {
        return new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.McpServer, CapabilityKey.Create(key)),
            key,
            string.Empty,
            ImplementationKey.Create($"mcp.{key.Replace('-', '.')}"),
            null,
            McpServerKey.Create(key),
            null,
            new HashSet<CapabilityTag> { CapabilityTag.Create("mcp") },
            new HashSet<CapabilityOperationClassification>
            {
                CapabilityOperationClassification.BrowserAccess,
                CapabilityOperationClassification.McpTool
            },
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.LocalProcessExecution, true, true),
            CapabilityAvailabilityState.Available,
            TemplatePath.Create($"Templates/Capabilities/mcps/{key}.json"));
    }

    private static CapabilityExposureDescriptor McpTool(string serverKey, string toolName)
    {
        return new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.McpTool, CapabilityKey.Create($"{serverKey}-{toolName.Replace('_', '-')}")),
            $"{serverKey} {toolName}",
            string.Empty,
            ImplementationKey.Create($"mcp.{serverKey.Replace('-', '.')}.{toolName.Replace('_', '.')}"),
            null,
            McpServerKey.Create(serverKey),
            McpToolName.Create(toolName),
            new HashSet<CapabilityTag> { CapabilityTag.Create("mcp-tool") },
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.McpTool },
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.McpTool, true, false),
            CapabilityAvailabilityState.Available,
            TemplatePath.Create($"Templates/Capabilities/mcps/{serverKey}.json"));
    }

    private static void AssertRequiredDenied(
        CapabilityAccessEvaluationResult result,
        CapabilityIdentity identity,
        CapabilitySelectorKind selectorKind,
        string ruleId)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Identity == identity &&
            diagnostic.Category == CapabilityDiagnosticCategory.RequiredCapabilityDenied &&
            diagnostic.SelectorKind == selectorKind &&
            diagnostic.RuleId == CapabilityRuleId.Create(ruleId) &&
            diagnostic.RepairHint.Contains("required", StringComparison.OrdinalIgnoreCase) &&
            diagnostic.CorrelationId == "SB11_INV_ACCESS_001");
    }

    private static IReadOnlyList<string> ReadAgentCapabilityKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles("Templates/Agents", "skills.json", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("capabilityKeys", out var capabilityKeys) ||
                capabilityKeys.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var key in capabilityKeys.EnumerateArray())
            {
                if (key.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(key.GetString()))
                {
                    keys.Add(key.GetString()!);
                }
            }
        }

        return keys.ToArray();
    }
}
