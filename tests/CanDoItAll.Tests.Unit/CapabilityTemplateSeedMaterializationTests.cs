using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Unit;

public sealed class CapabilityTemplateSeedMaterializationTests
{
    [Fact]
    public void SB06_INV_TEMPLATE_001_default_pack_materializes_known_catalog_without_duplicate_keys()
    {
        var pack = new CapabilityTemplatePackLoader().Load();
        var capabilities = CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities(pack);
        var byKey = capabilities.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(ExpectedCapabilityKeys.Length, capabilities.Count);
        Assert.DoesNotContain(
            capabilities.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase),
            group => group.Count() > 1);
        foreach (var key in ExpectedCapabilityKeys)
        {
            Assert.Contains(key, byKey.Keys);
        }

        Assert.Equal(CreateStableGuid("capabilities/run-tests-skill"), byKey["run-tests"].Id);
        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.Tool, byKey["workspace-dotnet-test"].Kind);
        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer, byKey["playwright-local-mcp"].Kind);
        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.Skill, byKey["aspnet-core-skill"].Kind);
        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.Rag, byKey["workspace-source-rag"].Kind);
    }

    [Fact]
    public void SB06_INV_TEMPLATE_002_materialization_preserves_representative_configuration_json()
    {
        var pack = new CapabilityTemplatePackLoader().Load();
        var capabilities = CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities(pack)
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        using var playwrightJson = JsonDocument.Parse(capabilities["playwright-local-mcp"].ConfigurationJson);
        Assert.Equal(pack.Manifest.SeedVersion, playwrightJson.RootElement.GetProperty("managedSeedVersion").GetString());
        Assert.Equal("stdio", playwrightJson.RootElement.GetProperty("transport").GetString());
        Assert.Equal("npx", playwrightJson.RootElement.GetProperty("command").GetString());
        Assert.Equal("newlineDelimitedJson", playwrightJson.RootElement.GetProperty("messageFraming").GetString());
        Assert.Contains(
            playwrightJson.RootElement.GetProperty("allowedTools").EnumerateArray(),
            item => item.GetString() == "browser_take_screenshot");

        using var fileSearchJson = JsonDocument.Parse(capabilities["provider-native-file-search"].ConfigurationJson);
        Assert.Equal("provider_native_file_search", fileSearchJson.RootElement.GetProperty("tool").GetString());
        Assert.Equal(8, fileSearchJson.RootElement.GetProperty("maximumResultCount").GetInt32());

        using var dotnetNewJson = JsonDocument.Parse(capabilities["workspace-dotnet-new"].ConfigurationJson);
        Assert.Equal("workspace_dotnet_new", dotnetNewJson.RootElement.GetProperty("tool").GetString());
        Assert.True(dotnetNewJson.RootElement.GetProperty("approvalRequired").GetBoolean());

        using var blazorSkillJson = JsonDocument.Parse(capabilities["blazor-ssr-delivery-inline-skill"].ConfigurationJson);
        Assert.Equal("inline", blazorSkillJson.RootElement.GetProperty("skillSource").GetString());
        Assert.Equal("blazor-ssr-delivery", blazorSkillJson.RootElement.GetProperty("inlineSkill").GetProperty("name").GetString());
        Assert.Equal(3, blazorSkillJson.RootElement.GetProperty("inlineSkill").GetProperty("resources").GetArrayLength());

        using var ragJson = JsonDocument.Parse(capabilities["workspace-source-rag"].ConfigurationJson);
        Assert.Equal(".", ragJson.RootElement.GetProperty("ragRoot").GetString());
        Assert.Equal(5, ragJson.RootElement.GetProperty("maxResults").GetInt32());
    }

    [Fact]
    public void SB06_INV_TEMPLATE_003_invalid_templates_block_materialization_without_fallback()
    {
        using var packDirectory = new TemporaryCapabilityTemplatePack(
            "capabilities.json",
            """
            {
              "capabilities": [
                {
                  "kind": "tool",
                  "key": "workspace-read-file",
                  "displayName": "Workspace Read File",
                  "description": "Duplicate one.",
                  "stableId": "tool:workspace-read-file:v1",
                  "stableGuidKey": "capabilities/workspace-read-file",
                  "endpointOrPath": "sandbox://workspace-read-file",
                  "runtimeToolName": "workspace_read_file"
                },
                {
                  "kind": "tool",
                  "key": "workspace-read-file",
                  "displayName": "Workspace Read File Duplicate",
                  "description": "Duplicate two.",
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
                }
              ]
            }
            """);

        var exception = Assert.Throws<CapabilityTemplatePackValidationException>(() =>
            new CapabilityTemplatePackLoader(packDirectory.RootPath).Load());

        Assert.Contains(exception.Issues, issue =>
            issue.Category == CapabilityDiagnosticCategory.TemplateValidation &&
            issue.FieldPath == "$.capabilities[1].key" &&
            issue.CapabilityKey?.Value == "workspace-read-file");
        Assert.Contains(exception.Issues, issue =>
            issue.Category == CapabilityDiagnosticCategory.SecretBinding &&
            issue.FieldPath == "$.externalHttp.headers.Authorization");
    }

    [Fact]
    public void SB06_INV_TEMPLATE_004_agent_template_assignments_resolve_against_template_catalog()
    {
        var pack = new CapabilityTemplatePackLoader().Load();
        var capabilities = CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities(pack);
        var agentPack = new AgentTemplatePackLoader().Load();

        var result = CapabilityTemplateSeedAssignmentValidator.ValidateAgentAssignments(agentPack, capabilities);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SB06_INV_SEED_001_sandbox_seed_document_uses_template_backed_capability_catalog()
    {
        var document = SandboxWorkspaceSeedBuilder.Build();
        var templateCapabilities = CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities(new CapabilityTemplatePackLoader().Load());
        var templateKeys = templateCapabilities
            .Select(item => item.Key)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var documentKeys = document.Capabilities
            .Select(item => item.Key)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(templateKeys, documentKeys);
        Assert.DoesNotContain(
            document.Agents.SelectMany(agent => agent.Capabilities),
            assignment => !document.Capabilities.Any(capability =>
                string.Equals(capability.Key, assignment.CapabilityKey, StringComparison.OrdinalIgnoreCase) &&
                capability.Id == assignment.CapabilityId));
    }

    [Fact]
    public void SB06_INV_POLICY_001_access_policy_loader_compiles_typed_rules_and_rejects_unknown_grants()
    {
        var pack = new CapabilityTemplatePackLoader().Load();
        var policy = Assert.Single(pack.Policies, item => item.Key == "default-compatibility-policy").Policy;

        Assert.Contains(policy.Rules, rule =>
            rule.Scope == CapabilityAccessScope.ProcessStep &&
            rule.Effect == CapabilityAccessEffect.Deny &&
            rule.Selector.Kind == CapabilitySelectorKind.OperationClassification &&
            rule.Selector.OperationClassification == CapabilityOperationClassification.Mutation);

        var invalidPolicy = new CapabilityAccessPolicyTemplateDto
        {
            Rules =
            [
                new CapabilityAccessRuleTemplateDto
                {
                    Id = "allow-missing-capability",
                    Effect = "allow",
                    Scope = "processStep",
                    Selector = new CapabilitySelectorTemplateDto
                    {
                        Kind = "capabilityKey",
                        Value = "missing-capability"
                    },
                    Reason = "Invalid template must not grant unknown capabilities."
                }
            ]
        };

        var issues = CapabilityTemplateSeedPolicyValidator.ValidatePolicyReferences(
            invalidPolicy,
            TemplatePath.Create("Templates/Capabilities/policies/invalid.json"),
            pack.Capabilities);

        Assert.Contains(issues, issue =>
            issue.Category == CapabilityDiagnosticCategory.AccessPolicy &&
            issue.FieldPath == "$.rules[0].selector.value" &&
            issue.CapabilityKey?.Value == "missing-capability");
    }

    [Fact]
    public void SB06_INV_POLICY_002_allowed_operations_compile_to_typed_compatibility_rules()
    {
        var result = ProcessAllowedOperationsCapabilityPolicyCompiler.Compile(
            [ProcessOperationContractNames.RunValidation, ProcessOperationContractNames.ReadProjectStructure],
            TemplatePath.Create("Templates/Processes/processes/dotnet-development-slice/definition.json"),
            "$.steps[0].allowedOperations");

        Assert.True(result.ValidationResult.IsValid);
        Assert.Contains(result.Policy.Rules, rule =>
            rule.Effect == CapabilityAccessEffect.Allow &&
            rule.Selector.Kind == CapabilitySelectorKind.OperationClassification &&
            rule.Selector.OperationClassification == CapabilityOperationClassification.Validation);
        Assert.Contains(result.Policy.Rules, rule =>
            rule.Effect == CapabilityAccessEffect.Allow &&
            rule.Selector.OperationClassification == CapabilityOperationClassification.ProjectStructure);
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

    private static readonly string[] ExpectedCapabilityKeys =
    [
        "agent-package-export",
        "architecture-map-inline-skill",
        "architecture-review-inline-skill",
        "architecture-source-rag",
        "aspnet-core-skill",
        "blazor-ssr-delivery-inline-skill",
        "candoitall-bundle-workflow",
        "candoitall-codeanalytics-mcp",
        "candoitall-components-mcp",
        "candoitall-frontend-theme",
        "candoitall-watch-playwright-loop",
        "concrete-deliverable-delivery-inline-skill",
        "document-spreadsheet-reconciliation-inline-skill",
        "dotnet-app-delivery-inline-skill",
        "frontend-skill",
        "generated-app-summary-inline-skill",
        "mail-summary-inline-skill",
        "mail-triage-context",
        "mail-triage-inline-skill",
        "mem0-shared-memory",
        "playwright-local-mcp",
        "provider-health",
        "provider-native-code-interpreter",
        "provider-native-file-search",
        "provider-native-web-search",
        "repository-playbook",
        "research-briefing-context",
        "run-tests",
        "spreadsheet-skill",
        "workspace-analyze-image",
        "workspace-analyze-images",
        "workspace-append-file",
        "workspace-convert-document",
        "workspace-copy-path",
        "workspace-create-directory",
        "workspace-delete-path",
        "workspace-diff-text",
        "workspace-dotnet-build",
        "workspace-dotnet-new",
        "workspace-dotnet-restore",
        "workspace-dotnet-run",
        "workspace-dotnet-stop",
        "workspace-dotnet-test",
        "workspace-execution-boundary",
        "workspace-git-diff",
        "workspace-git-status",
        "workspace-inspect-image",
        "workspace-inspect-spreadsheet",
        "workspace-list-files",
        "workspace-move-path",
        "workspace-pwsh-run-script",
        "workspace-python-run-file",
        "workspace-read-file",
        "workspace-search",
        "workspace-source-rag",
        "workspace-stat-path",
        "workspace-write-file",
        "writing-mstest-tests"
    ];

    private sealed class TemporaryCapabilityTemplatePack : IDisposable
    {
        public TemporaryCapabilityTemplatePack(string capabilityFileName, string capabilityJson)
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"capability-template-pack-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(RootPath, "capabilities"));
            Directory.CreateDirectory(Path.Combine(RootPath, "policies"));
            File.WriteAllText(
                Path.Combine(RootPath, "manifest.json"),
                $$"""
                {
                  "packKey": "test-pack",
                  "name": "Test Pack",
                  "version": "test",
                  "seedMarker": "test",
                  "seedVersion": "test",
                  "capabilityFiles": [
                    {
                      "relativePath": "capabilities/{{capabilityFileName}}"
                    }
                  ],
                  "policyFiles": []
                }
                """);
            File.WriteAllText(Path.Combine(RootPath, "capabilities", capabilityFileName), capabilityJson);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
