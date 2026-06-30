using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using SeedCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Unit;

public sealed class CapabilityTemplateSeedHardeningCheckpointTests
{
    [Fact]
    public void SB07_INV_PARITY_001_default_pack_preserves_behavior_critical_fields()
    {
        var pack = new CapabilityTemplatePackLoader().Load();
        var materialized = CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities(pack)
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var template in pack.Capabilities)
        {
            if (!materialized.TryGetValue(template.Key, out var capability))
            {
                failures.Add($"{template.Key}: missing materialized capability");
                continue;
            }

            if (capability.Id != CreateStableGuid(template.StableGuidKey))
            {
                failures.Add($"{template.Key}: stable id drift");
            }

            if (capability.Kind != ParseSeedKind(template.Kind) ||
                capability.Name != template.DisplayName ||
                capability.Description != template.Description)
            {
                failures.Add($"{template.Key}: display metadata drift");
            }

            if (!string.Equals(template.SkillSource, "file", StringComparison.OrdinalIgnoreCase) &&
                capability.Kind != SeedCapabilityKind.AiContext &&
                !string.Equals(capability.EndpointOrPath, template.EndpointOrPath, StringComparison.Ordinal))
            {
                failures.Add($"{template.Key}: endpoint drift");
            }

            if (template.Kind.Equals("tool", StringComparison.OrdinalIgnoreCase))
            {
                AssertToolConfigurationParity(template, capability, failures, pack.Manifest.SeedVersion);
            }

            if (template.Kind.Equals("mcp-server", StringComparison.OrdinalIgnoreCase))
            {
                AssertMcpConfigurationParity(template, capability, failures);
            }

            if (template.Kind.Equals("tool", StringComparison.OrdinalIgnoreCase) ||
                template.Kind.Equals("mcp-server", StringComparison.OrdinalIgnoreCase))
            {
                Assert.NotEmpty(template.OperationClassifications);
            }
        }

        var policy = Assert.Single(pack.Policies, item => item.Key == "default-compatibility-policy").Policy;
        Assert.Contains(policy.Rules, rule =>
            rule.Effect == CapabilityAccessEffect.Deny &&
            rule.Selector.OperationClassification == CapabilityOperationClassification.Mutation);
        Assert.Contains(policy.Rules, rule =>
            rule.Effect == CapabilityAccessEffect.Deny &&
            rule.Selector.OperationClassification == CapabilityOperationClassification.ExternalAction);
        Assert.Empty(failures);
    }

    [Fact]
    public void SB07_INV_TEMPLATE_001_invalid_pack_reports_structured_diagnostics_without_seed_fallback()
    {
        using var pack = TemporaryCapabilityTemplatePack.Create(
            new Dictionary<string, string>
            {
                ["capabilities/invalid.json"] =
                    """
                    {
                      "capabilities": [
                        {
                          "kind": "tool",
                          "key": "workspace-read-file",
                          "displayName": "Workspace Read File",
                          "description": "Valid first descriptor.",
                          "stableId": "tool:workspace-read-file:v1",
                          "stableGuidKey": "capabilities/workspace-read-file",
                          "endpointOrPath": "sandbox://workspace-read-file",
                          "runtimeToolName": "workspace_read_file"
                        },
                        {
                          "kind": "tool",
                          "key": "workspace-read-file",
                          "displayName": "Workspace Read File Duplicate",
                          "description": "Duplicate with multiple repairable failures.",
                          "stableId": "tool:workspace-read-file:v1",
                          "stableGuidKey": "capabilities/workspace-read-file-duplicate",
                          "endpointOrPath": "sandbox://workspace-read-file",
                          "runtimeToolName": "workspace read file",
                          "externalHttp": {
                            "method": "POST",
                            "urlTemplate": "https://example.test/call",
                            "headers": {
                              "Authorization": "Bearer raw-secret"
                            }
                          }
                        },
                        {
                          "kind": "mcp-server",
                          "key": "playwright-local-mcp",
                          "displayName": "Playwright Local MCP",
                          "description": "MCP with missing allowlist.",
                          "stableId": "mcp:playwright-local-mcp:v1",
                          "stableGuidKey": "capabilities/playwright-local-mcp",
                          "endpointOrPath": "npx",
                          "mcpServerKey": "playwright-local",
                          "mcpTransport": {
                            "transport": "local-stdio",
                            "command": "npx",
                            "allowedTools": []
                          }
                        }
                      ]
                    }
                    """
            },
            new Dictionary<string, string>
            {
                ["policies/invalid-policy.json"] =
                    """
                    {
                      "defaultEffect": "silentlyAllow",
                      "rules": [
                        {
                          "id": "bad-effect",
                          "effect": "grant",
                          "scope": "processStep",
                          "selector": {
                            "kind": "runtimeToolName",
                            "value": "missing_tool"
                          },
                          "reason": "Invalid effect must fail."
                        },
                        {
                          "id": "ambiguous-mcp-tool",
                          "effect": "deny",
                          "scope": "processStep",
                          "selector": {
                            "kind": "mcpToolName",
                            "value": "browser_snapshot"
                          },
                          "reason": "MCP tool selectors need a server."
                        },
                        {
                          "id": "unknown-implementation",
                          "effect": "deny",
                          "scope": "processStep",
                          "selector": {
                            "kind": "implementationKey",
                            "value": "missing.impl"
                          },
                          "reason": "Unknown implementation keys must fail."
                        },
                        {
                          "id": "allow-missing-capability",
                          "effect": "allow",
                          "scope": "processStep",
                          "selector": {
                            "kind": "capabilityKey",
                            "value": "missing-capability"
                          },
                          "reason": "Allow cannot grant a missing capability."
                        }
                      ]
                    }
                    """
            },
            missingCapabilityRefs: ["capabilities/missing.json"]);

        var exception = Assert.Throws<CapabilityTemplatePackValidationException>(() =>
            SandboxWorkspaceSeedBuilder.Build(pack.RootPath));
        var issues = exception.Issues;

        AssertIssue(issues, CapabilityDiagnosticCategory.TemplateValidation, "capabilities/missing.json", "$.capabilityFiles");
        AssertIssue(issues, CapabilityDiagnosticCategory.TemplateValidation, "capabilities/invalid.json", "$.capabilities[1].key", "workspace-read-file");
        AssertIssue(issues, CapabilityDiagnosticCategory.SecretBinding, "capabilities/invalid.json", "$.externalHttp.headers.Authorization", "workspace-read-file");
        AssertIssue(issues, CapabilityDiagnosticCategory.TemplateValidation, "capabilities/invalid.json", "$.mcpTransport.allowedTools", "playwright-local-mcp");
        AssertIssue(issues, CapabilityDiagnosticCategory.AccessPolicy, "policies/invalid-policy.json", "$.defaultEffect");
        AssertIssue(issues, CapabilityDiagnosticCategory.AccessPolicy, "policies/invalid-policy.json", "$.rules[0].effect");
        AssertIssue(issues, CapabilityDiagnosticCategory.AccessPolicy, "policies/invalid-policy.json", "$.rules[1].selector.serverKey");
        AssertIssue(issues, CapabilityDiagnosticCategory.AccessPolicy, "policies/invalid-policy.json", "$.rules[2].selector.value");
        AssertIssue(issues, CapabilityDiagnosticCategory.AccessPolicy, "policies/invalid-policy.json", "$.rules[3].selector.value", "missing-capability");
    }

    [Fact]
    public void SB07_INV_POLICY_001_allowed_operations_compile_to_behavior_equivalent_denies()
    {
        var candidates = new[]
        {
            Candidate("workspace-write-file", "workspace_write_file", CapabilityOperationClassification.Write, CapabilityOperationClassification.Mutation),
            Candidate("workspace-dotnet-build", "workspace_dotnet_build", CapabilityOperationClassification.Validation, CapabilityOperationClassification.ScriptExecution),
            Candidate("workspace-pwsh-run-script", "workspace_pwsh_run_script", CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution),
            Candidate("playwright-local-mcp-browser-take-screenshot", null, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.McpTool)
        };

        var validationPolicy = ProcessAllowedOperationsCapabilityPolicyCompiler.Compile(
            [ProcessOperationContractNames.RunValidation],
            TemplatePath.Create("Templates/Processes/example/definition.json"),
            "$.steps[0].allowedOperations").Policy;
        AssertAllowedKeys(
            validationPolicy,
            candidates,
            ["workspace-dotnet-build"],
            "SB07_INV_POLICY_001_validation");

        var mutationPolicy = ProcessAllowedOperationsCapabilityPolicyCompiler.Compile(
            [ProcessOperationContractNames.MutateProductTarget],
            TemplatePath.Create("Templates/Processes/example/definition.json"),
            "$.steps[1].allowedOperations").Policy;
        AssertAllowedKeys(
            mutationPolicy,
            candidates,
            ["workspace-write-file"],
            "SB07_INV_POLICY_001_mutation");

        var proofPolicy = ProcessAllowedOperationsCapabilityPolicyCompiler.Compile(
            [ProcessOperationContractNames.CaptureRuntimeProof],
            TemplatePath.Create("Templates/Processes/example/definition.json"),
            "$.steps[2].allowedOperations").Policy;
        AssertAllowedKeys(
            proofPolicy,
            candidates,
            ["playwright-local-mcp-browser-take-screenshot"],
            "SB07_INV_POLICY_001_proof");
    }

    [Fact]
    public void SB07_INV_POLICY_002_workspace_tool_flags_compile_to_typed_runtime_tool_denies()
    {
        var pack = new CapabilityTemplatePackLoader().Load();
        var templates = pack.Capabilities
            .Where(template => template.Kind.Equals("tool", StringComparison.OrdinalIgnoreCase))
            .Where(template => !string.IsNullOrWhiteSpace(template.RuntimeToolName))
            .ToArray();
        var settings = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.QualityValidation);

        var result = AgentWorkspaceToolAccessCapabilityPolicyCompiler.Compile(
            settings,
            templates,
            TemplatePath.Create("Templates/Agents/teams/dotnet-delivery/members/delivery-qa-observer/settings.json"));
        var candidates = templates
            .Select(template => Candidate(template.Key, template.RuntimeToolName, ParseClassifications(template.OperationClassifications)))
            .ToArray();
        var allowed = Evaluate(result.Policy, candidates, "SB07_INV_POLICY_002")
            .Select(item => item.RuntimeToolName?.Value)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            var expected = AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(settings, template.RuntimeToolName);
            Assert.Equal(expected, allowed.Contains(template.RuntimeToolName));
        }
    }

    [Fact]
    public void SB07_INV_POLICY_003_allow_rules_never_grant_missing_assignments()
    {
        var missingIdentity = new CapabilityIdentity(AccessCapabilityKind.Tool, CapabilityKey.Create("workspace-write-file"));
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("allow-write-file"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByCapabilityKey(missingIdentity.Key),
                "Allow keeps existing assignments only.")
        ]);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [],
            [missingIdentity],
            [policy],
            "SB07_INV_POLICY_003"));

        Assert.Empty(result.AllowedCapabilities);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.RequiredCapabilityDenied, diagnostic.Category);
        Assert.Equal("SB07_INV_POLICY_003", diagnostic.CorrelationId);
    }

    [Fact]
    public void SB07_INV_SEED_001_managed_seed_dry_run_is_idempotent_without_duplicate_capability_identity()
    {
        var seed = SandboxWorkspaceSeedBuilder.Build();
        var normalized = SandboxWorkspaceSeedNormalizer.Normalize(seed);
        var normalizedAgain = SandboxWorkspaceSeedNormalizer.Normalize(normalized);

        AssertNoDuplicateCapabilityIdentity(normalized);
        AssertNoDuplicateCapabilityIdentity(normalizedAgain);

        var firstByKey = normalized.Capabilities.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var secondByKey = normalizedAgain.Capabilities.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(firstByKey.Keys.Order(StringComparer.OrdinalIgnoreCase), secondByKey.Keys.Order(StringComparer.OrdinalIgnoreCase));

        foreach (var key in firstByKey.Keys)
        {
            Assert.Equal(firstByKey[key].Id, secondByKey[key].Id);
            Assert.Equal(firstByKey[key].ConfigurationJson, secondByKey[key].ConfigurationJson);
        }

        Assert.Equal(
            "2026-06-agent-template-teams-v26",
            ReadManagedSeedVersion(firstByKey["workspace-read-file"].ConfigurationJson));
        Assert.Null(ReadManagedSeedVersion(firstByKey["mail-triage-inline-skill"].ConfigurationJson));
    }

    private static void AssertToolConfigurationParity(
        CapabilitySeedTemplateDescriptor template,
        CapabilityCatalogItem capability,
        List<string> failures,
        string seedVersion)
    {
        using var document = JsonDocument.Parse(capability.ConfigurationJson);
        var root = document.RootElement;
        if (root.GetProperty("tool").GetString() != template.RuntimeToolName)
        {
            failures.Add($"{template.Key}: runtime tool name drift");
        }

        if (root.GetProperty("approvalRequired").GetBoolean() != template.ApprovalRequired)
        {
            failures.Add($"{template.Key}: approval default drift");
        }

        if (root.GetProperty("managedSeedVersion").GetString() != seedVersion)
        {
            failures.Add($"{template.Key}: managed seed version drift");
        }
    }

    private static void AssertMcpConfigurationParity(
        CapabilitySeedTemplateDescriptor template,
        CapabilityCatalogItem capability,
        List<string> failures)
    {
        using var document = JsonDocument.Parse(capability.ConfigurationJson);
        var allowedTools = document.RootElement.GetProperty("allowedTools")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        var templateAllowedTools = template.McpTransport?.AllowedTools.ToArray() ?? [];

        if (!allowedTools.SequenceEqual(templateAllowedTools, StringComparer.Ordinal))
        {
            failures.Add($"{template.Key}: MCP allowed tool drift");
        }
    }

    private static void AssertIssue(
        IReadOnlyList<CapabilityValidationIssue> issues,
        CapabilityDiagnosticCategory category,
        string templatePath,
        string fieldPath,
        string? capabilityKey = null)
    {
        Assert.Contains(issues, issue =>
            issue.Category == category &&
            TemplatePathMatches(issue.TemplatePath?.Value, templatePath) &&
            issue.FieldPath == fieldPath &&
            (capabilityKey is null || issue.CapabilityKey?.Value == capabilityKey) &&
            !string.IsNullOrWhiteSpace(issue.RepairHint));
    }

    private static bool TemplatePathMatches(string? actual, string expectedSuffix)
    {
        return actual is not null &&
               (string.Equals(actual, expectedSuffix, StringComparison.Ordinal) ||
                actual.EndsWith($"/{expectedSuffix}", StringComparison.Ordinal));
    }

    private static CapabilityExposureDescriptor Candidate(
        string key,
        string? runtimeToolName,
        params CapabilityOperationClassification[] classifications)
        => Candidate(key, runtimeToolName, classifications.ToHashSet());

    private static CapabilityExposureDescriptor Candidate(
        string key,
        string? runtimeToolName,
        IReadOnlySet<CapabilityOperationClassification> classifications)
    {
        return new CapabilityExposureDescriptor(
            new CapabilityIdentity(AccessCapabilityKind.Tool, CapabilityKey.Create(key)),
            key,
            key,
            null,
            RuntimeToolName.TryCreate(runtimeToolName, out var runtimeName) ? runtimeName : null,
            key.StartsWith("playwright", StringComparison.OrdinalIgnoreCase) ? McpServerKey.Create("playwright-local") : null,
            key.StartsWith("playwright", StringComparison.OrdinalIgnoreCase) ? McpToolName.Create("browser_take_screenshot") : null,
            new HashSet<CapabilityTag>(),
            classifications,
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false),
            CapabilityAvailabilityState.Available,
            TemplatePath.Create("Templates/Capabilities/test.json"));
    }

    private static IReadOnlyList<CapabilityExposureDescriptor> Evaluate(
        CapabilityAccessPolicy policy,
        IReadOnlyList<CapabilityExposureDescriptor> candidates,
        string correlationId)
    {
        return new CapabilityAccessPolicyEvaluator()
            .Evaluate(new CapabilityAccessEvaluationContext(candidates, [], [policy], correlationId))
            .AllowedCapabilities;
    }

    private static void AssertAllowedKeys(
        CapabilityAccessPolicy policy,
        IReadOnlyList<CapabilityExposureDescriptor> candidates,
        string[] expectedKeys,
        string correlationId)
    {
        var allowedKeys = Evaluate(policy, candidates, correlationId)
            .Select(item => item.Identity.Key.Value)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expectedKeys.Order(StringComparer.OrdinalIgnoreCase), allowedKeys);
    }

    private static IReadOnlySet<CapabilityOperationClassification> ParseClassifications(IReadOnlyList<string> values)
    {
        return values
            .Select(value => CapabilityText.TryParseEnum<CapabilityOperationClassification>(value, out var parsed) ? parsed : (CapabilityOperationClassification?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToHashSet();
    }

    private static SeedCapabilityKind ParseSeedKind(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.GetValues<SeedCapabilityKind>()
            .Single(kind => string.Equals(kind.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid CreateStableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        Span<byte> buffer = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(buffer);
        buffer[6] = (byte)((buffer[6] & 0x0F) | 0x50);
        buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);
        return new Guid(buffer);
    }

    private static void AssertNoDuplicateCapabilityIdentity(SandboxWorkspaceDocument document)
    {
        Assert.DoesNotContain(
            document.Capabilities.GroupBy(item => item.Id),
            group => group.Count() > 1);
        Assert.DoesNotContain(
            document.Capabilities.GroupBy(item => $"{item.Kind}:{item.Key}", StringComparer.OrdinalIgnoreCase),
            group => group.Count() > 1);
    }

    private static string? ReadManagedSeedVersion(string configurationJson)
    {
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement.TryGetProperty("managedSeedVersion", out var version)
            ? version.GetString()
            : null;
    }

    private sealed class TemporaryCapabilityTemplatePack : IDisposable
    {
        private TemporaryCapabilityTemplatePack(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static TemporaryCapabilityTemplatePack Create(
            IReadOnlyDictionary<string, string> capabilityFiles,
            IReadOnlyDictionary<string, string> policyFiles,
            IReadOnlyList<string>? missingCapabilityRefs = null)
        {
            var root = Path.Combine(Path.GetTempPath(), $"capability-template-pack-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            foreach (var (relativePath, content) in capabilityFiles.Concat(policyFiles))
            {
                var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }

            var capabilityRefs = capabilityFiles.Keys.Concat(missingCapabilityRefs ?? [])
                .Select(path => $$"""{ "relativePath": "{{path}}" }""");
            var policyRefs = policyFiles.Keys
                .Select(path => $$"""{ "key": "{{Path.GetFileNameWithoutExtension(path)}}", "relativePath": "{{path}}" }""");
            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                $$"""
                {
                  "packKey": "test-pack",
                  "name": "Test Pack",
                  "version": "test",
                  "seedMarker": "test",
                  "seedVersion": "test",
                  "capabilityFiles": [{{string.Join(",", capabilityRefs)}}],
                  "policyFiles": [{{string.Join(",", policyRefs)}}]
                }
                """);

            return new TemporaryCapabilityTemplatePack(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
