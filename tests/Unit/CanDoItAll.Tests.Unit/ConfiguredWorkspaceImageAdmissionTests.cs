using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ConfiguredWorkspaceImageAdmissionTests {
    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImage, false)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImage, true)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImages, false)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImages, true)]
    public async Task Actual_configured_composition_preserves_image_schema_read_classification_and_catalog_approval(string name, bool catalogApproval) {
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection().BuildServiceProvider();
        var state = await ComposeAsync(services, name, catalogApproval, enabled: true);
        try {
            var tool = Assert.Single(state.Tools, tool => tool.Name == name);
            var metadata = Assert.Single(state.RuntimeToolMetadata, item => item.ToolName == name);
            Assert.Equal(AgentRuntimeToolOperationKind.Read, metadata.OperationKind);
            Assert.Equal(AgentRuntimeToolRecoveryPolicy.ReconcileBeforeRetry, metadata.RecoveryPolicy);
            Assert.Equal(catalogApproval, tool is ApprovalRequiredAIFunction);
            Assert.Equal(catalogApproval, metadata.RequiresApprovalByDefault);
            Assert.Null(metadata.PrepareAdmission);
            Assert.Null(metadata.AuthorizeAdmissionAsync);
            Assert.Null(metadata.AuthorizeResultDisclosureAsync);
            Assert.Equal(ToolInvocationClassification.Read, AgentToolPolicyCatalog.BuiltIn.Classify(name));
            Assert.False(AgentToolPolicyCatalog.BuiltIn.RequiresApprovalByDefault(name));
            var schema = Assert.IsAssignableFrom<AIFunction>(tool).JsonSchema.GetProperty("properties");
            Assert.True(schema.TryGetProperty(name == ToolContractCatalog.WorkspaceAnalyzeImage ? "path" : "paths", out _));
            Assert.True(schema.TryGetProperty("prompt", out _));
            Assert.Equal(state.Tools.Count, state.Tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());
        } finally {
            Assert.Empty(await state.DisposeAcquiredResourcesAsync());
        }
    }

    [Fact]
    public async Task Disabled_configured_workspace_tools_add_neither_image_functions_nor_metadata() {
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection().BuildServiceProvider();
        var state = await ComposeAsync(services, ToolContractCatalog.WorkspaceAnalyzeImage, false, enabled: false);
        try {
            Assert.DoesNotContain(state.Tools, tool => tool.Name is ToolContractCatalog.WorkspaceAnalyzeImage or ToolContractCatalog.WorkspaceAnalyzeImages);
            Assert.DoesNotContain(state.RuntimeToolMetadata, item => item.ToolName is ToolContractCatalog.WorkspaceAnalyzeImage or ToolContractCatalog.WorkspaceAnalyzeImages);
        } finally {
            Assert.Empty(await state.DisposeAcquiredResourcesAsync());
        }
    }

    [Fact]
    public void Ordinary_workspace_reads_do_not_gain_a_metered_recovery_override() {
        var ordinary = AIFunctionFactory.Create(() => "read", ToolContractCatalog.WorkspaceReadFile);
        Assert.Empty(WorkspaceToolSet.CreateRecoveryMetadata([ordinary]));
    }

    private static Task<RuntimeCapabilityState> ComposeAsync(IServiceProvider services, string name, bool catalogApproval, bool enabled) {
        var root = Path.GetTempPath();
        var catalog = SandboxWorkspaceSeedFactory.Create().ToCatalog();
        var agent = catalog.Agents.First(item => item.ProviderProfileId.HasValue);
        var provider = catalog.Providers.First(item => item.Id == agent.ProviderProfileId);
        CapabilityCatalogItem[] capabilities = catalogApproval ? [new(Guid.NewGuid(), CapabilityKind.Tool, "reviewed-image-analysis",
            "Reviewed image analysis", string.Empty, string.Empty, JsonSerializer.Serialize(new { tool = name, approvalRequired = true }),
            CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, IsBuiltIn: true)] : [];
        agent = agent with {
            Permissions = agent.Permissions with { CanUseTools = true },
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}", new() {
                Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true, CanTransformArtifacts = true
            }),
            Capabilities = capabilities.Select(item => new AgentCapabilityAssignment(item.Id, item.Key, item.Kind,
                item.ProofStatus, item.LastVerifiedAtUtc, item.ProofNotes)).ToArray()
        };
        return RuntimeCapabilityComposer.CreateDefault(root, services).CreateCapabilityStateCoreAsync(agent, provider, provider.DefaultModel,
            capabilities, [], (_, _, _) => Task.CompletedTask, default, false, WorkspaceScopeDescriptor.Sandbox,
            AgentRuntimeContextIntent.Empty with {
                Purpose = AgentRuntimeContextPurpose.InteractiveChat, WorkspaceToolsEnabled = enabled,
                ToolCapabilitiesEnabled = enabled, RuntimeToolProvidersEnabled = false
            }, WorkspaceRuntimeServicesTestFactory.Create(root));
    }
}
