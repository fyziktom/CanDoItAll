using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceProviderKind = CanDoItAll.Modules.Workspace.ProviderKind;
using WorkspaceProviderProfile = CanDoItAll.Modules.Workspace.ProviderProfile;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentFrameworkWorkspaceSeedIntegrationTests
{
    private static readonly Guid ManagedOpenAiImageProviderId = Guid.Parse("8958FA61-4BD6-1451-8123-4E4E4FEA2E26");

    [Fact]
    public void Seed_catalog_loads_generic_reconciliation_skill_and_retires_stale_built_in_skills()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var reconciliationCapability = Assert.Single(
            seed.Capabilities,
            item => string.Equals(item.Key, "document-spreadsheet-reconciliation-inline-skill", StringComparison.OrdinalIgnoreCase));
        var retiredCapabilityId = Guid.NewGuid();
        var retiredBundleWorkflowCapabilityId = Guid.NewGuid();
        var retiredMemoryCapabilityId = Guid.NewGuid();
        var retiredProjectTaskCreateCapabilityId = Guid.NewGuid();
        var retiredProjectTaskUpdateCapabilityId = Guid.NewGuid();
        var retiredCapability = new CapabilityCatalogItem(
            retiredCapabilityId,
            CapabilityKind.Skill,
            "retired-built-in-inline-skill",
            "Retired Built-In Inline Skill",
            "Previous built-in inline skill no longer present in the seed catalog.",
            "inline://retired-built-in-inline-skill",
            "{}",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var retiredBundleWorkflowCapability = new CapabilityCatalogItem(
            retiredBundleWorkflowCapabilityId,
            CapabilityKind.Skill,
            "candoitall-bundle-workflow",
            "Bundle Workflow Skill",
            "Old Codex development workflow skill that must not be exposed to internal runtime agents.",
            "~/.codex/skills/candoitall-bundle-workflow/SKILL.md",
            JsonSerializer.Serialize(new
            {
                skillSource = "file",
                skillRoot = "~/.codex/skills/candoitall-bundle-workflow"
            }),
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var retiredMemoryCapability = new CapabilityCatalogItem(
            retiredMemoryCapabilityId,
            CapabilityKind.Memory,
            "legacy-mem0-memory",
            "Legacy Mem0 Memory",
            "Retired catalog memory capability.",
            "https://api.mem0.ai",
            "{}",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var retiredProjectTaskCreateCapability = new CapabilityCatalogItem(
            retiredProjectTaskCreateCapabilityId,
            CapabilityKind.Tool,
            "project-task-create",
            "Project Task Create",
            "Retired duplicate authority; project task tools are governed by project-structure access metadata.",
            "sandbox://project-task-create",
            """{"tool":"project_task_create"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var retiredProjectTaskUpdateCapability = new CapabilityCatalogItem(
            retiredProjectTaskUpdateCapabilityId,
            CapabilityKind.Tool,
            "project-task-update",
            "Project Task Update",
            "Retired duplicate authority; project task tools are governed by project-structure access metadata.",
            "sandbox://project-task-update",
            """{"tool":"project_task_update"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var financialStrategist = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        var spreadsheetAnalyst = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Spreadsheet Analyst", StringComparison.Ordinal));
        var catalog = seed.ToCatalog() with
        {
            Capabilities = seed.Capabilities
                .Concat([
                    retiredCapability,
                    retiredBundleWorkflowCapability,
                    retiredMemoryCapability,
                    retiredProjectTaskCreateCapability,
                    retiredProjectTaskUpdateCapability
                ])
                .ToList(),
            Agents = seed.Agents.Select(agent => agent.Id == financialStrategist.Id
                ? agent with
                {
                    Capabilities = agent.Capabilities.Concat([
                        new AgentCapabilityAssignment(
                            retiredCapabilityId,
                            retiredCapability.Key,
                            retiredCapability.Kind,
                            CapabilityProofStatus.NotRun,
                            null,
                            string.Empty),
                        new AgentCapabilityAssignment(
                            retiredBundleWorkflowCapabilityId,
                            retiredBundleWorkflowCapability.Key,
                            retiredBundleWorkflowCapability.Kind,
                            CapabilityProofStatus.NotRun,
                            null,
                            string.Empty),
                        new AgentCapabilityAssignment(
                            retiredMemoryCapabilityId,
                            retiredMemoryCapability.Key,
                            retiredMemoryCapability.Kind,
                            CapabilityProofStatus.NotRun,
                            null,
                            string.Empty),
                        new AgentCapabilityAssignment(
                            retiredProjectTaskCreateCapabilityId,
                            retiredProjectTaskCreateCapability.Key,
                            retiredProjectTaskCreateCapability.Kind,
                            CapabilityProofStatus.NotRun,
                            null,
                            string.Empty),
                        new AgentCapabilityAssignment(
                            retiredProjectTaskUpdateCapabilityId,
                            retiredProjectTaskUpdateCapability.Key,
                            retiredProjectTaskUpdateCapability.Kind,
                            CapabilityProofStatus.NotRun,
                            null,
                            string.Empty)
                    ]).ToList()
                }
                : agent).ToList()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var normalizedFinancialStrategist = Assert.Single(
            normalized.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));

        Assert.DoesNotContain(normalized.Capabilities, item => item.Id == retiredCapabilityId);
        Assert.DoesNotContain(normalized.Capabilities, item => item.Id == retiredBundleWorkflowCapabilityId);
        Assert.DoesNotContain(normalized.Capabilities, item => item.Id == retiredMemoryCapabilityId);
        Assert.DoesNotContain(normalized.Capabilities, item => item.Id == retiredProjectTaskCreateCapabilityId);
        Assert.DoesNotContain(normalized.Capabilities, item => item.Id == retiredProjectTaskUpdateCapabilityId);
        Assert.DoesNotContain(normalized.Capabilities, item => string.Equals(item.Key, "candoitall-bundle-workflow", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(normalized.Capabilities, item => string.Equals(item.Key, "project-task-create", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(normalized.Capabilities, item => string.Equals(item.Key, "project-task-update", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == retiredCapabilityId);
        Assert.DoesNotContain(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == retiredBundleWorkflowCapabilityId);
        Assert.DoesNotContain(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == retiredMemoryCapabilityId);
        Assert.DoesNotContain(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == retiredProjectTaskCreateCapabilityId);
        Assert.DoesNotContain(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == retiredProjectTaskUpdateCapabilityId);
        Assert.Contains(spreadsheetAnalyst.Capabilities, item => item.CapabilityId == reconciliationCapability.Id);
        Assert.Contains(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == reconciliationCapability.Id);
    }

    [Fact]
    public void Seed_catalog_normalization_preserves_empty_model_for_explicit_local_provider_assignment()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var localOllama = Assert.Single(
            seed.Providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Local Ollama", StringComparison.Ordinal));
        var financialStrategist = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        var catalog = seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == financialStrategist.Id
                    ? agent with
                    {
                        ProviderProfileId = localOllama.Id,
                        Model = string.Empty
                    }
                    : agent)
                .ToList()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var normalizedFinancialStrategist = Assert.Single(
            normalized.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));

        Assert.Equal(localOllama.Id, normalizedFinancialStrategist.ProviderProfileId);
        Assert.Empty(normalizedFinancialStrategist.Model);
    }

    [Fact]
    public void Managed_agent_normalization_refreshes_permission_policy_drift_for_current_seed_version()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var financialStrategist = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        Assert.True(financialStrategist.Permissions.AutoApproveExternalCallsByDefault);

        var catalog = seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == financialStrategist.Id
                    ? agent with
                    {
                        Permissions = agent.Permissions with
                        {
                            AutoApproveExternalCallsByDefault = false
                        }
                    }
                    : agent)
                .ToList()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var normalizedFinancialStrategist = Assert.Single(
            normalized.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));

        Assert.True(normalizedFinancialStrategist.Permissions.AutoApproveExternalCallsByDefault);
        Assert.Equal(financialStrategist.ConfigurationJson, normalizedFinancialStrategist.ConfigurationJson);
    }

    [Fact]
    public void Managed_agent_normalization_refreshes_capability_policy_drift_for_current_seed_version()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var financialStrategist = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        Assert.Contains(
            financialStrategist.Capabilities,
            item => string.Equals(item.CapabilityKey, "workspace-convert-document", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            financialStrategist.Capabilities,
            item => string.Equals(item.CapabilityKey, "provider-native-code-interpreter", StringComparison.OrdinalIgnoreCase));

        var catalog = seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == financialStrategist.Id
                    ? agent with
                    {
                        Capabilities = agent.Capabilities
                            .Where(capability => !string.Equals(capability.CapabilityKey, "workspace-convert-document", StringComparison.OrdinalIgnoreCase))
                            .ToList()
                    }
                    : agent)
                .ToList()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var normalizedFinancialStrategist = Assert.Single(
            normalized.Agents,
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));

        Assert.Equal(
            financialStrategist.Capabilities.Select(item => item.CapabilityKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
            normalizedFinancialStrategist.Capabilities.Select(item => item.CapabilityKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Saving_managed_agent_policy_customization_preserves_dialog_settings()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, ".NET Application Developer", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        var removedCapabilityId = Assert.Single(editor.SelectedCapabilityIds.Take(1));

        Assert.False(editor.ProjectStructureAccess.CanWrite);
        Assert.False(editor.ProcessAccess.CanWrite);
        Assert.False(editor.ImageGenerationAccess.CanGenerateImages);

        editor.ProjectStructureAccess.CanWrite = true;
        editor.ProcessAccess.CanWrite = true;
        editor.ImageGenerationAccess.CanGenerateImages = true;
        editor.WorkspaceToolAccess.Profile = AgentWorkspaceToolProfileKind.Custom;
        editor.WorkspaceToolAccess.CanScaffoldProjects = false;
        editor.Permissions = editor.Permissions with
        {
            RequiresApprovalForExternalCalls = !editor.Permissions.RequiresApprovalForExternalCalls
        };
        editor.SelectedCapabilityIds.Remove(removedCapabilityId);

        await workspaceService.SaveAgentAsync(editor);

        var saved = await workspaceService.GetAgentEditorAsync(agent.Id);
        Assert.True(saved.ProjectStructureAccess.CanWrite);
        Assert.True(saved.ProcessAccess.CanWrite);
        Assert.True(saved.ImageGenerationAccess.CanGenerateImages);
        Assert.Equal(AgentWorkspaceToolProfileKind.Custom, saved.WorkspaceToolAccess.Profile);
        Assert.False(saved.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.Equal(editor.Permissions.RequiresApprovalForExternalCalls, saved.Permissions.RequiresApprovalForExternalCalls);
        Assert.DoesNotContain(removedCapabilityId, saved.SelectedCapabilityIds);
        Assert.True(AgentManagedSeedCustomizationMetadata.HasCurrentCustomization(saved.ConfigurationJson));
    }

    [Fact]
    public void Managed_agent_normalization_preserves_customizations_from_a_stale_seed_version()
    {
        const string staleManagedSeedVersion = "2026-07-agent-template-teams-v61";
        const string customizedSummary = "Customer-owned programming workflow and review policy.";

        var seed = SandboxWorkspaceSeedFactory.Create();
        var programmingAgent = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var currentManagedSeedVersion = GetExpectedManagedSeedVersion();
        var staleCustomizedAgent = programmingAgent with
        {
            Summary = customizedSummary,
            Model = OpenAiModelIds.Gpt56Terra,
            Permissions = programmingAgent.Permissions with
            {
                CanAskOtherAgents = !programmingAgent.Permissions.CanAskOtherAgents
            },
            ConfigurationJson = AgentManagedSeedCustomizationMetadata
                .MarkCustomized(programmingAgent.ConfigurationJson)
                .Replace(currentManagedSeedVersion, staleManagedSeedVersion, StringComparison.Ordinal)
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == programmingAgent.Id ? staleCustomizedAgent : agent)
                .ToList()
        });
        var preservedAgent = Assert.Single(normalized.Agents, agent => agent.Id == programmingAgent.Id);

        Assert.Equal(customizedSummary, preservedAgent.Summary);
        Assert.Equal(OpenAiModelIds.Gpt56Terra, preservedAgent.Model);
        Assert.Equal(staleCustomizedAgent.Permissions, preservedAgent.Permissions);
        Assert.Equal(staleCustomizedAgent.ConfigurationJson, preservedAgent.ConfigurationJson);
        Assert.Contains(staleManagedSeedVersion, preservedAgent.ConfigurationJson, StringComparison.Ordinal);
        Assert.True(AgentManagedSeedCustomizationMetadata.HasCustomization(preservedAgent.ConfigurationJson));
    }

    [Fact]
    public void Managed_seed_normalization_restores_canonical_actor_without_promoting_template_key_lookalike()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var lookalike = seededHrAgent with
        {
            Id = Guid.NewGuid(),
            Name = "HR identity lookalike",
            Capabilities = []
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Where(agent => agent.Id != seededHrAgent.Id)
                .Append(lookalike)
                .ToArray()
        });

        var canonical = Assert.Single(normalized.Agents, HrAgentIdentity.Matches);
        var preservedLookalike = Assert.Single(normalized.Agents, agent => agent.Id == lookalike.Id);
        Assert.Equal(HrAgentIdentity.AgentId, canonical.Id);
        Assert.False(HrAgentIdentity.Matches(preservedLookalike));
        Assert.Empty(preservedLookalike.Capabilities);
    }

    [Theory]
    [InlineData("")]
    [InlineData("HR-AGENT")]
    [InlineData("stale-managed-hr-key")]
    public void Managed_seed_normalization_repairs_trusted_reserved_identity_drift_idempotently(
        string driftedTemplateKey)
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var drifted = seededHrAgent with
        {
            Name = "Customized HR display name",
            IsTemplate = true,
            TemplateKey = driftedTemplateKey
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == seededHrAgent.Id ? drifted : agent)
                .ToArray()
        });
        var repaired = Assert.Single(normalized.Agents, HrAgentIdentity.Matches);

        Assert.False(repaired.IsTemplate);
        Assert.Equal(HrAgentIdentity.TemplateKey, repaired.TemplateKey);
        Assert.Equal(drifted.Name, repaired.Name);

        var normalizedAgain = SandboxWorkspaceSeedFactory.NormalizeCatalog(normalized);
        var repairedAgain = Assert.Single(normalizedAgain.Agents, HrAgentIdentity.Matches);

        Assert.False(repairedAgain.IsTemplate);
        Assert.Equal(repaired.TemplateKey, repairedAgain.TemplateKey);
        Assert.Equal(repaired.Name, repairedAgain.Name);
        Assert.Equal(
            repaired.Capabilities.Select(assignment => assignment.CapabilityId),
            repairedAgain.Capabilities.Select(assignment => assignment.CapabilityId));
    }

    [Fact]
    public void Managed_seed_normalization_rejects_untrusted_reserved_agent_id_collision()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var collision = seededHrAgent with
        {
            Name = "Untrusted reserved-id collision",
            TemplateKey = "untrusted-agent",
            ConfigurationJson = "{}",
            Capabilities = []
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
            {
                Agents = seed.Agents
                    .Select(agent => agent.Id == seededHrAgent.Id ? collision : agent)
                    .ToArray()
            }));

        Assert.Contains("collides with reserved managed identity", exception.Message, StringComparison.Ordinal);
        Assert.Contains(HrAgentIdentity.AgentId.ToString("D"), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_seed_normalization_canonicalizes_built_in_catalog_contract_fields()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededCapability = Assert.Single(
            seed.Capabilities,
            capability => string.Equals(
                capability.Key,
                CapabilityCuratorAgentIdentity.CatalogSearchCapabilityKey,
                StringComparison.Ordinal));
        var driftedCapability = seededCapability with
        {
            Kind = CapabilityKind.Skill,
            Key = seededCapability.Key.ToUpperInvariant()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Capabilities = seed.Capabilities
                .Select(capability => capability.Id == seededCapability.Id ? driftedCapability : capability)
                .ToArray()
        });

        var repaired = Assert.Single(normalized.Capabilities, capability => capability.Id == seededCapability.Id);
        Assert.Equal(CapabilityKind.Tool, repaired.Kind);
        Assert.Equal(seededCapability.Key, repaired.Key);
    }

    [Fact]
    public void Managed_capability_version_refreshes_only_changed_built_in_at_same_pack_version_and_is_idempotent()
    {
        const string staleInstructions = "Stale HR governance instructions.";
        const string customTag = "customer-observation";
        var seed = SandboxWorkspaceSeedFactory.Create();
        var changedCapability = Assert.Single(
            seed.Capabilities,
            capability => string.Equals(
                capability.Key,
                "hr-agent-governance-inline-skill",
                StringComparison.Ordinal));
        var unchangedCapability = Assert.Single(
            seed.Capabilities,
            capability => string.Equals(
                capability.Key,
                CapabilityCuratorAgentIdentity.CatalogSearchCapabilityKey,
                StringComparison.Ordinal));
        var staleConfiguration = JsonNode.Parse(changedCapability.ConfigurationJson)!.AsObject();
        var packVersion = staleConfiguration[ManagedCapabilitySeedMetadata.PackVersionPropertyName]!.GetValue<string>();
        staleConfiguration[ManagedCapabilitySeedMetadata.CapabilityVersionPropertyName] =
            "skill:hr-agent-governance:v1";
        staleConfiguration["inlineSkill"]!["instructions"] = staleInstructions;
        var customCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            "customer-garden-catalog",
            "Customer Garden Catalog",
            "Customer-owned garden catalog.",
            "custom://customer-garden-catalog",
            """
            {
              "managedSeedVersion": "customer-v1",
              "managedCapabilityVersion": "tool:customer-garden-catalog:v1"
            }
            """,
            CapabilityProofStatus.Verified,
            "Customer proof",
            DateTimeOffset.Parse("2026-08-02T12:00:00Z"),
            IsBuiltIn: false);
        var staleChangedCapability = changedCapability with
        {
            ConfigurationJson = staleConfiguration.ToJsonString(),
            Tags = changedCapability.Tags.Append("stale-version-marker").ToArray()
        };
        var taggedUnchangedCapability = unchangedCapability with
        {
            Tags = unchangedCapability.Tags.Append(customTag).ToArray()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Capabilities = seed.Capabilities
                .Select(capability => capability.Id switch
                {
                    var id when id == changedCapability.Id => staleChangedCapability,
                    var id when id == unchangedCapability.Id => taggedUnchangedCapability,
                    _ => capability
                })
                .Append(customCapability)
                .ToArray()
        });

        var refreshed = Assert.Single(normalized.Capabilities, capability => capability.Id == changedCapability.Id);
        var preservedUnchanged = Assert.Single(
            normalized.Capabilities,
            capability => capability.Id == unchangedCapability.Id);
        var preservedCustom = Assert.Single(normalized.Capabilities, capability => capability.Id == customCapability.Id);
        Assert.Equal(changedCapability.ConfigurationJson, refreshed.ConfigurationJson);
        Assert.DoesNotContain("stale-version-marker", refreshed.Tags, StringComparer.OrdinalIgnoreCase);
        using (var refreshedConfiguration = JsonDocument.Parse(refreshed.ConfigurationJson))
        {
            Assert.Equal(
                packVersion,
                refreshedConfiguration.RootElement
                    .GetProperty(ManagedCapabilitySeedMetadata.PackVersionPropertyName)
                    .GetString());
            Assert.Equal(
                "skill:hr-agent-governance:v2",
                refreshedConfiguration.RootElement
                    .GetProperty(ManagedCapabilitySeedMetadata.CapabilityVersionPropertyName)
                    .GetString());
            Assert.DoesNotContain(
                staleInstructions,
                refreshedConfiguration.RootElement.GetProperty("inlineSkill").GetProperty("instructions").GetString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(unchangedCapability.ConfigurationJson, preservedUnchanged.ConfigurationJson);
        Assert.Contains(customTag, preservedUnchanged.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(customCapability.Kind, preservedCustom.Kind);
        Assert.Equal(customCapability.Key, preservedCustom.Key);
        Assert.Equal(customCapability.Name, preservedCustom.Name);
        Assert.Equal(customCapability.Description, preservedCustom.Description);
        Assert.Equal(customCapability.EndpointOrPath, preservedCustom.EndpointOrPath);
        Assert.Equal(customCapability.ConfigurationJson, preservedCustom.ConfigurationJson);
        Assert.Equal(customCapability.ProofStatus, preservedCustom.ProofStatus);
        Assert.Equal(customCapability.ProofNotes, preservedCustom.ProofNotes);
        Assert.Equal(customCapability.LastVerifiedAtUtc, preservedCustom.LastVerifiedAtUtc);
        Assert.False(preservedCustom.IsBuiltIn);
        Assert.Contains("tool", preservedCustom.Tags, StringComparer.OrdinalIgnoreCase);

        var normalizedAgain = SandboxWorkspaceSeedFactory.NormalizeCatalog(normalized);
        var refreshedAgain = Assert.Single(
            normalizedAgain.Capabilities,
            capability => capability.Id == changedCapability.Id);
        var unchangedAgain = Assert.Single(
            normalizedAgain.Capabilities,
            capability => capability.Id == unchangedCapability.Id);
        var customAgain = Assert.Single(normalizedAgain.Capabilities, capability => capability.Id == customCapability.Id);
        Assert.Equal(refreshed.ConfigurationJson, refreshedAgain.ConfigurationJson);
        Assert.Equal(refreshed.Tags, refreshedAgain.Tags);
        Assert.Equal(preservedUnchanged.ConfigurationJson, unchangedAgain.ConfigurationJson);
        Assert.Equal(preservedUnchanged.Tags, unchangedAgain.Tags);
        Assert.Equal(preservedCustom.ConfigurationJson, customAgain.ConfigurationJson);
        Assert.Equal(preservedCustom.Tags, customAgain.Tags);
    }

    [Fact]
    public void Legacy_versioned_built_in_without_capability_version_receives_one_time_seed_refresh()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededCapability = Assert.Single(
            seed.Capabilities,
            capability => string.Equals(
                capability.Key,
                "workflow-curator-agent-inline-skill",
                StringComparison.Ordinal));
        var legacyConfiguration = JsonNode.Parse(seededCapability.ConfigurationJson)!.AsObject();
        Assert.True(legacyConfiguration.Remove(ManagedCapabilitySeedMetadata.CapabilityVersionPropertyName));
        legacyConfiguration["inlineSkill"]!["instructions"] = "Legacy workflow curator instructions.";

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Capabilities = seed.Capabilities
                .Select(capability => capability.Id == seededCapability.Id
                    ? capability with { ConfigurationJson = legacyConfiguration.ToJsonString() }
                    : capability)
                .ToArray()
        });
        var refreshed = Assert.Single(normalized.Capabilities, capability => capability.Id == seededCapability.Id);
        Assert.Equal(seededCapability.ConfigurationJson, refreshed.ConfigurationJson);

        var normalizedAgain = SandboxWorkspaceSeedFactory.NormalizeCatalog(normalized);
        var refreshedAgain = Assert.Single(
            normalizedAgain.Capabilities,
            capability => capability.Id == seededCapability.Id);
        Assert.Equal(refreshed.ConfigurationJson, refreshedAgain.ConfigurationJson);
        Assert.Equal(refreshed.Tags, refreshedAgain.Tags);
    }

    [Fact]
    public void Managed_seed_normalization_rejects_custom_catalog_collision_with_built_in_identity()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededCapability = Assert.Single(
            seed.Capabilities,
            capability => string.Equals(
                capability.Key,
                CapabilityCuratorAgentIdentity.CatalogSearchCapabilityKey,
                StringComparison.Ordinal));
        var customCollision = seededCapability with
        {
            Id = Guid.NewGuid(),
            IsBuiltIn = false,
            Name = "Customer-owned collision"
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
            {
                Capabilities = seed.Capabilities
                    .Where(capability => capability.Id != seededCapability.Id)
                    .Append(customCollision)
                    .ToArray()
            }));

        Assert.Contains("collides with built-in", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(seededCapability.Key, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_normalization_canonicalizes_custom_agent_assignment_snapshot_from_catalog_identity()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var customCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Skill,
            "customer-garden-design",
            "Customer garden design",
            "Customer-owned skill.",
            "inline://customer-garden-design",
            "{}",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            IsBuiltIn: false);
        var customAgent = seed.Agents.First(agent => !ManagedAdministrativeAgentIdentityCatalog.AgentIds.Contains(agent.Id)) with
        {
            Id = Guid.NewGuid(),
            Name = "Customer Gardener",
            TemplateKey = "customer-gardener",
            Capabilities =
            [
                new AgentCapabilityAssignment(
                    customCapability.Id,
                    customCapability.Key.ToUpperInvariant(),
                    CapabilityKind.Tool,
                    CapabilityProofStatus.NotRun,
                    null,
                    string.Empty)
            ]
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Capabilities = seed.Capabilities.Append(customCapability).ToArray(),
            Agents = seed.Agents.Append(customAgent).ToArray()
        });

        var repaired = Assert.Single(normalized.Agents, agent => agent.Id == customAgent.Id);
        var assignment = Assert.Single(repaired.Capabilities);
        Assert.Equal(customCapability.Key, assignment.CapabilityKey);
        Assert.Equal(customCapability.Kind, assignment.Kind);
    }

    [Fact]
    public void Customized_managed_agent_normalization_repairs_present_assignment_snapshots_without_restoring_removed_grants()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var catalogSearch = Assert.Single(
            seededHrAgent.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                CapabilityCuratorAgentIdentity.CatalogSearchCapabilityKey,
                StringComparison.Ordinal));
        var editorGet = Assert.Single(
            seededHrAgent.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                CapabilityCuratorAgentIdentity.EditorGetCapabilityKey,
                StringComparison.Ordinal));
        var customCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Skill,
            "customer-retained-skill",
            "Customer retained skill",
            "Customer-owned capability that normalization must preserve.",
            "inline://customer-retained-skill",
            "{}",
            CapabilityProofStatus.Verified,
            "Customer proof",
            DateTimeOffset.Parse("2026-08-01T12:00:00Z"),
            false);
        var customAssignment = new AgentCapabilityAssignment(
            customCapability.Id,
            customCapability.Key,
            customCapability.Kind,
            customCapability.ProofStatus,
            customCapability.LastVerifiedAtUtc,
            customCapability.ProofNotes);
        var driftedAssignments = seededHrAgent.Capabilities
            .Where(assignment => !string.Equals(
                assignment.CapabilityKey,
                CapabilityCuratorAgentIdentity.SaveCapabilityKey,
                StringComparison.Ordinal))
            .Where(assignment => assignment.CapabilityId != catalogSearch.CapabilityId)
            .Where(assignment => assignment.CapabilityId != editorGet.CapabilityId)
            .Append(catalogSearch with
            {
                CapabilityKey = catalogSearch.CapabilityKey.ToUpperInvariant(),
                Kind = CapabilityKind.Skill
            })
            .Append(catalogSearch with
            {
                CapabilityKey = catalogSearch.CapabilityKey.ToUpperInvariant(),
                ProofNotes = "duplicate stale snapshot"
            })
            .Append(editorGet with
            {
                CapabilityId = Guid.NewGuid(),
                CapabilityKey = editorGet.CapabilityKey.ToUpperInvariant(),
                Kind = CapabilityKind.Skill
            })
            .Append(customAssignment)
            .ToArray();
        var customizedHrAgent = seededHrAgent with
        {
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(
                seededHrAgent.ConfigurationJson),
            Capabilities = driftedAssignments
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Capabilities = seed.Capabilities.Append(customCapability).ToArray(),
            Agents = seed.Agents
                .Select(agent => agent.Id == seededHrAgent.Id ? customizedHrAgent : agent)
                .ToArray()
        });
        var repairedHrAgent = Assert.Single(normalized.Agents, HrAgentIdentity.Matches);

        Assert.DoesNotContain(
            repairedHrAgent.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                CapabilityCuratorAgentIdentity.SaveCapabilityKey,
                StringComparison.Ordinal));
        var repairedCatalogSearch = Assert.Single(
            repairedHrAgent.Capabilities,
            assignment => assignment.CapabilityId == catalogSearch.CapabilityId);
        Assert.Equal(catalogSearch.CapabilityKey, repairedCatalogSearch.CapabilityKey);
        Assert.Equal(CapabilityKind.Tool, repairedCatalogSearch.Kind);
        var repairedEditorGet = Assert.Single(
            repairedHrAgent.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                editorGet.CapabilityKey,
                StringComparison.Ordinal));
        Assert.Equal(editorGet.CapabilityId, repairedEditorGet.CapabilityId);
        Assert.Equal(CapabilityKind.Tool, repairedEditorGet.Kind);
        Assert.Contains(repairedHrAgent.Capabilities, assignment => assignment == customAssignment);
    }

    [Fact]
    public void Customized_hr_normalization_unions_curation_access_once_without_overwriting_customer_policy()
    {
        const string unrelatedRevokedCapabilityKey = "hr-crm-search";
        const string customizedSummary = "Customer-owned Terra HR policy.";
        const string customizedInstructions = "Retain the customer's HR operating instructions.";
        const string customerConfigurationValue = "retain-this-policy";

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var seededOpenAiProvider = Assert.Single(
            seed.Providers,
            provider => string.Equals(
                provider.Name,
                ManagedSeedProviderFallbacks.OpenAiDefaultProviderName,
                StringComparison.Ordinal));
        var terraProvider = seededOpenAiProvider with
        {
            Id = Guid.NewGuid(),
            Name = "Customer Terra HR provider",
            DefaultModel = OpenAiModelIds.Gpt56Terra,
            Tags = seededOpenAiProvider.Tags
                .Append("customer-owned")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        var curationAssignments = seededHrAgent.Capabilities
            .Where(assignment => HrAgentIdentity.CapabilityCurationCapabilityKeys.Contains(
                assignment.CapabilityKey))
            .ToArray();
        Assert.Equal(HrAgentIdentity.CapabilityCurationCapabilityKeys.Count, curationAssignments.Length);
        var unrelatedRevokedAssignment = Assert.Single(
            seededHrAgent.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                unrelatedRevokedCapabilityKey,
                StringComparison.Ordinal));
        var retainedAssignments = seededHrAgent.Capabilities
            .Where(assignment =>
                assignment.CapabilityId != unrelatedRevokedAssignment.CapabilityId &&
                !HrAgentIdentity.CapabilityCurationCapabilityKeys.Contains(assignment.CapabilityKey))
            .ToArray();

        var customizedConfiguration = JsonNode.Parse(seededHrAgent.ConfigurationJson)?.AsObject()
            ?? throw new InvalidOperationException("The seeded HR configuration must be a JSON object.");
        customizedConfiguration.Remove(HrAgentIdentity.CapabilityCurationAccessVersionPropertyName);
        customizedConfiguration["customerHrPolicy"] = customerConfigurationValue;
        var customizedHrAgent = seededHrAgent with
        {
            Summary = customizedSummary,
            Instructions = customizedInstructions,
            ProviderProfileId = terraProvider.Id,
            Model = OpenAiModelIds.Gpt56Terra,
            Permissions = seededHrAgent.Permissions with
            {
                CanAskOtherAgents = !seededHrAgent.Permissions.CanAskOtherAgents
            },
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(
                customizedConfiguration.ToJsonString()),
            Capabilities = retainedAssignments,
            Tags = seededHrAgent.Tags
                .Append("customer-managed-hr")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        var spoofConfiguration = JsonNode.Parse(customizedHrAgent.ConfigurationJson)?.AsObject()
            ?? throw new InvalidOperationException("The customized HR configuration must be a JSON object.");
        spoofConfiguration["customerHrPolicy"] = "spoof-must-remain-untouched";
        var hrIdentitySpoof = customizedHrAgent with
        {
            Id = Guid.NewGuid(),
            Name = "HR identity spoof",
            ConfigurationJson = spoofConfiguration.ToJsonString(),
            Capabilities = []
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Providers = seed.Providers.Append(terraProvider).ToArray(),
            Agents = seed.Agents
                .Select(agent => agent.Id == seededHrAgent.Id ? customizedHrAgent : agent)
                .ToArray()
        });
        var migratedHrAgent = Assert.Single(normalized.Agents, HrAgentIdentity.Matches);

        Assert.Equal(terraProvider.Id, migratedHrAgent.ProviderProfileId);
        Assert.Equal(OpenAiModelIds.Gpt56Terra, migratedHrAgent.Model);
        Assert.Equal(customizedSummary, migratedHrAgent.Summary);
        Assert.Equal(customizedInstructions, migratedHrAgent.Instructions);
        Assert.Equal(customizedHrAgent.Permissions, migratedHrAgent.Permissions);
        Assert.Contains("customer-managed-hr", migratedHrAgent.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            retainedAssignments
                .Concat(curationAssignments)
                .Select(assignment => assignment.CapabilityKey)
                .OrderBy(item => item, StringComparer.Ordinal),
            migratedHrAgent.Capabilities
                .Select(assignment => assignment.CapabilityKey)
                .OrderBy(item => item, StringComparer.Ordinal));
        Assert.DoesNotContain(
            migratedHrAgent.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                unrelatedRevokedCapabilityKey,
                StringComparison.Ordinal));
        using (var migratedConfiguration = JsonDocument.Parse(migratedHrAgent.ConfigurationJson))
        {
            Assert.Equal(
                customerConfigurationValue,
                migratedConfiguration.RootElement.GetProperty("customerHrPolicy").GetString());
            Assert.Equal(
                HrAgentIdentity.CurrentCapabilityCurationAccessVersion,
                migratedConfiguration.RootElement
                    .GetProperty(HrAgentIdentity.CapabilityCurationAccessVersionPropertyName)
                    .GetString());
        }

        var normalizedSpoofCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Providers = seed.Providers.Append(terraProvider).ToArray(),
            Agents = seed.Agents
                .Where(agent => agent.Id != seededHrAgent.Id)
                .Append(hrIdentitySpoof)
                .ToArray()
        });
        var normalizedSpoof = Assert.Single(
            normalizedSpoofCatalog.Agents,
            agent => agent.Id == hrIdentitySpoof.Id);
        Assert.Equal(hrIdentitySpoof.ConfigurationJson, normalizedSpoof.ConfigurationJson);
        Assert.Empty(normalizedSpoof.Capabilities);
        Assert.False(HrAgentIdentity.Matches(normalizedSpoof));

        var deliberatelyRemovedCapabilityKey = CapabilityCuratorAgentIdentity.SaveCapabilityKey;
        const string preexistingLedgerMarker = "operator-preserved-ledger-entry";
        var postMigrationConfiguration = JsonNode.Parse(migratedHrAgent.ConfigurationJson)?.AsObject()
            ?? throw new InvalidOperationException("The migrated HR configuration must be a JSON object.");
        postMigrationConfiguration[HrAgentIdentity.CapabilityCurationAccessVersionPropertyName] =
            preexistingLedgerMarker;
        var hrAgentAfterDeliberateRemoval = migratedHrAgent with
        {
            ConfigurationJson = postMigrationConfiguration.ToJsonString(),
            Capabilities = migratedHrAgent.Capabilities
                .Where(assignment => !string.Equals(
                    assignment.CapabilityKey,
                    deliberatelyRemovedCapabilityKey,
                    StringComparison.Ordinal))
                .ToArray()
        };
        var normalizedAgain = SandboxWorkspaceSeedFactory.NormalizeCatalog(normalized with
        {
            Agents = normalized.Agents
                .Select(agent => agent.Id == migratedHrAgent.Id ? hrAgentAfterDeliberateRemoval : agent)
                .ToArray()
        });
        var preservedRemoval = Assert.Single(normalizedAgain.Agents, HrAgentIdentity.Matches);

        Assert.DoesNotContain(
            preservedRemoval.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                deliberatelyRemovedCapabilityKey,
                StringComparison.Ordinal));
        Assert.Equal(terraProvider.Id, preservedRemoval.ProviderProfileId);
        Assert.Equal(OpenAiModelIds.Gpt56Terra, preservedRemoval.Model);
        Assert.Equal(customizedSummary, preservedRemoval.Summary);
        using var preservedConfiguration = JsonDocument.Parse(preservedRemoval.ConfigurationJson);
        Assert.Equal(
            preexistingLedgerMarker,
            preservedConfiguration.RootElement
                .GetProperty(HrAgentIdentity.CapabilityCurationAccessVersionPropertyName)
                .GetString());
    }

    [Fact]
    public void Customized_workflow_curator_receives_runtime_access_once_then_preserves_operator_removal()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededCurator = Assert.Single(seed.Agents, WorkflowCuratorAgentIdentity.Matches);
        var seededRuntimeAssignments = seededCurator.Capabilities
            .Where(assignment => WorkflowRuntimeCapabilityKeys.Keys.Contains(assignment.CapabilityKey))
            .ToArray();
        Assert.Equal(WorkflowRuntimeCapabilityKeys.Keys.Count, seededRuntimeAssignments.Length);
        var configuration = JsonNode.Parse(seededCurator.ConfigurationJson)?.AsObject()
            ?? throw new InvalidOperationException("The seeded Workflow Curator configuration must be an object.");
        configuration.Remove(WorkflowCuratorAgentIdentity.RuntimeAccessVersionPropertyName);
        configuration["customerWorkflowPolicy"] = "preserve";
        var customized = seededCurator with
        {
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(
                configuration.ToJsonString()),
            Capabilities = seededCurator.Capabilities
                .Where(assignment => !WorkflowRuntimeCapabilityKeys.Keys.Contains(
                    assignment.CapabilityKey))
                .ToArray()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == seededCurator.Id ? customized : agent)
                .ToArray()
        });
        var migrated = Assert.Single(normalized.Agents, WorkflowCuratorAgentIdentity.Matches);

        Assert.Equal(
            WorkflowRuntimeCapabilityKeys.Keys.OrderBy(key => key, StringComparer.Ordinal),
            migrated.Capabilities
                .Where(assignment => WorkflowRuntimeCapabilityKeys.Keys.Contains(
                    assignment.CapabilityKey))
                .Select(assignment => assignment.CapabilityKey)
                .OrderBy(key => key, StringComparer.Ordinal));
        using (var migratedConfiguration = JsonDocument.Parse(migrated.ConfigurationJson))
        {
            Assert.Equal(
                "preserve",
                migratedConfiguration.RootElement.GetProperty("customerWorkflowPolicy").GetString());
            Assert.Equal(
                WorkflowCuratorAgentIdentity.CurrentRuntimeAccessVersion,
                migratedConfiguration.RootElement
                    .GetProperty(WorkflowCuratorAgentIdentity.RuntimeAccessVersionPropertyName)
                    .GetString());
        }

        var deliberatelyRemoved = WorkflowRuntimeCapabilityKeys.RunCancel;
        var normalizedAgain = SandboxWorkspaceSeedFactory.NormalizeCatalog(normalized with
        {
            Agents = normalized.Agents
                .Select(agent => agent.Id == migrated.Id
                    ? agent with
                    {
                        Capabilities = agent.Capabilities
                            .Where(assignment => !string.Equals(
                                assignment.CapabilityKey,
                                deliberatelyRemoved,
                                StringComparison.Ordinal))
                            .ToArray()
                    }
                    : agent)
                .ToArray()
        });
        var preserved = Assert.Single(normalizedAgain.Agents, WorkflowCuratorAgentIdentity.Matches);

        Assert.DoesNotContain(
            preserved.Capabilities,
            assignment => string.Equals(
                assignment.CapabilityKey,
                deliberatelyRemoved,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Customized_scheduler_receives_scheduling_access_once_then_preserves_operator_removal()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededScheduler = Assert.Single(seed.Agents, SchedulerAgentIdentity.Matches);
        var configuration = JsonNode.Parse(seededScheduler.ConfigurationJson)?.AsObject()
            ?? throw new InvalidOperationException("The seeded Scheduler configuration must be an object.");
        configuration.Remove(SchedulerAgentIdentity.SchedulingAccessVersionPropertyName);
        configuration["customerSchedulerPolicy"] = "preserve";
        var customized = seededScheduler with
        {
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(
                configuration.ToJsonString()),
            Permissions = seededScheduler.Permissions with { CanScheduleWork = false }
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == seededScheduler.Id ? customized : agent)
                .ToArray()
        });
        var migrated = Assert.Single(normalized.Agents, SchedulerAgentIdentity.Matches);

        Assert.True(migrated.Permissions.CanScheduleWork);
        using (var migratedConfiguration = JsonDocument.Parse(migrated.ConfigurationJson))
        {
            Assert.Equal(
                "preserve",
                migratedConfiguration.RootElement.GetProperty("customerSchedulerPolicy").GetString());
            Assert.Equal(
                SchedulerAgentIdentity.CurrentSchedulingAccessVersion,
                migratedConfiguration.RootElement
                    .GetProperty(SchedulerAgentIdentity.SchedulingAccessVersionPropertyName)
                    .GetString());
        }

        var normalizedAgain = SandboxWorkspaceSeedFactory.NormalizeCatalog(normalized);
        var idempotent = Assert.Single(normalizedAgain.Agents, SchedulerAgentIdentity.Matches);
        Assert.True(idempotent.Permissions.CanScheduleWork);

        var deliberatelyRemoved = normalizedAgain with
        {
            Agents = normalizedAgain.Agents
                .Select(agent => agent.Id == idempotent.Id
                    ? agent with
                    {
                        Permissions = agent.Permissions with { CanScheduleWork = false }
                    }
                    : agent)
                .ToArray()
        };
        var preservedCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(deliberatelyRemoved);
        var preserved = Assert.Single(preservedCatalog.Agents, SchedulerAgentIdentity.Matches);

        Assert.False(preserved.Permissions.CanScheduleWork);
        using var preservedConfiguration = JsonDocument.Parse(preserved.ConfigurationJson);
        Assert.Equal(
            SchedulerAgentIdentity.CurrentSchedulingAccessVersion,
            preservedConfiguration.RootElement
                .GetProperty(SchedulerAgentIdentity.SchedulingAccessVersionPropertyName)
                .GetString());
    }

    [Fact]
    public async Task Organization_workspace_seeds_playwright_mcp_for_ui_delivery_agents()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var playwrightCapability = Assert.Single(
            capabilities,
            item => item.Kind == CapabilityKind.McpServer &&
                    string.Equals(item.Key, "playwright-local-mcp", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("npx", playwrightCapability.EndpointOrPath);

        using var configuration = JsonDocument.Parse(playwrightCapability.ConfigurationJson);
        var root = configuration.RootElement;
        Assert.Equal("stdio", root.GetProperty("transport").GetString());
        Assert.Equal("npx", root.GetProperty("command").GetString());
        Assert.Equal(".", root.GetProperty("workingDirectory").GetString());
        Assert.Equal("newlineDelimitedJson", root.GetProperty("messageFraming").GetString());
        Assert.Equal(120, root.GetProperty("timeoutSeconds").GetInt32());
        Assert.Equal("NeverRequire", root.GetProperty("approvalMode").GetString());
        var arguments = root.GetProperty("arguments")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();
        Assert.Contains(arguments, item => string.Equals(item, "--yes", StringComparison.Ordinal));
        Assert.Contains(arguments, item => string.Equals(item, "@playwright/mcp@0.0.78", StringComparison.Ordinal));
        var allowedTools = root.GetProperty("allowedTools")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
        Assert.Contains("browser_navigate", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_take_screenshot", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_snapshot", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_console_messages", allowedTools, StringComparer.OrdinalIgnoreCase);

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));

        Assert.Contains(qaAgent.Capabilities, item => item.CapabilityId == playwrightCapability.Id);
        Assert.Contains(programmingAgent.Capabilities, item => item.CapabilityId == playwrightCapability.Id);
    }

    [Fact]
    public async Task Organization_workspace_seeds_openai_image_generation_provider()
    {
        const string apiKeyEnvironmentVariable = "OPENAI_API_KEY";
        var originalApiKey = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable);
        Environment.SetEnvironmentVariable(apiKeyEnvironmentVariable, "integration-test-openai-key");
        try
        {
            await using var application = await TestApplication.CreateAsync();
            await using var scope = application.Services.CreateAsyncScope();
            var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

            var providers = await workspaceService.ListProvidersAsync();
            var imageProvider = Assert.Single(
                providers,
                item => item.Kind == ProviderKind.OpenAi &&
                        item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                        string.Equals(item.Name, "OpenAI image generation", StringComparison.Ordinal));
            var service = new ProviderProfileService();
            var matrix = service.ResolveFeatureMatrix(imageProvider);

            Assert.Equal("https://api.openai.com/v1", imageProvider.BaseUrl);
            Assert.StartsWith(
                "secret:",
                imageProvider.ApiKeyEnvironmentVariable,
                StringComparison.Ordinal);
            Assert.True(
                Guid.TryParse(
                    imageProvider.ApiKeyEnvironmentVariable["secret:".Length..],
                    out _));
            Assert.Equal(OpenAiModelIds.GptImage2, imageProvider.DefaultModel);
            Assert.False(imageProvider.SupportsTools);
            Assert.Contains(OpenAiModelIds.GptImage2, imageProvider.SuggestedModels, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(OpenAiModelIds.GptImage1Mini, imageProvider.SuggestedModels, StringComparer.OrdinalIgnoreCase);
            Assert.True(matrix.SupportsImageGeneration);
        }
        finally
        {
            Environment.SetEnvironmentVariable(apiKeyEnvironmentVariable, originalApiKey);
        }
    }

    [Fact]
    public async Task Organization_workspace_migrates_exact_legacy_managed_openai_image_model()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var beforeMigration = await ConfigureLegacyManagedOpenAiImageProviderAsync(dbContextFactory);

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        var afterFirstBootstrap = await ReadManagedOpenAiImageProviderStateAsync(dbContextFactory);
        Assert.Equal(
            beforeMigration with
            {
                DefaultModel = OpenAiModelIds.GptImage2,
                ConcurrencyToken = afterFirstBootstrap.ConcurrencyToken
            },
            afterFirstBootstrap);

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        Assert.Equal(
            afterFirstBootstrap,
            await ReadManagedOpenAiImageProviderStateAsync(dbContextFactory));
    }

    [Fact]
    public async Task Organization_workspace_preserves_custom_managed_openai_image_model_override()
    {
        const string CustomModel = "customer-image-model-v7";
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await SetManagedOpenAiImageProviderModelAsync(dbContextFactory, CustomModel);

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        Assert.Equal(
            CustomModel,
            await ReadManagedOpenAiImageProviderModelAsync(dbContextFactory));
    }

    [Fact]
    public async Task Organization_workspace_upgrades_managed_local_ollama_structured_output_capability()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var provider = await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
                .SingleAsync(item => item.Name == "Local Ollama");
            provider.SupportsStructuredOutput = false;
            await dbContext.SaveChangesAsync();
        }

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var provider = await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
                .SingleAsync(item => item.Name == "Local Ollama");
            Assert.True(provider.SupportsStructuredOutput);
        }

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var provider = await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
                .SingleAsync(item => item.Name == "Local Ollama");
            Assert.True(provider.SupportsStructuredOutput);
        }
    }

    [Theory]
    [InlineData(OpenAiModelIds.GptImage1Mini, OpenAiModelIds.GptImage2)]
    [InlineData("GPT-IMAGE-1-MINI", "GPT-IMAGE-1-MINI")]
    [InlineData("customer-image-model-v7", "customer-image-model-v7")]
    public void Catalog_normalization_migrates_only_legacy_managed_openai_image_model(
        string configuredModel,
        string expectedModel)
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededProvider = Assert.Single(
            seed.Providers,
            provider => provider.Purpose == ProviderProfilePurpose.ImageGeneration &&
                        string.Equals(provider.Name, "OpenAI image generation", StringComparison.Ordinal));
        Assert.Equal(OpenAiModelIds.GptImage2, seededProvider.DefaultModel);
        Assert.Equal([OpenAiModelIds.GptImage2], seededProvider.SuggestedModels);
        var catalog = seed.ToCatalog() with
        {
            Providers = seed.Providers
                .Select(provider => provider.Id == seededProvider.Id
                    ? provider with
                    {
                        DefaultModel = configuredModel,
                        SuggestedModels = [configuredModel, OpenAiModelIds.GptImage1Mini]
                    }
                    : provider)
                .ToArray()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);

        var normalizedProvider = Assert.Single(
            normalized.Providers,
            provider => provider.Id == seededProvider.Id);
        Assert.Equal(expectedModel, normalizedProvider.DefaultModel);
        Assert.Contains(OpenAiModelIds.GptImage2, normalizedProvider.SuggestedModels, StringComparer.OrdinalIgnoreCase);
        if (string.Equals(configuredModel, OpenAiModelIds.GptImage1Mini, StringComparison.Ordinal))
        {
            Assert.DoesNotContain(OpenAiModelIds.GptImage1Mini, normalizedProvider.SuggestedModels, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains(configuredModel, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void Catalog_normalization_uses_stable_image_seed_identity_after_provider_rename()
    {
        const string RenamedProvider = "Customer-renamed image provider";
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededProvider = Assert.Single(
            seed.Providers,
            provider => provider.Purpose == ProviderProfilePurpose.ImageGeneration &&
                        string.Equals(provider.Name, "OpenAI image generation", StringComparison.Ordinal));
        var catalog = seed.ToCatalog() with
        {
            Providers = seed.Providers
                .Select(provider => provider.Id == seededProvider.Id
                    ? provider with
                    {
                        Name = RenamedProvider,
                        Kind = ProviderKind.Ollama,
                        DefaultModel = OpenAiModelIds.GptImage1Mini,
                        SuggestedModels = [OpenAiModelIds.GptImage1Mini]
                    }
                    : provider)
                .ToArray()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);

        var normalizedProvider = Assert.Single(
            normalized.Providers,
            provider => provider.Id == seededProvider.Id);
        Assert.Equal(RenamedProvider, normalizedProvider.Name);
        Assert.Equal(ProviderKind.Ollama, normalizedProvider.Kind);
        Assert.Equal(OpenAiModelIds.GptImage2, normalizedProvider.DefaultModel);
        Assert.DoesNotContain(OpenAiModelIds.GptImage1Mini, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
        Assert.Contains(OpenAiModelIds.GptImage2, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
    }

    [Fact]
    public void Catalog_normalization_preserves_legacy_image_configuration_for_same_name_provider_with_different_id()
    {
        const string CustomSuggestedModel = "customer-image-model-v7";
        var customProviderId = Guid.Parse("07C5DF13-90AD-4AED-9F2C-DB2F38458E16");
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededProvider = Assert.Single(
            seed.Providers,
            provider => provider.Purpose == ProviderProfilePurpose.ImageGeneration &&
                        string.Equals(provider.Name, "OpenAI image generation", StringComparison.Ordinal));
        var catalog = seed.ToCatalog() with
        {
            Providers = seed.Providers
                .Select(provider => provider.Id == seededProvider.Id
                    ? provider with
                    {
                        Id = customProviderId,
                        DefaultModel = OpenAiModelIds.GptImage1Mini,
                        SuggestedModels = [OpenAiModelIds.GptImage1Mini, CustomSuggestedModel]
                    }
                    : provider)
                .ToArray()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);

        var normalizedProvider = Assert.Single(
            normalized.Providers,
            provider => provider.Id == customProviderId);
        Assert.Equal(OpenAiModelIds.GptImage1Mini, normalizedProvider.DefaultModel);
        Assert.Contains(OpenAiModelIds.GptImage1Mini, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
        Assert.Contains(CustomSuggestedModel, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
        Assert.Contains(OpenAiModelIds.GptImage2, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
        Assert.DoesNotContain(normalized.Providers, provider => provider.Id == seededProvider.Id);
    }

    [Fact]
    public void Catalog_normalization_keeps_name_based_managed_chat_merge_behavior()
    {
        const string ExistingSuggestedModel = "customer-chat-model-v7";
        var customProviderId = Guid.Parse("58EB9AFE-536D-472D-8B3E-3E71FD6E4CAA");
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededProvider = Assert.Single(
            seed.Providers,
            provider => provider.Purpose == ProviderProfilePurpose.Chat &&
                        string.Equals(provider.Name, "OpenAI default", StringComparison.Ordinal));
        var catalog = seed.ToCatalog() with
        {
            Providers = seed.Providers
                .Select(provider => provider.Id == seededProvider.Id
                    ? provider with
                    {
                        Id = customProviderId,
                        DefaultModel = ExistingSuggestedModel,
                        SuggestedModels = [ExistingSuggestedModel]
                    }
                    : provider)
                .ToArray()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);

        var normalizedProvider = Assert.Single(
            normalized.Providers,
            provider => provider.Id == customProviderId);
        Assert.Equal(seededProvider.DefaultModel, normalizedProvider.DefaultModel);
        Assert.Contains(seededProvider.DefaultModel, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
        Assert.Contains(ExistingSuggestedModel, normalizedProvider.SuggestedModels, StringComparer.Ordinal);
        Assert.DoesNotContain(normalized.Providers, provider => provider.Id == seededProvider.Id);
    }

    [Fact]
    public async Task Organization_workspace_seeds_local_comfyui_flux_image_generation_provider()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var imageProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.ComfyUi &&
                    item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                    string.Equals(item.Name, ComfyUiFluxProviderDefaults.ProviderName, StringComparison.Ordinal));
        var service = new ProviderProfileService();
        var matrix = service.ResolveFeatureMatrix(imageProvider);
        using var configuration = JsonDocument.Parse(imageProvider.ConfigurationJson);
        var root = configuration.RootElement;

        Assert.Equal(ComfyUiFluxProviderDefaults.DefaultBaseUrl, imageProvider.BaseUrl);
        Assert.Equal(ComfyUiFluxProviderDefaults.DefaultModel, imageProvider.DefaultModel);
        Assert.True(imageProvider.IsEnabled);
        Assert.True(imageProvider.IsPrivateProvider);
        Assert.False(imageProvider.SupportsTools);
        Assert.Contains(ComfyUiFluxProviderDefaults.DefaultModel, imageProvider.SuggestedModels, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(ComfyUiFluxProviderDefaults.PositivePromptNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.PositivePromptNodeId).GetString());
        Assert.Equal(ComfyUiFluxProviderDefaults.SamplerNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.SamplerNodeId).GetString());
        Assert.Equal(ComfyUiFluxProviderDefaults.LatentSizeNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.WidthNodeId).GetString());
        Assert.Equal(ComfyUiFluxProviderDefaults.OutputNodeId, root.GetProperty(ComfyUiProviderConfigurationKeys.OutputNodeId).GetString());
        Assert.Contains("flux1-dev.safetensors", root.GetProperty(ComfyUiProviderConfigurationKeys.WorkflowTemplateJson).GetString(), StringComparison.Ordinal);
        Assert.True(matrix.SupportsImageGeneration);
        Assert.False(matrix.SupportsFunctionTools);
    }

    [Fact]
    public async Task Organization_workspace_seeds_tagged_openai_and_local_ollama_provider_catalog()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefault = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var localOllama = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Local Ollama", StringComparison.Ordinal));
        var remoteOllama = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));
        var seededOpenAiDefault = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));

        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, openAiDefault.DefaultModel);
        Assert.All(
            OpenAiModelIds.Gpt56Models,
            model =>
            {
                Assert.Contains(model, openAiDefault.SuggestedModels, StringComparer.OrdinalIgnoreCase);
                Assert.Contains(model, seededOpenAiDefault.SuggestedModels, StringComparer.OrdinalIgnoreCase);
                Assert.True(ProviderPricingDefaults.TryFindPrice(seededOpenAiDefault.ModelPrices, model, out var price));
                Assert.True(price.HasConfiguredStandardPrice);
            });
        Assert.Contains("openai", openAiDefault.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("responses", openAiDefault.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("http://127.0.0.1:11434", localOllama.BaseUrl);
        Assert.Equal("llama3.1", localOllama.DefaultModel);
        Assert.Contains("ollama", localOllama.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("local", localOllama.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("remote", remoteOllama.Tags, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Capability_catalog_save_normalizes_and_persists_tags()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilityId = await workspaceService.SaveCapabilityAsync(new CapabilityEditorModel
        {
            Kind = CapabilityKind.McpServer,
            Key = "tagged-mcp-proof",
            Name = "Tagged MCP Proof",
            Description = "Capability tag persistence proof.",
            EndpointOrPath = "npx",
            ConfigurationJson = """{"transport":"logical"}""",
            Tags = ["Economy", "#mcp", "economy"]
        });

        var savedCapability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Id == capabilityId);

        Assert.Equal(["economy", "mcp"], savedCapability.Tags);
    }

    [Fact]
    public async Task Capability_catalog_save_rejects_a_stale_editor_fingerprint_atomically()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var capabilityId = await workspaceService.SaveCapabilityAsync(new CapabilityEditorModel
        {
            Kind = CapabilityKind.Skill,
            Key = "capability-concurrency-proof",
            Name = "Capability Concurrency Proof",
            Description = "Original description.",
            EndpointOrPath = "inline://capability-concurrency-proof",
            ConfigurationJson = "{}"
        });
        var firstEditor = await workspaceService.GetCapabilityEditorAsync(capabilityId);
        var staleEditor = await workspaceService.GetCapabilityEditorAsync(capabilityId);
        Assert.False(string.IsNullOrWhiteSpace(firstEditor.ExpectedFingerprint));
        Assert.Equal(firstEditor.ExpectedFingerprint, staleEditor.ExpectedFingerprint);

        firstEditor.Description = "First accepted update.";
        await workspaceService.SaveCapabilityAsync(firstEditor);
        staleEditor.Description = "Stale overwrite.";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workspaceService.SaveCapabilityAsync(staleEditor));
        var saved = await workspaceService.GetCapabilityEditorAsync(capabilityId);
        Assert.Equal("First accepted update.", saved.Description);
    }

    [Fact]
    public async Task Organization_workspace_seeds_screenshot_agent_templates_with_required_access()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefaultProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var openAiImageProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                    string.Equals(item.Name, "OpenAI image generation", StringComparison.Ordinal));
        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var agentsWithTemplates = await workspaceService.ListAgentsAsync(includeTemplates: true);
        var activeAgents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var captureTemplate = Assert.Single(
            agentsWithTemplates,
            item => string.Equals(item.Name, "App Screenshot Capture Agent Template", StringComparison.Ordinal));
        var reviewTemplate = Assert.Single(
            agentsWithTemplates,
            item => string.Equals(item.Name, "Screenshot Review Storage Agent Template", StringComparison.Ordinal));
        var layoutTemplate = Assert.Single(
            agentsWithTemplates,
            item => string.Equals(item.Name, "Layout Image Generation Agent Template", StringComparison.Ordinal));

        Assert.DoesNotContain(activeAgents, item => item.Id == captureTemplate.Id);
        Assert.DoesNotContain(activeAgents, item => item.Id == reviewTemplate.Id);
        Assert.DoesNotContain(activeAgents, item => item.Id == layoutTemplate.Id);

        Assert.True(captureTemplate.IsTemplate);
        Assert.Equal("app-screenshot-capture-agent", captureTemplate.TemplateKey);
        AssertOpenAiBacked(captureTemplate, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertHasCapabilities(
            captureTemplate,
            capabilityIdsByKey["playwright-local-mcp"],
            capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"],
            capabilityIdsByKey["dotnet-app-delivery-inline-skill"],
            capabilityIdsByKey["blazor-ssr-delivery-inline-skill"],
            capabilityIdsByKey["candoitall-watch-playwright-loop"],
            capabilityIdsByKey["workspace-create-directory"],
            capabilityIdsByKey["workspace-write-file"],
            capabilityIdsByKey["workspace-dotnet-run"],
            capabilityIdsByKey["workspace-dotnet-stop"],
            capabilityIdsByKey["workspace-pwsh-run-script"]);

        var captureEditor = await workspaceService.GetAgentEditorAsync(captureTemplate.Id);
        Assert.True(captureEditor.ProjectStructureAccess.CanRead);
        Assert.False(captureEditor.ProjectStructureAccess.CanWrite);
        Assert.True(captureEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(captureEditor.ProcessAccess.CanRead);
        Assert.False(captureEditor.ProcessAccess.CanWrite);
        Assert.True(captureEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, captureEditor.WorkspaceToolAccess.Profile);
        Assert.True(captureEditor.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(captureEditor.WorkspaceToolAccess.CanRunLocalScripts);
        Assert.False(captureEditor.ImageGenerationAccess.CanGenerateImages);
        Assert.False(captureEditor.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.Contains("Start the application once", captureEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Use Playwright MCP", captureEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Use only the stack-specific startup and cleanup capabilities explicitly declared by the current launch contract", captureEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Use the declared executable entrypoint exactly", captureEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not substitute a solution, product root, project directory, or other container path", captureEditor.Instructions, StringComparison.Ordinal);

        Assert.True(reviewTemplate.IsTemplate);
        Assert.Equal("screenshot-review-storage-agent", reviewTemplate.TemplateKey);
        AssertOpenAiBacked(reviewTemplate, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertHasCapabilities(
            reviewTemplate,
            capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"],
            capabilityIdsByKey["frontend-skill"],
            capabilityIdsByKey["workspace-create-directory"],
            capabilityIdsByKey["workspace-write-file"],
            capabilityIdsByKey["workspace-inspect-image"],
            capabilityIdsByKey["workspace-analyze-image"],
            capabilityIdsByKey["workspace-analyze-images"],
            capabilityIdsByKey["workspace-source-rag"]);

        var reviewEditor = await workspaceService.GetAgentEditorAsync(reviewTemplate.Id);
        Assert.True(reviewEditor.ProjectStructureAccess.CanRead);
        Assert.True(reviewEditor.ProjectStructureAccess.CanWrite);
        Assert.True(reviewEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(reviewEditor.ProcessAccess.CanRead);
        Assert.True(reviewEditor.ProcessAccess.CanWrite);
        Assert.True(reviewEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, reviewEditor.WorkspaceToolAccess.Profile);
        Assert.True(reviewEditor.WorkspaceToolAccess.CanReadStorage);
        Assert.True(reviewEditor.WorkspaceToolAccess.CanWriteStorage);
        Assert.True(reviewEditor.WorkspaceToolAccess.AllowAllStorageCatalogs);
        Assert.False(reviewEditor.ImageGenerationAccess.CanGenerateImages);
        Assert.True(reviewEditor.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.Contains("project_structure_asset_create", reviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("objectType` `ImageAsset", reviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("source visual target ImageAsset", reviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Visual target comparison", reviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", reviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("sourceWorkspacePath", reviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("invalid base64", reviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);

        Assert.True(layoutTemplate.IsTemplate);
        Assert.Equal("layout-image-generation-agent", layoutTemplate.TemplateKey);
        AssertOpenAiBacked(layoutTemplate, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertHasCapabilities(
            layoutTemplate,
            capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"],
            capabilityIdsByKey["frontend-skill"],
            capabilityIdsByKey["workspace-create-directory"],
            capabilityIdsByKey["workspace-write-file"],
            capabilityIdsByKey["workspace-source-rag"]);

        var layoutEditor = await workspaceService.GetAgentEditorAsync(layoutTemplate.Id);
        Assert.True(layoutEditor.ProjectStructureAccess.CanRead);
        Assert.True(layoutEditor.ProjectStructureAccess.CanWrite);
        Assert.True(layoutEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(layoutEditor.ProcessAccess.CanRead);
        Assert.True(layoutEditor.ProcessAccess.CanWrite);
        Assert.True(layoutEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, layoutEditor.WorkspaceToolAccess.Profile);
        Assert.True(layoutEditor.ImageGenerationAccess.CanGenerateImages);
        Assert.Equal(openAiImageProvider.Id, layoutEditor.ImageGenerationAccess.PreferredProviderProfileId);
        Assert.Equal(OpenAiModelIds.GptImage2, layoutEditor.ImageGenerationAccess.DefaultModel);
        Assert.True(layoutEditor.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.Contains("sourceProjectAssets", layoutEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("image_generation_create", layoutEditor.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_default_integrated_agents_do_not_attach_project_structure_or_processes_mcp_capabilities()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        Assert.DoesNotContain(
            capabilities,
            item => item.Kind == CapabilityKind.McpServer &&
                    string.Equals(item.EndpointOrPath, "CanDoItAll.Mcp.Processes", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            capabilities,
            item => item.Kind == CapabilityKind.McpServer &&
                    string.Equals(item.EndpointOrPath, "CanDoItAll.Mcp.ProjectStructure", StringComparison.OrdinalIgnoreCase));

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        foreach (var agentName in new[]
                 {
                     "Portfolio Architect",
                     "Programming Workspace Analyst",
                     "Delivery QA Observer",
                     "Code Review Lead",
                     "UI Review Lead",
                     "Security Reviewer",
                     "Release Readiness Manager",
                     "Delivery Manager",
                     "Research Deep Dive Analyst",
                     ".NET Solution Architect",
                     ".NET Application Developer",
                     "Blazor Application Developer",
                     ".NET QA Review Lead",
                     "JavaScript Solution Architect",
                     "JavaScript Application Developer",
                     "JavaScript QA Review Lead",
                     "Business Strategist",
                     "Financial Strategist",
                     "Marketing Specialist",
                     "Mail Triage Analyst",
                     "Spreadsheet Analyst"
                 })
        {
            var agent = Assert.Single(agents, item => string.Equals(item.Name, agentName, StringComparison.Ordinal));
            Assert.DoesNotContain(
                agent.Capabilities,
                item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                agent.Capabilities,
                item => item.Kind == CapabilityKind.McpServer &&
                        item.CapabilityKey.Contains("process", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Serious_delivery_agents_seed_internal_project_structure_and_process_access_after_mcp_removal()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var architect = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var architectEditor = await workspaceService.GetAgentEditorAsync(architect.Id);
        Assert.True(architectEditor.ProjectStructureAccess.CanRead);
        Assert.False(architectEditor.ProjectStructureAccess.CanWrite);
        Assert.True(architectEditor.ProjectStructureAccess.CanWriteNonTaskStructure);
        Assert.False(architectEditor.ProjectStructureAccess.CanWriteTasks);
        Assert.True(architectEditor.ProjectStructureAccess.CanCreateProjects);
        Assert.True(architectEditor.ProjectStructureAccess.CanCreateSubprojects);
        Assert.True(architectEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(architectEditor.ProcessAccess.CanRead);
        Assert.False(architectEditor.ProcessAccess.CanWrite);
        Assert.True(architectEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Contains("project_structure_node_delete", architectEditor.Instructions, StringComparison.Ordinal);

        var deliveryManager = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Delivery Manager", StringComparison.Ordinal));
        var deliveryManagerEditor = await workspaceService.GetAgentEditorAsync(deliveryManager.Id);
        Assert.True(deliveryManagerEditor.ProjectStructureAccess.CanRead);
        Assert.True(deliveryManagerEditor.ProjectStructureAccess.CanWrite);
        Assert.True(deliveryManagerEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(deliveryManagerEditor.ProcessAccess.CanRead);
        Assert.False(deliveryManagerEditor.ProcessAccess.CanWrite);
        Assert.True(deliveryManagerEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.BusinessAnalysis, deliveryManagerEditor.WorkspaceToolAccess.Profile);

        var financialStrategist = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        var financialStrategistEditor = await workspaceService.GetAgentEditorAsync(financialStrategist.Id);
        Assert.True(financialStrategistEditor.ProjectStructureAccess.CanRead);
        Assert.False(financialStrategistEditor.ProjectStructureAccess.CanWrite);
        Assert.False(financialStrategistEditor.ProjectStructureAccess.CanWriteNonTaskStructure);
        Assert.False(financialStrategistEditor.ProjectStructureAccess.CanWriteTasks);
        Assert.True(financialStrategistEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(financialStrategistEditor.ProcessAccess.CanRead);
        Assert.False(financialStrategistEditor.ProcessAccess.CanWrite);
        Assert.True(financialStrategistEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.BusinessAnalysis, financialStrategistEditor.WorkspaceToolAccess.Profile);
        Assert.True(financialStrategistEditor.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.True(financialStrategistEditor.ImageGenerationAccess.CanGenerateImages);
        Assert.True(financialStrategistEditor.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.Equal(OpenAiModelIds.GptImage2, financialStrategistEditor.ImageGenerationAccess.DefaultModel);
        Assert.True(financialStrategistEditor.Permissions.AutoApproveExternalCallsByDefault);
        Assert.Contains("project_structure_read", financialStrategistEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_content_get", financialStrategistEditor.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("project_structure_node_create", financialStrategistEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_convert_document", financialStrategistEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_spreadsheet", financialStrategistEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_spreadsheet_function_catalog", financialStrategistEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("image_generation_create", financialStrategistEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", financialStrategistEditor.Instructions, StringComparison.Ordinal);

        var qaObserver = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var qaObserverEditor = await workspaceService.GetAgentEditorAsync(qaObserver.Id);
        Assert.True(qaObserverEditor.ProjectStructureAccess.CanRead);
        Assert.True(qaObserverEditor.ProjectStructureAccess.CanWrite);
        Assert.True(qaObserverEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(qaObserverEditor.ProcessAccess.CanRead);
        Assert.False(qaObserverEditor.ProcessAccess.CanWrite);
        Assert.True(qaObserverEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, qaObserverEditor.WorkspaceToolAccess.Profile);

        foreach (var agentName in new[]
                 {
                     "Programming Workspace Analyst",
                     "Code Review Lead",
                     "UI Review Lead",
                     "Security Reviewer",
                     "Release Readiness Manager",
                     "Research Deep Dive Analyst",
                     ".NET Solution Architect",
                     ".NET Application Developer",
                     "Blazor Application Developer",
                     ".NET QA Review Lead",
                     "JavaScript Solution Architect",
                     "JavaScript Application Developer",
                     "JavaScript QA Review Lead",
                     "Business Strategist",
                     "Marketing Specialist",
                     "Mail Triage Analyst",
                     "Spreadsheet Analyst"
                 })
        {
            var agent = Assert.Single(
                await workspaceService.ListAgentsAsync(includeTemplates: false),
                item => string.Equals(item.Name, agentName, StringComparison.Ordinal));
            var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
            Assert.True(editor.ProjectStructureAccess.CanRead);
            Assert.False(editor.ProjectStructureAccess.CanWrite);
            Assert.True(editor.ProjectStructureAccess.AllowAllProjects);
            Assert.True(editor.ProcessAccess.CanRead);
            Assert.False(editor.ProcessAccess.CanWrite);
            Assert.True(editor.ProcessAccess.AllowAllDefinitions);
        }
    }

    [Fact]
    public async Task Organization_workspace_seeds_workspace_source_rag_with_generated_runtime_noise_excluded()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Rag &&
                    string.Equals(item.Key, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));

        using var configuration = JsonDocument.Parse(capability.ConfigurationJson);
        var excludePaths = configuration.RootElement
            .GetProperty("excludePaths")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

        Assert.Contains(".playwright-mcp", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("process-runs", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("data", excludePaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stale_workspace_source_rag_capability_is_refreshed_to_exclude_generated_runtime_noise()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);

        await store.UpdateCatalogAsync(catalog =>
        {
            var downgradedCapabilities = catalog.Capabilities
                .Select(capability =>
                {
                    if (!string.Equals(capability.Key, "workspace-source-rag", StringComparison.OrdinalIgnoreCase))
                    {
                        return capability;
                    }

                    return capability with
                    {
                        ConfigurationJson = JsonSerializer.Serialize(new
                        {
                            ragRoot = ".",
                            extensions = new[] { ".cs", ".md" },
                            excludePaths = new[] { "artifacts", "output" },
                            searchTime = "BeforeAIInvoke",
                            maxResults = 5
                        })
                    };
                })
                .ToList();

            return catalog with
            {
                Capabilities = downgradedCapabilities
            };
        });

        var refreshedCatalog = await store.LoadCatalogAsync();
        var refreshedCapability = Assert.Single(
            refreshedCatalog.Capabilities,
            item => string.Equals(item.Key, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        using var configuration = JsonDocument.Parse(refreshedCapability.ConfigurationJson);
        var excludePaths = configuration.RootElement
            .GetProperty("excludePaths")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

        Assert.Contains(".playwright-mcp", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("process-runs", excludePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("data", excludePaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Organization_workspace_seeds_blazor_ssr_delivery_skill_with_external_target_rules()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Skill &&
                    string.Equals(item.Key, "blazor-ssr-delivery-inline-skill", StringComparison.OrdinalIgnoreCase));
        var instructions = ReadInlineSkillInstructions(capability.ConfigurationJson);

        Assert.Contains("If the project structure or attached step materials name a concrete output directory", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/<drive>/...", instructions, StringComparison.Ordinal);
        Assert.Contains("do not scaffold a parallel copy under `artifacts/...`, `output/...`, or another generated implementation folder", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scaffold directly into it instead of adding an extra nested", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Before any scaffold call", instructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", instructions, StringComparison.Ordinal);
        Assert.Contains("Capture screenshot, browser_snapshot or browser_evaluate state output, and browser_console_messages as current-run evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("When writing xUnit tests, include a visible `using Xunit;`", instructions, StringComparison.Ordinal);
        Assert.Contains("custom route backed only by scaffold-default `app.css` and layout CSS", instructions, StringComparison.Ordinal);
        Assert.Contains("custom class names without matching loaded styles", instructions, StringComparison.Ordinal);
        Assert.Contains("provider-native filenames before managed artifact import", instructions, StringComparison.Ordinal);
        Assert.Contains("one run-app proof node, one run-tests proof node, and one manager summary node", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_seeds_concrete_deliverable_delivery_skill_as_generic_contract()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Skill &&
                    string.Equals(item.Key, "concrete-deliverable-delivery-inline-skill", StringComparison.OrdinalIgnoreCase));
        var instructions = ReadInlineSkillInstructions(capability.ConfigurationJson);

        Assert.Contains("any process step that creates, repairs, validates, or summarizes a concrete deliverable", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A deliverable can be an app, service, API", instructions, StringComparison.Ordinal);
        Assert.Contains("Do not reuse sample topics, older generated apps", instructions, StringComparison.Ordinal);
        Assert.Contains("Use technology-specific skills and tools only after the current files or step contract justify them", instructions, StringComparison.Ordinal);
        Assert.Contains("For documents, render/export/open the produced file", instructions, StringComparison.Ordinal);
        Assert.Contains("For spreadsheets, inspect workbook structure", instructions, StringComparison.Ordinal);
        Assert.Contains("Final delivery order is strict", instructions, StringComparison.Ordinal);
        Assert.Contains("Do not claim completion with chat-only evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("computed styles apply to the primary surface", instructions, StringComparison.Ordinal);
        Assert.Contains("product-specific class names but the accepted screenshot or state output shows only unstyled DOM", instructions, StringComparison.Ordinal);
        Assert.Contains("provider-native filenames before managed artifact import", instructions, StringComparison.Ordinal);
        Assert.Contains("store accepted screenshots as ImageAsset nodes or record the exact project-structure asset handoff", instructions, StringComparison.Ordinal);
        Assert.Contains("one run-app proof node, one run-tests proof node, and one manager summary node", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_seeds_dotnet_app_delivery_skill_with_process_visible_browser_proof()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capability = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => item.Kind == CapabilityKind.Skill &&
                    string.Equals(item.Key, "dotnet-app-delivery-inline-skill", StringComparison.OrdinalIgnoreCase));
        var instructions = ReadInlineSkillInstructions(capability.ConfigurationJson);

        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", instructions, StringComparison.Ordinal);
        Assert.Contains("workspace file tools cannot see the managed browser folder during the same attempt", instructions, StringComparison.Ordinal);
        Assert.Contains("When writing xUnit tests, include a visible `using Xunit;`", instructions, StringComparison.Ordinal);
        Assert.Contains("domain-specific classes but only stock template CSS", instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_stop", instructions, StringComparison.Ordinal);
        Assert.Contains("cleanup.json", instructions, StringComparison.Ordinal);
        Assert.Contains("DotNetAppProjectFileAlias", instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", instructions, StringComparison.Ordinal);
        Assert.Contains("targetPath must be a runnable project file", instructions, StringComparison.Ordinal);
        Assert.Contains("one run-app proof node, one run-tests proof node, and one manager summary node", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_workspace_seeds_serious_delivery_agents_on_openai_with_required_skills()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefaultProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var capabilities = await workspaceService.ListCapabilitiesAsync();
        Assert.DoesNotContain(capabilities, item => string.Equals(item.Key, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(capabilities, item => string.Equals(item.Key, "workspace-inspector-plugin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(capabilities, item => string.Equals(item.Key, "candoitall-bundle-workflow", StringComparison.OrdinalIgnoreCase));
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var playwrightCapabilityId = capabilityIdsByKey["playwright-local-mcp"];
        var codeanalyticsCapabilityId = capabilityIdsByKey["candoitall-codeanalytics-mcp"];
        var componentsCapabilityId = capabilityIdsByKey["candoitall-components-mcp"];
        var frontendThemeCapabilityId = capabilityIdsByKey["candoitall-frontend-theme"];
        var frontendSkillCapabilityId = capabilityIdsByKey["frontend-skill"];
        var playwrightWorkflowCapabilityId = capabilityIdsByKey["candoitall-watch-playwright-loop"];
        var spreadsheetCapabilityId = capabilityIdsByKey["spreadsheet-skill"];
        var runTestsCapabilityId = capabilityIdsByKey["run-tests"];
        var mstestCapabilityId = capabilityIdsByKey["writing-mstest-tests"];
        var concreteDeliverableDeliveryCapabilityId = capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"];
        var dotnetAppDeliveryCapabilityId = capabilityIdsByKey["dotnet-app-delivery-inline-skill"];
        var blazorSsrDeliveryCapabilityId = capabilityIdsByKey["blazor-ssr-delivery-inline-skill"];
        var workspaceSourceRagCapabilityId = capabilityIdsByKey["workspace-source-rag"];
        var architectureSourceRagCapabilityId = capabilityIdsByKey["architecture-source-rag"];
        var createDirectoryCapabilityId = capabilityIdsByKey["workspace-create-directory"];
        var writeFileCapabilityId = capabilityIdsByKey["workspace-write-file"];
        var appendFileCapabilityId = capabilityIdsByKey["workspace-append-file"];
        var workspaceDotnetRunCapabilityId = capabilityIdsByKey["workspace-dotnet-run"];
        var workspaceDotnetStopCapabilityId = capabilityIdsByKey["workspace-dotnet-stop"];
        var workspaceDotnetNewCapabilityId = capabilityIdsByKey["workspace-dotnet-new"];
        var pwshRunScriptCapabilityId = capabilityIdsByKey["workspace-pwsh-run-script"];
        var convertDocumentCapabilityId = capabilityIdsByKey["workspace-convert-document"];
        var inspectSpreadsheetCapabilityId = capabilityIdsByKey["workspace-inspect-spreadsheet"];
        var spreadsheetSummaryCapabilityId = capabilityIdsByKey["workspace-spreadsheet-summary"];
        var readSpreadsheetCellCapabilityId = capabilityIdsByKey["workspace-read-spreadsheet-cell"];
        var readSpreadsheetRangeCapabilityId = capabilityIdsByKey["workspace-read-spreadsheet-range"];
        var writeSpreadsheetCapabilityId = capabilityIdsByKey["workspace-write-spreadsheet"];
        var spreadsheetFunctionCatalogCapabilityId = capabilityIdsByKey["workspace-spreadsheet-function-catalog"];
        var inspectImageCapabilityId = capabilityIdsByKey["workspace-inspect-image"];
        var analyzeImageCapabilityId = capabilityIdsByKey["workspace-analyze-image"];
        var analyzeImagesCapabilityId = capabilityIdsByKey["workspace-analyze-images"];
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var architectAgent = Assert.Single(agents, item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var codeReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var uiReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var securityReviewerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var releaseManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));
        var deliveryManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery Manager", StringComparison.Ordinal));
        var dotnetArchitectAgent = Assert.Single(agents, item => string.Equals(item.Name, ".NET Solution Architect", StringComparison.Ordinal));
        var dotnetDeveloperAgent = Assert.Single(agents, item => string.Equals(item.Name, ".NET Application Developer", StringComparison.Ordinal));
        var blazorDeveloperAgent = Assert.Single(agents, item => string.Equals(item.Name, "Blazor Application Developer", StringComparison.Ordinal));
        var dotnetQaAgent = Assert.Single(agents, item => string.Equals(item.Name, ".NET QA Review Lead", StringComparison.Ordinal));
        var runtimeFailureAnalystAgent = Assert.Single(agents, item => string.Equals(item.Name, ".NET Runtime Failure Analyst", StringComparison.Ordinal));
        var javascriptArchitectAgent = Assert.Single(agents, item => string.Equals(item.Name, "JavaScript Solution Architect", StringComparison.Ordinal));
        var javascriptDeveloperAgent = Assert.Single(agents, item => string.Equals(item.Name, "JavaScript Application Developer", StringComparison.Ordinal));
        var javascriptQaAgent = Assert.Single(agents, item => string.Equals(item.Name, "JavaScript QA Review Lead", StringComparison.Ordinal));
        var businessStrategistAgent = Assert.Single(agents, item => string.Equals(item.Name, "Business Strategist", StringComparison.Ordinal));
        var researchAgent = Assert.Single(agents, item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal));
        var financialStrategistAgent = Assert.Single(agents, item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal));
        var marketingSpecialistAgent = Assert.Single(agents, item => string.Equals(item.Name, "Marketing Specialist", StringComparison.Ordinal));

        AssertOpenAiBacked(architectAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(programmingAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(qaAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(codeReviewAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(uiReviewAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(securityReviewerAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(releaseManagerAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(dotnetArchitectAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(dotnetDeveloperAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(blazorDeveloperAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(dotnetQaAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(runtimeFailureAnalystAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(javascriptArchitectAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(javascriptDeveloperAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(javascriptQaAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(businessStrategistAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(financialStrategistAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(marketingSpecialistAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        AssertHasCapabilities(architectAgent, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(programmingAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(qaAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(codeReviewAgent, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(uiReviewAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(securityReviewerAgent, codeanalyticsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(releaseManagerAgent, playwrightCapabilityId, playwrightWorkflowCapabilityId, concreteDeliverableDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(dotnetArchitectAgent, concreteDeliverableDeliveryCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, architectureSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(dotnetDeveloperAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(blazorDeveloperAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(dotnetQaAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(javascriptArchitectAgent, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(javascriptDeveloperAgent, playwrightCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(javascriptQaAgent, playwrightCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
        AssertHasCapabilities(businessStrategistAgent, concreteDeliverableDeliveryCapabilityId, convertDocumentCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);
        AssertHasCapabilities(deliveryManagerAgent, convertDocumentCapabilityId);
        AssertHasCapabilities(researchAgent, convertDocumentCapabilityId);
        AssertHasCapabilities(financialStrategistAgent, spreadsheetCapabilityId, concreteDeliverableDeliveryCapabilityId, convertDocumentCapabilityId, inspectSpreadsheetCapabilityId, spreadsheetSummaryCapabilityId, readSpreadsheetCellCapabilityId, readSpreadsheetRangeCapabilityId, writeSpreadsheetCapabilityId, spreadsheetFunctionCatalogCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId);
        Assert.DoesNotContain(financialStrategistAgent.Capabilities, item => string.Equals(item.CapabilityKey, "provider-native-code-interpreter", StringComparison.OrdinalIgnoreCase));
        AssertHasCapabilities(marketingSpecialistAgent, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, convertDocumentCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId);

        var qaEditor = await workspaceService.GetAgentEditorAsync(qaAgent.Id);
        var dotnetDeveloperEditor = await workspaceService.GetAgentEditorAsync(dotnetDeveloperAgent.Id);
        var blazorDeveloperEditor = await workspaceService.GetAgentEditorAsync(blazorDeveloperAgent.Id);
        var dotnetQaEditor = await workspaceService.GetAgentEditorAsync(dotnetQaAgent.Id);
        Assert.Contains("DotNetAppProjectFileAlias", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("DotNetAppProjectFileAlias", dotnetDeveloperEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("run target must be the runnable project file", dotnetDeveloperEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("DotNetAppProjectFileAlias", blazorDeveloperEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("run target must be the runnable project file", blazorDeveloperEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("DotNetAppProjectFileAlias", dotnetQaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Never pass a `.sln`, `.slnx`", dotnetQaEditor.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain(architectAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(programmingAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dotnetDeveloperAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(blazorDeveloperAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(javascriptDeveloperAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(businessStrategistAgent.Capabilities, item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Organization_workspace_seeds_typed_workspace_tool_profiles_for_delivery_roles()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var programming = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal)).Id);
        var qa = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal)).Id);
        var security = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal)).Id);
        var business = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Business Strategist", StringComparison.Ordinal)).Id);
        var deliveryManager = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Delivery Manager", StringComparison.Ordinal)).Id);
        var research = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal)).Id);

        Assert.Equal(AgentWorkspaceToolProfileKind.SoftwareDevelopment, programming.WorkspaceToolAccess.Profile);
        Assert.True(programming.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(programming.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.True(programming.WorkspaceToolAccess.CanManageWorkspacePaths);

        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, qa.WorkspaceToolAccess.Profile);
        Assert.True(qa.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(qa.WorkspaceToolAccess.CanWriteFiles);
        Assert.False(qa.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.False(qa.WorkspaceToolAccess.CanManageWorkspacePaths);

        Assert.Equal(AgentWorkspaceToolProfileKind.SecurityReview, security.WorkspaceToolAccess.Profile);
        Assert.True(security.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.False(security.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.False(security.WorkspaceToolAccess.CanManageWorkspacePaths);

        Assert.Equal(AgentWorkspaceToolProfileKind.BusinessAnalysis, business.WorkspaceToolAccess.Profile);
        Assert.True(business.WorkspaceToolAccess.CanWriteFiles);
        Assert.True(business.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.False(business.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.False(business.WorkspaceToolAccess.CanScaffoldProjects);

        Assert.Equal(AgentWorkspaceToolProfileKind.BusinessAnalysis, deliveryManager.WorkspaceToolAccess.Profile);
        Assert.True(deliveryManager.WorkspaceToolAccess.CanWriteFiles);
        Assert.True(deliveryManager.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.False(deliveryManager.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.False(deliveryManager.WorkspaceToolAccess.CanScaffoldProjects);

        Assert.Equal(AgentWorkspaceToolProfileKind.ReadOnly, research.WorkspaceToolAccess.Profile);
        Assert.True(research.WorkspaceToolAccess.CanReadFiles);
        Assert.True(research.WorkspaceToolAccess.CanWriteFiles);
        Assert.True(research.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.False(research.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.False(research.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.False(research.WorkspaceToolAccess.CanManageWorkspacePaths);
    }

    [Fact]
    public async Task Programming_agent_seed_instructions_require_modern_mstest_assertions_for_scaffolded_projects()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var programmingAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(programmingAgent.Id);
        var mstestSkill = Assert.Single(
            await workspaceService.ListCapabilitiesAsync(),
            item => string.Equals(item.Key, "writing-mstest-tests", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(programmingAgent.Capabilities, assignment => assignment.CapabilityId == mstestSkill.Id);
        var mstestInstructions = ReadInlineSkillInstructions(mstestSkill.ConfigurationJson);

        Assert.Contains("Choose one test runner for each test project before scaffolding and keep it consistent", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("dotnet new mstest", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("preserve its generated package family and versions", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("Assert.Throws<T>", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("Assert.ThrowsExactly<T>", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("Reject legacy `Assert.ThrowsException` and `[ExpectedException]` patterns", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("Assert.HasCount(expected, collection)", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("analyzer-approved assertion", mstestInstructions, StringComparison.Ordinal);
        Assert.Contains("expected pre-bootstrap state rather than a blocker", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Run the provided bootstrap or init script first", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("If a required build, test, or browser validation fails", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("keep the on-disk solution, project, and folder names short", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("For .NET app delivery, use the .NET app delivery skill and .NET developer agent", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Route framework-specific implementation, test-runner, component-library, or rendering details to the corresponding specialist", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("source-of-truth product root", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Use mapped aliases and workspace tools exactly as documented", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not treat managed `artifacts/...`, `output/...`, or execution-run folders as the product working directory", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("For greenfield implementation, create the smallest real deliverable structure that fits the request", editor.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Portfolio_architect_seed_instructions_define_typed_project_structure_blocks_and_spacing_rules()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var architectAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(architectAgent.Id);

        Assert.Contains("typed nodes for their real job", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Feature block", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Architecture block", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Project block", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Work item", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not invent enum names like `FeatureBlock`", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("`ProjectBlock` + `feature`", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("`WorkItem` + `task`", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("siblings should usually be separated by about 280", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("child branches by about 480", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_node_move", editor.Instructions, StringComparison.Ordinal);
        Assert.Contains("run subtree recomposition", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("concrete external output path", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolved working directory", editor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not substitute managed `artifacts/...`, `output/...`, or execution-run evidence roots for the app directory", editor.Instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Serious_delivery_review_and_validation_agents_require_durable_file_writes_in_their_seed_instructions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var codeReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var uiReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var securityReviewerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var releaseManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));

        var codeReviewEditor = await workspaceService.GetAgentEditorAsync(codeReviewAgent.Id);
        var qaEditor = await workspaceService.GetAgentEditorAsync(qaAgent.Id);
        var uiReviewEditor = await workspaceService.GetAgentEditorAsync(uiReviewAgent.Id);
        var securityEditor = await workspaceService.GetAgentEditorAsync(securityReviewerAgent.Id);
        var releaseEditor = await workspaceService.GetAgentEditorAsync(releaseManagerAgent.Id);

        Assert.Contains("workspace_write_file", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains(codeReviewAgent.Capabilities, assignment => string.Equals(assignment.CapabilityKey, "candoitall-components-mcp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(qaAgent.Capabilities, assignment => string.Equals(assignment.CapabilityKey, "candoitall-components-mcp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(qaAgent.Capabilities, assignment => string.Equals(assignment.CapabilityKey, "candoitall-frontend-theme", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(qaAgent.Capabilities, assignment => string.Equals(assignment.CapabilityKey, "frontend-skill", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("When a review needs framework-specific, runtime, or UI expertise, require the matching specialist evidence", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Apply framework-specific visual and hosting checks only when the current run's declared stack and inspected source establish that framework", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("If the process step includes `LaunchRuntime` and `CaptureRuntimeProof`", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("When Playwright or screenshot review exposes a defect", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not assume legacy route names from earlier sample runs", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("stale evidence", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path-length failures", qaEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Treat untouched template/demo styling, stock navigation, placeholder-looking content, or a custom visual surface rendered as unstyled DOM as QA defects", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("meaningful filled, selected, or changed state", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("click a representative sequence", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("framework error UI", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("For a .NET target explicitly identified by current-run `DotNet*` launch variables", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("source visual target ImageAsset", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("downstream screenshot proof requirements", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("normal repair branch", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("provider/model is not vision-capable", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("A single static screenshot is insufficient", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("If a non-.NET app is not already running and only PowerShell execution is available", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not call `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for non-.NET deliverables", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace file tools cannot see the managed browser folder during the same attempt", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("screenshot asset handoff path and target project node", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("generated delivery workspaces or other non-git execution roots", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("treat them as secondary context only", codeReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path-length failures", codeReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Back every claim with visible proof", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("provider/model is not vision-capable", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("mark conflicting prior screenshots or notes as stale evidence", uiReviewEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Prefer the existing design system or component library when the inspected product establishes one", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not assume a framework, host, route shape, scaffold, or starter output", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("If a UI uses custom classes, verify that loaded styles visibly affect the primary surface", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("effectively rendered by starter styling only", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("current-run evidence", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not accept vague statements like \"secure enough.\"", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Prior-run summaries do not override the current code", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("filesystem assumptions", securityEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keep the decision explicit: ready, blocked, or ready-with-residual-risk", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not accept stale prior-run artifacts as proof for the current release", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("missing active styles for custom visual surfaces", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("For visible browser workflows, release readiness requires process-visible current-run browser artifacts", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("missing, empty, detached, stale, or chat-only browser proof", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("build-system fragility", releaseEditor.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_read", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", codeReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", qaEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", uiReviewEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", securityEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", releaseEditor.Instructions, StringComparison.Ordinal);
        Assert.Contains("project-structure-context-brief", releaseEditor.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Specialized_default_agents_have_domain_specific_instructions_for_code_and_business_processes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var dotnetDeveloper = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, ".NET Application Developer", StringComparison.Ordinal)).Id);
        var blazorDeveloper = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Blazor Application Developer", StringComparison.Ordinal)).Id);
        var javascriptDeveloper = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "JavaScript Application Developer", StringComparison.Ordinal)).Id);
        var javascriptQa = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "JavaScript QA Review Lead", StringComparison.Ordinal)).Id);
        var dotnetQa = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, ".NET QA Review Lead", StringComparison.Ordinal)).Id);
        var businessStrategist = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Business Strategist", StringComparison.Ordinal)).Id);
        var deliveryManager = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Delivery Manager", StringComparison.Ordinal)).Id);
        var research = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal)).Id);
        var financialStrategist = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Financial Strategist", StringComparison.Ordinal)).Id);
        var marketingSpecialist = await workspaceService.GetAgentEditorAsync(
            Assert.Single(agents, item => string.Equals(item.Name, "Marketing Specialist", StringComparison.Ordinal)).Id);

        Assert.Contains("workspace_dotnet_new", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Prefer `workspace_dotnet_new` only when the authoritative solution context declares `initialize`", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("do not change a declared `initialize` or `verify-existing` mode", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not infer or prefer a `src`/`tests`, inside-root, sibling-project, or product-root-host layout", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("source visual target ImageAsset", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not leave starter content, stock navigation, placeholder routes", dotnetDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("BaseLib", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("component-library", blazorDeveloper.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Before bootstrapping or repairing, inspect the grounded root", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("small JavaScript interop", blazorDeveloper.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Capture screenshot, browser_snapshot or browser_evaluate state output, and browser_console_messages as current-run evidence", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("source visual target ImageAsset", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Replace any observed starter content, stock navigation, placeholder routes", blazorDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("package.json", javascriptDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("package manager", javascriptDeveloper.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("For peer review and integration-readiness steps", javascriptDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not satisfy a behavior defect by adding manifests", javascriptDeveloper.Instructions, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", dotnetQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", dotnetQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("A single static screenshot is insufficient", dotnetQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not submit Completed or Blocked for missing browser receipts from file inspection alone", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Browser screenshots, snapshots, console logs, and state outputs must be current-run evidence", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace file tools cannot see the managed browser folder during the same attempt", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("External generated app folders are often not git repositories", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("npm.cmd", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not pass a server implementation script itself to `workspace_pwsh_run_script`", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not use `[System.Threading.Tasks.Task]::Run({ ... })` with scriptblocks", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("use native absolute paths for redirect files", javascriptQa.Instructions, StringComparison.Ordinal);
        Assert.Contains("do not put `$listener`, `$context`, `$request`, or `$file` variables inside a double-quoted `-Command` string", javascriptQa.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifacts/business/<project-slug>/", businessStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("business-plan.md", businessStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_convert_document", deliveryManager.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_convert_document", research.Instructions, StringComparison.Ordinal);
        Assert.Contains("unit economics", financialStrategist.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assumptions.csv", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_content_get", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("project_structure_node_create", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_convert_document", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_write_spreadsheet", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_spreadsheet_function_catalog", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("image_generation_create", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", financialStrategist.Instructions, StringComparison.Ordinal);
        Assert.Contains("go-to-market", marketingSpecialist.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("campaign-brief.md", marketingSpecialist.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_serious_delivery_seed_agents_are_refreshed_to_the_current_baseline()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var openAiDefaultProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var openAiChatProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.OpenAi &&
                    string.Equals(item.Name, "OpenAI chat completions", StringComparison.Ordinal));
        var ollamaProvider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);

        await DowngradeAgentToLegacyQaAsync(workspaceService, capabilityIdsByKey, ollamaProvider.Id);
        await DowngradeAgentToLegacyProgrammingAsync(workspaceService, capabilityIdsByKey, openAiChatProvider.Id);
        await DowngradeAgentToLegacyArchitectAsync(workspaceService, capabilityIdsByKey, openAiDefaultProvider.Id);
        await DowngradeAgentToLegacyCodeReviewAsync(workspaceService, capabilityIdsByKey);
        await DowngradeAgentToLegacyUiReviewAsync(workspaceService, capabilityIdsByKey);
        await DowngradeAgentToLegacySecurityReviewAsync(workspaceService, capabilityIdsByKey);
        await DowngradeAgentToLegacyReleaseReadinessAsync(workspaceService, capabilityIdsByKey);
        Assert.DoesNotContain(capabilityIdsByKey.Keys, key => string.Equals(key, "candoitall-bundle-workflow", StringComparison.OrdinalIgnoreCase));

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var architectAgent = Assert.Single(agents, item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var codeReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var uiReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var securityReviewAgent = Assert.Single(agents, item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var releaseManagerAgent = Assert.Single(agents, item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));

        AssertOpenAiBacked(architectAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        AssertOpenAiBacked(programmingAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(qaAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(codeReviewAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(uiReviewAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(securityReviewAgent, openAiDefaultProvider.Id, OpenAiModelIds.Gpt56Luna);
        AssertOpenAiBacked(releaseManagerAgent, openAiDefaultProvider.Id, ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        AssertHasCapabilities(architectAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(programmingAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["run-tests"], capabilityIdsByKey["writing-mstest-tests"], capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"], capabilityIdsByKey["dotnet-app-delivery-inline-skill"], capabilityIdsByKey["blazor-ssr-delivery-inline-skill"], capabilityIdsByKey["workspace-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-dotnet-run"], capabilityIdsByKey["workspace-dotnet-stop"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(qaAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["run-tests"], capabilityIdsByKey["writing-mstest-tests"], capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"], capabilityIdsByKey["dotnet-app-delivery-inline-skill"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-dotnet-run"], capabilityIdsByKey["workspace-dotnet-stop"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(codeReviewAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(uiReviewAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-components-mcp"], capabilityIdsByKey["candoitall-frontend-theme"], capabilityIdsByKey["frontend-skill"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        AssertHasCapabilities(securityReviewAgent, capabilityIdsByKey["candoitall-codeanalytics-mcp"], capabilityIdsByKey["architecture-source-rag"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"]);
        AssertHasCapabilities(releaseManagerAgent, capabilityIdsByKey["playwright-local-mcp"], capabilityIdsByKey["candoitall-watch-playwright-loop"], capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"], capabilityIdsByKey["workspace-create-directory"], capabilityIdsByKey["workspace-write-file"], capabilityIdsByKey["workspace-append-file"], capabilityIdsByKey["workspace-pwsh-run-script"]);
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(codeReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(uiReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(securityReviewAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-source-rag", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(qaAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseManagerAgent.Capabilities, item => string.Equals(item.CapabilityKey, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Stale_research_agent_seed_is_refreshed_and_drops_project_structure_capability()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var capabilityIdsByKey = capabilities.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);

        var researchAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(researchAgent.Id);
        editor.ConfigurationJson = "{}";
        if (capabilityIdsByKey.TryGetValue("project-structure-central", out var projectStructureCapabilityId) &&
            !editor.SelectedCapabilityIds.Contains(projectStructureCapabilityId))
        {
            editor.SelectedCapabilityIds.Add(projectStructureCapabilityId);
        }

        await workspaceService.SaveAgentAsync(editor);

        var refreshedResearchAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Research Deep Dive Analyst", StringComparison.Ordinal));

        Assert.DoesNotContain(
            refreshedResearchAgent.Capabilities,
            item => string.Equals(item.CapabilityKey, "project-structure-central", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(GetExpectedManagedSeedVersion(), refreshedResearchAgent.ConfigurationJson, StringComparison.Ordinal);

        var refreshedEditor = await workspaceService.GetAgentEditorAsync(refreshedResearchAgent.Id);
        Assert.True(refreshedEditor.ProjectStructureAccess.CanRead);
        Assert.True(refreshedEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.True(refreshedEditor.ProcessAccess.CanRead);
        Assert.True(refreshedEditor.ProcessAccess.AllowAllDefinitions);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_persists_the_refreshed_agent_seed_for_other_processes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        string[] managedAgentNames =
        [
            "Portfolio Architect",
            "Programming Workspace Analyst",
            "Delivery QA Observer",
            "Code Review Lead",
            "UI Review Lead",
            "Security Reviewer",
            "Release Readiness Manager",
            ".NET Solution Architect",
            ".NET Application Developer",
            "Blazor Application Developer",
            ".NET QA Review Lead",
            ".NET Runtime Failure Analyst",
            "JavaScript Solution Architect",
            "JavaScript Application Developer",
            "JavaScript QA Review Lead",
            "Business Strategist",
            "Financial Strategist",
            "Marketing Specialist",
            "Mail Triage Analyst",
            "Spreadsheet Analyst"
        ];

        foreach (var agentName in managedAgentNames)
        {
            MutateAgentSnapshotInCatalog(catalogPath, agentName, "gpt-4o-mini", "{}");
            var staleSnapshot = ReadAgentSnapshotFromCatalog(catalogPath, agentName);
            Assert.Equal("gpt-4o-mini", staleSnapshot.Model);
            Assert.Equal("{}", staleSnapshot.ConfigurationJson);
        }

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        foreach (var agentName in managedAgentNames)
        {
            AssertManagedSeedRefreshed(agentName, ReadAgentSnapshotFromCatalog(catalogPath, agentName));
        }

        var javascriptQaInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "JavaScript QA Review Lead");
        var javascriptDeveloperInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "JavaScript Application Developer");
        var securityReviewerInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "Security Reviewer");
        var releaseManagerInstructions = ReadAgentInstructionsFromCatalog(catalogPath, "Release Readiness Manager");
        Assert.Contains("For peer review and integration-readiness steps", javascriptDeveloperInstructions, StringComparison.Ordinal);
        Assert.Contains("Do not satisfy a behavior defect by adding manifests", javascriptDeveloperInstructions, StringComparison.Ordinal);
        Assert.Contains("single-quoted here-string", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("escape every literal `$`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("must return after it records the reachable URL and process id", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Do not pass a server implementation script itself to `workspace_pwsh_run_script`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("never execute that child server script directly through `workspace_pwsh_run_script`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("do not make missing `package.json` or missing automated tests release-blocking", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("do not call blocking stream reads", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Multiple ambiguous overloads found for \"Run\"", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Treat HTTP reachability as the startup proof", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("single-quoted here-string and launch it with `-File`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("use `browser_evaluate`", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("replace it with `browser_evaluate` DOM or state proof", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("identify the shipped entrypoint first", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Do not treat unreferenced source files, stale README claims, or a file manifest as proof of shipped behavior", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("Browser proof must exercise the actual loaded runtime", javascriptQaInstructions, StringComparison.Ordinal);
        Assert.Contains("inspect the listed artifact paths directly with workspace tools", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("do not recapture fresh browser proof unless the current security step explicitly requires runtime or browser proof", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("Scale security controls to the declared release boundary", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("do not turn public hosting, CI integration, cross-browser support, artifact signing, or production telemetry into release blockers", securityReviewerInstructions, StringComparison.Ordinal);
        Assert.Contains("QA evidence names the shipped entrypoint", releaseManagerInstructions, StringComparison.Ordinal);
        Assert.Contains("unreferenced implementation files", releaseManagerInstructions, StringComparison.Ordinal);
        Assert.Contains("Scale release approval and rollout to the declared release boundary", releaseManagerInstructions, StringComparison.Ordinal);
        Assert.Contains("A static/package handoff can complete by confirming the approved artifacts are present in the target root", releaseManagerInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_persists_the_refreshed_blazor_ssr_delivery_capability_for_other_processes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListCapabilitiesAsync();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        var staleConfigurationJson = JsonSerializer.Serialize(new
        {
            key = "blazor-ssr-delivery",
            instructions = "Create or improve Blazor SSR applications with maintainable, strongly typed C# and explicit validation."
        });

        MutateCapabilityConfigurationJsonInCatalog(catalogPath, "blazor-ssr-delivery-inline-skill", staleConfigurationJson);
        var staleConfiguration = ReadCapabilityConfigurationJsonFromCatalog(catalogPath, "blazor-ssr-delivery-inline-skill");
        Assert.DoesNotContain("external-target/<drive>/...", staleConfiguration, StringComparison.Ordinal);

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        var refreshedInstructions = ReadInlineSkillInstructions(
            ReadCapabilityConfigurationJsonFromCatalog(catalogPath, "blazor-ssr-delivery-inline-skill"));
        Assert.Contains("If the project structure or attached step materials name a concrete output directory", refreshedInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/<drive>/...", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("do not scaffold a parallel copy under `artifacts/...`, `output/...`, or another generated implementation folder", refreshedInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scaffold directly into it instead of adding an extra nested", refreshedInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Before any scaffold call", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("Workspace command timeout arguments are seconds", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("When writing xUnit tests, include a visible `using Xunit;`", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("DotNetAppProjectFileAlias", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("run target must be the runnable project file", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("custom route backed only by scaffold-default `app.css` and layout CSS", refreshedInstructions, StringComparison.Ordinal);
        Assert.Contains("custom class names without matching loaded styles", refreshedInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_refreshes_versioned_inline_skill_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListCapabilitiesAsync();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        var staleConfigurationJson = JsonSerializer.Serialize(new
        {
            skillSource = "inline",
            inlineSkill = new
            {
                name = "architecture-map",
                description = "Outdated task-specific workflow.",
                instructions = "Use this skill only when the user explicitly asks for a Mermaid or class-diagram output."
            }
        });

        MutateCapabilitySnapshotInCatalog(
            catalogPath,
            "architecture-map-inline-skill",
            "Outdated task-specific workflow.",
            staleConfigurationJson);

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        var refreshedSnapshot = ReadCapabilitySnapshotFromCatalog(catalogPath, "architecture-map-inline-skill");
        var seededSnapshot = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Capabilities,
            item => string.Equals(item.Key, "architecture-map-inline-skill", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(seededSnapshot.Description, refreshedSnapshot.Description);
        Assert.Contains(GetExpectedSeriousDeliveryManagedSeedVersion(), refreshedSnapshot.ConfigurationJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_a_stale_managed_catalog_refreshes_versioned_dotnet_tool_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        _ = await workspaceService.ListCapabilitiesAsync();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var catalogPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceScope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.json");
        var staleConfigurationJson = JsonSerializer.Serialize(new
        {
            tool = "workspace_dotnet_test",
            approvalRequired = false
        });

        MutateCapabilitySnapshotInCatalog(
            catalogPath,
            "workspace-dotnet-test",
            "Runs a bounded dotnet test recipe.",
            staleConfigurationJson);
        MutateCapabilitySnapshotInCatalog(
            catalogPath,
            "workspace-dotnet-stop",
            "Stops a process with a command.",
            staleConfigurationJson);

        var freshStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        _ = await freshStore.LoadCatalogAsync();

        var refreshedSnapshot = ReadCapabilitySnapshotFromCatalog(catalogPath, "workspace-dotnet-test");
        var refreshedStopSnapshot = ReadCapabilitySnapshotFromCatalog(catalogPath, "workspace-dotnet-stop");

        Assert.Contains("stdout/stderr diagnostics", refreshedSnapshot.Description, StringComparison.Ordinal);
        Assert.Contains(GetExpectedSeriousDeliveryManagedSeedVersion(), refreshedSnapshot.ConfigurationJson, StringComparison.Ordinal);
        Assert.Contains("startup.json receipt", refreshedStopSnapshot.Description, StringComparison.Ordinal);
        Assert.Contains("cleanup.json proof", refreshedStopSnapshot.Description, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_stop", refreshedStopSnapshot.ConfigurationJson, StringComparison.Ordinal);
        Assert.Contains(GetExpectedSeriousDeliveryManagedSeedVersion(), refreshedStopSnapshot.ConfigurationJson, StringComparison.Ordinal);
    }

    private static void AssertOpenAiBacked(AgentDefinition agent, Guid providerId, string expectedModel)
    {
        Assert.False(string.IsNullOrWhiteSpace(expectedModel));
        Assert.Equal(providerId, agent.ProviderProfileId);
        Assert.Equal(expectedModel, agent.Model);
    }

    private static void AssertManagedSeedRefreshed(string agentName, (string Model, string ConfigurationJson) snapshot)
    {
        var seededAgent = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Agents,
            item => string.Equals(item.Name, agentName, StringComparison.Ordinal));

        Assert.Equal(seededAgent.Model, snapshot.Model);
        Assert.Contains(GetExpectedManagedSeedVersion(), snapshot.ConfigurationJson, StringComparison.Ordinal);
    }

    private static string GetExpectedManagedSeedVersion()
    {
        var seededAgent = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Agents,
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        using var configuration = JsonDocument.Parse(seededAgent.ConfigurationJson);
        return configuration.RootElement.GetProperty("managedSeedVersion").GetString()
               ?? throw new InvalidOperationException("Managed seed version is missing from the default software delivery agent configuration.");
    }

    private static string GetExpectedSeriousDeliveryManagedSeedVersion()
    {
        var seededCapability = Assert.Single(
            SandboxWorkspaceSeedFactory.Create().Capabilities,
            item => string.Equals(item.Key, "dotnet-app-delivery-inline-skill", StringComparison.Ordinal));
        using var configuration = JsonDocument.Parse(seededCapability.ConfigurationJson);
        return configuration.RootElement.GetProperty("managedSeedVersion").GetString()
               ?? throw new InvalidOperationException("Managed seed version is missing from the serious delivery capability configuration.");
    }

    private static void AssertHasCapabilities(AgentDefinition agent, params Guid[] capabilityIds)
    {
        foreach (var capabilityId in capabilityIds)
        {
            Assert.Contains(agent.Capabilities, item => item.CapabilityId == capabilityId);
        }
    }

    private static async Task DowngradeAgentToLegacyQaAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey,
        Guid ollamaProviderId)
    {
        var qaAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(qaAgent.Id);
        editor.Summary = "Tracks what agents are doing, reviews proofs, and highlights missing gates.";
        editor.ProviderProfileId = ollamaProviderId;
        editor.Model = string.Empty;
        editor.ThinkingEffortOverride = null;
        editor.IsThinkingEffortOverrideEdited = true;
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["candoitall-watch-playwright-loop"] &&
                         id != capabilityIdsByKey["run-tests"] &&
                         id != capabilityIdsByKey["writing-mstest-tests"] &&
                         id != capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["dotnet-app-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"] &&
                         id != capabilityIdsByKey["workspace-dotnet-run"] &&
                         id != capabilityIdsByKey["workspace-dotnet-stop"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyProgrammingAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey,
        Guid openAiChatProviderId)
    {
        var programmingAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(programmingAgent.Id);
        editor.Summary = "Uses skills, RAG, approval-aware tools, and workspace execution helpers to inspect and improve repositories or build applications.";
        editor.ProviderProfileId = openAiChatProviderId;
        editor.Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-codeanalytics-mcp"] &&
                         id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["run-tests"] &&
                         id != capabilityIdsByKey["candoitall-watch-playwright-loop"] &&
                         id != capabilityIdsByKey["writing-mstest-tests"] &&
                         id != capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["dotnet-app-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"] &&
                         id != capabilityIdsByKey["blazor-ssr-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["workspace-dotnet-run"] &&
                         id != capabilityIdsByKey["workspace-dotnet-stop"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyArchitectAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey,
        Guid openAiDefaultProviderId)
    {
        var architectAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(architectAgent.Id);
        editor.Summary = "Explores integration seams, rights boundaries, and long-term CanDoItAll alignment.";
        editor.ProviderProfileId = openAiDefaultProviderId;
        editor.Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["candoitall-codeanalytics-mcp"] &&
                         id != capabilityIdsByKey["candoitall-components-mcp"] &&
                         id != capabilityIdsByKey["architecture-source-rag"] &&
                         id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyCodeReviewAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Code Review Lead", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["concrete-deliverable-delivery-inline-skill"] &&
                         id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyUiReviewAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "UI Review Lead", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["candoitall-frontend-theme"] &&
                         id != capabilityIdsByKey["frontend-skill"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"] &&
                         id != capabilityIdsByKey["workspace-pwsh-run-script"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacySecurityReviewAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Security Reviewer", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task DowngradeAgentToLegacyReleaseReadinessAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey)
    {
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Release Readiness Manager", StringComparison.Ordinal));
        var editor = await workspaceService.GetAgentEditorAsync(agent.Id);
        editor.ConfigurationJson = "{}";
        editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
            .Where(id => id != capabilityIdsByKey["workspace-create-directory"] &&
                         id != capabilityIdsByKey["workspace-write-file"] &&
                         id != capabilityIdsByKey["workspace-append-file"] &&
                         id != capabilityIdsByKey["workspace-pwsh-run-script"])
            .ToList();
        await workspaceService.SaveAgentAsync(editor);
    }

    private static (string Model, string ConfigurationJson) ReadAgentSnapshotFromCatalog(string catalogPath, string agentName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var agent = document.RootElement.GetProperty("agents")
            .EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("name").GetString(), agentName, StringComparison.Ordinal));

        return (
            agent.GetProperty("model").GetString() ?? string.Empty,
            agent.GetProperty("configurationJson").GetString() ?? string.Empty);
    }

    private static string ReadAgentInstructionsFromCatalog(string catalogPath, string agentName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var agent = document.RootElement.GetProperty("agents")
            .EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("name").GetString(), agentName, StringComparison.Ordinal));

        return agent.GetProperty("instructions").GetString() ?? string.Empty;
    }

    private static string ReadCapabilityConfigurationJsonFromCatalog(string catalogPath, string capabilityKey)
    {
        return ReadCapabilitySnapshotFromCatalog(catalogPath, capabilityKey).ConfigurationJson;
    }

    private static (string Description, string ConfigurationJson) ReadCapabilitySnapshotFromCatalog(string catalogPath, string capabilityKey)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var capability = document.RootElement.GetProperty("capabilities")
            .EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("key").GetString(), capabilityKey, StringComparison.OrdinalIgnoreCase));

        return (
            capability.GetProperty("description").GetString() ?? string.Empty,
            capability.GetProperty("configurationJson").GetString() ?? string.Empty);
    }

    private static string ReadInlineSkillInstructions(string configurationJson)
    {
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement
            .GetProperty("inlineSkill")
            .GetProperty("instructions")
            .GetString()
            ?? string.Empty;
    }

    private static async Task SetManagedOpenAiImageProviderModelAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        string model)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var provider = await dbContext.Set<WorkspaceProviderProfile>()
            .SingleAsync(item => item.Id == ManagedOpenAiImageProviderId);
        provider.DefaultModel = model;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<ManagedOpenAiImageProviderState>
        ConfigureLegacyManagedOpenAiImageProviderAsync(
            IDbContextFactory<AppDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var provider = await dbContext.Set<WorkspaceProviderProfile>()
            .SingleAsync(item => item.Id == ManagedOpenAiImageProviderId);
        provider.ProviderKind = WorkspaceProviderKind.OllamaRemote;
        provider.ConnectorPluginKey =
            CanDoItAll.Modules.Workspace.OllamaRemoteProviderAdapter.PluginKey;
        provider.ConfigSchemaVersion = "customer-v7";
        provider.BaseUrl = "https://customer.example.test/image-api";
        provider.ApiKeySecretId = Guid.Parse("9EFFD604-8A0C-497C-AD48-7FB9BB405EDD");
        provider.DefaultModel = OpenAiModelIds.GptImage1Mini;
        provider.TimeoutSeconds = 137;
        provider.IsEnabled = false;
        provider.SupportsStreaming = true;
        provider.SupportsToolCalling = true;
        provider.SupportsStructuredOutput = true;
        provider.SupportsVision = true;
        provider.LastHealthCheckAtUtc = new DateTimeOffset(2026, 7, 19, 12, 34, 56, TimeSpan.Zero);
        provider.LastHealthStatus = "Customer image provider healthy";
        provider.ExtraSettingsJson = BuildManagedOpenAiImageProviderPreservationSettingsJson();
        provider.ConcurrencyToken = Guid.Parse("7E9AABAD-BE9F-4F25-9E91-BF4D06A105F8");
        await dbContext.SaveChangesAsync();
        return CaptureManagedOpenAiImageProviderState(provider);
    }

    private static async Task<string> ReadManagedOpenAiImageProviderModelAsync(
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<WorkspaceProviderProfile>()
            .Where(item => item.Id == ManagedOpenAiImageProviderId)
            .Select(item => item.DefaultModel)
            .SingleAsync();
    }

    private static async Task<ManagedOpenAiImageProviderState>
        ReadManagedOpenAiImageProviderStateAsync(
            IDbContextFactory<AppDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var provider = await dbContext.Set<WorkspaceProviderProfile>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == ManagedOpenAiImageProviderId);
        return CaptureManagedOpenAiImageProviderState(provider);
    }

    private static ManagedOpenAiImageProviderState CaptureManagedOpenAiImageProviderState(
        WorkspaceProviderProfile provider)
    {
        return new ManagedOpenAiImageProviderState(
            provider.Id,
            provider.Name,
            provider.ProviderKind,
            provider.ConnectorPluginKey,
            provider.ConfigSchemaVersion,
            provider.BaseUrl,
            provider.ApiKeySecretId,
            provider.DefaultModel,
            provider.TimeoutSeconds,
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsToolCalling,
            provider.SupportsStructuredOutput,
            provider.SupportsVision,
            provider.LastHealthCheckAtUtc,
            provider.LastHealthStatus,
            provider.ExtraSettingsJson,
            provider.ConcurrencyToken);
    }

    private static string BuildManagedOpenAiImageProviderPreservationSettingsJson()
    {
        var configurationJson = JsonSerializer.Serialize(new
        {
            configuration = new
            {
                background = "transparent",
                quality = "high"
            },
            apiKeyEnvironmentVariable = "CUSTOM_IMAGE_API_KEY",
            providerTransport = nameof(ProviderTransportKind.ChatCompletions),
            providerPurpose = nameof(ProviderProfilePurpose.ImageGeneration),
            notes = "Customer-owned image provider settings.",
            tags = new[] { "customer", "image" },
            suggestedModels = new[] { OpenAiModelIds.GptImage1Mini, "customer-image-model-v7" }
        });
        return ProviderPricingMetadata.Write(
            configurationJson,
            isPrivateProvider: true,
            [new ProviderModelTokenPrice("customer-image-model-v7", 1.25m, 0.25m, 2.5m)]);
    }

    private sealed record ManagedOpenAiImageProviderState(
        Guid Id,
        string Name,
        WorkspaceProviderKind? ProviderKind,
        string ConnectorPluginKey,
        string ConfigSchemaVersion,
        string BaseUrl,
        Guid? ApiKeySecretId,
        string DefaultModel,
        int TimeoutSeconds,
        bool IsEnabled,
        bool SupportsStreaming,
        bool SupportsToolCalling,
        bool SupportsStructuredOutput,
        bool SupportsVision,
        DateTimeOffset? LastHealthCheckAtUtc,
        string? LastHealthStatus,
        string ExtraSettingsJson,
        Guid ConcurrencyToken);

    private static void MutateAgentSnapshotInCatalog(string catalogPath, string agentName, string model, string configurationJson)
    {
        var root = JsonNode.Parse(File.ReadAllText(catalogPath))?.AsObject()
            ?? throw new InvalidOperationException("Catalog JSON could not be parsed.");
        var agents = root["agents"]?.AsArray()
            ?? throw new InvalidOperationException("Catalog JSON did not contain an agents array.");
        var agent = agents
            .OfType<JsonObject>()
            .Single(item => string.Equals(item["name"]?.GetValue<string>(), agentName, StringComparison.Ordinal));

        agent["model"] = model;
        agent["configurationJson"] = configurationJson;
        File.WriteAllText(catalogPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static void MutateCapabilityConfigurationJsonInCatalog(string catalogPath, string capabilityKey, string configurationJson)
    {
        MutateCapabilitySnapshotInCatalog(catalogPath, capabilityKey, description: null, configurationJson);
    }

    private static void MutateCapabilitySnapshotInCatalog(string catalogPath, string capabilityKey, string? description, string configurationJson)
    {
        var root = JsonNode.Parse(File.ReadAllText(catalogPath))?.AsObject()
            ?? throw new InvalidOperationException("Catalog JSON could not be parsed.");
        var capabilities = root["capabilities"]?.AsArray()
            ?? throw new InvalidOperationException("Catalog JSON did not contain a capabilities array.");
        var capability = capabilities
            .OfType<JsonObject>()
            .Single(item => string.Equals(item["key"]?.GetValue<string>(), capabilityKey, StringComparison.OrdinalIgnoreCase));

        if (description is not null)
        {
            capability["description"] = description;
        }

        capability["configurationJson"] = configurationJson;
        File.WriteAllText(catalogPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
