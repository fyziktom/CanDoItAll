using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptsCuratorAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Provider_fails_closed_for_identity_lifecycle_permission_purpose_and_catalog_spoofs()
    {
        var gallery = PromptGalleryTestSupport.CreateService(
            PromptGalleryTestSupport.CreateFactory(nameof(Provider_fails_closed_for_identity_lifecycle_permission_purpose_and_catalog_spoofs)));
        var harness = CreateHarness(gallery);
        var wrongId = harness.Context with
        {
            Agent = harness.Context.Agent with { Id = Guid.NewGuid() }
        };
        var wrongTemplate = harness.Context with
        {
            Agent = harness.Context.Agent with { TemplateKey = "prompts-curator-agent-spoof" }
        };
        var suspended = harness.Context with
        {
            Agent = harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
        };
        var template = harness.Context with
        {
            Agent = harness.Context.Agent with { IsTemplate = true }
        };
        var toolsDisabled = harness.Context with
        {
            Agent = harness.Context.Agent with
            {
                Permissions = harness.Context.Agent.Permissions with { CanUseTools = false }
            }
        };
        var processPurpose = harness.Context with
        {
            Purpose = AgentRuntimeToolProviderPurpose.GovernedProcessAutomation
        };
        var searchAssignment = Assert.Single(
            harness.Context.Agent.Capabilities,
            item => item.CapabilityKey == PromptsCuratorAgentCapabilityKeys.CatalogSearch);
        var wrongCatalog = harness.Context with
        {
            Agent = harness.Context.Agent with { Capabilities = [searchAssignment] },
            Capabilities = harness.Context.Capabilities
                .Where(item => item.Id == searchAssignment.CapabilityId)
                .Select(item => item with { Key = $"{item.Key}-spoof" })
                .ToArray()
        };

        foreach (var context in new[]
                 {
                     wrongId,
                     wrongTemplate,
                     suspended,
                     template,
                     toolsDisabled,
                     processPurpose,
                     wrongCatalog
                 })
        {
            Assert.Empty(await harness.Provider.CreateToolsAsync(context, CancellationToken.None));
            Assert.Empty(harness.Provider.GetToolMetadata(context));
        }

        var wrongCase = CreateHarness(
            gallery,
            [PromptsCuratorAgentCapabilityKeys.CatalogSearch.ToUpperInvariant()]);
        Assert.Empty(await wrongCase.Provider.CreateToolsAsync(wrongCase.Context, CancellationToken.None));
    }

    [Fact]
    public async Task Attached_tool_reauthorizes_capability_and_lifecycle_at_invocation_time()
    {
        var gallery = PromptGalleryTestSupport.CreateService(
            PromptGalleryTestSupport.CreateFactory(nameof(Attached_tool_reauthorizes_capability_and_lifecycle_at_invocation_time)));
        var harness = CreateHarness(gallery, [PromptsCuratorAgentCapabilityKeys.CatalogSearch]);
        var searchTool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(
            await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None)));

        harness.Workspace.Agents =
        [
            harness.Context.Agent with { Capabilities = [] }
        ];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            searchTool.InvokeAsync(new AIFunctionArguments
            {
                ["request"] = new PromptsCuratorCatalogSearchInput()
            }).AsTask());

        harness.Workspace.Agents =
        [
            harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
        ];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            searchTool.InvokeAsync(new AIFunctionArguments
            {
                ["request"] = new PromptsCuratorCatalogSearchInput()
            }).AsTask());
    }

    [Fact]
    public async Task Curator_tools_create_update_detect_stale_state_version_and_search_all_statuses()
    {
        var gallery = PromptGalleryTestSupport.CreateService(
            PromptGalleryTestSupport.CreateFactory(nameof(Curator_tools_create_update_detect_stale_state_version_and_search_all_statuses)));
        var harness = CreateHarness(gallery);
        var tools = (await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);

        var created = await InvokeAsync<PromptsCuratorItemEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.PromptGalleryDraftCreate],
            CreateDraftInput("First curator draft", "Initial content."));

        Assert.Equal(PromptArtifactStatus.Draft, created.Status);
        Assert.Equal(PromptArtifactProvenance.User, created.Source.Provenance);
        Assert.NotEqual(default, created.UpdatedAtUtc);

        var editor = await InvokeAsync<PromptsCuratorItemEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.PromptGalleryItemEditorGet],
            new PromptsCuratorItemEditorInput(created.PromptArtifactId));
        Assert.Equal("Initial content.", editor.DraftContent);
        Assert.Equal(created.UpdatedAtUtc, editor.UpdatedAtUtc);

        var updated = await InvokeAsync<PromptsCuratorItemEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate],
            CreateUpdateInput(editor, "Reviewed content."));
        Assert.Equal("Reviewed content.", updated.DraftContent);
        Assert.True(updated.UpdatedAtUtc > editor.UpdatedAtUtc);

        var staleException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeAsync<PromptsCuratorItemEditorResult>(
                tools[AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate],
                CreateUpdateInput(editor, "Stale overwrite.")));
        Assert.Contains("prompts.gallery.concurrency-conflict", staleException.Message, StringComparison.Ordinal);

        var version = await InvokeAsync<PromptVersionSnapshot>(
            tools[AgentToolInvocationPolicyMetadata.PromptGalleryVersionCreate],
            new PromptsCuratorVersionCreateInput(
                updated.PromptArtifactId,
                updated.UpdatedAtUtc,
                "Approved curator publication"));
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("Reviewed content.", version.Content);

        var secondDraft = await InvokeAsync<PromptsCuratorItemEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.PromptGalleryDraftCreate],
            CreateDraftInput("Second curator draft", "Still a draft."));
        Assert.Equal(PromptArtifactStatus.Draft, secondDraft.Status);

        var allStatuses = await InvokeAsync<PromptsCuratorCatalogSearchResult>(
            tools[AgentToolInvocationPolicyMetadata.PromptGalleryCatalogSearch],
            new PromptsCuratorCatalogSearchInput(pageSize: 10));

        Assert.Equal(2, allStatuses.TotalCount);
        Assert.Contains(allStatuses.Items, item => item.Status == PromptArtifactStatus.Final);
        Assert.Contains(allStatuses.Items, item => item.Status == PromptArtifactStatus.Draft);
        Assert.Equal(10, allStatuses.PageSize);

        var draftsOnly = await InvokeAsync<PromptsCuratorCatalogSearchResult>(
            tools[AgentToolInvocationPolicyMetadata.PromptGalleryCatalogSearch],
            new PromptsCuratorCatalogSearchInput(status: PromptArtifactStatus.Draft, pageSize: 10));
        var draft = Assert.Single(draftsOnly.Items);
        Assert.Equal(secondDraft.PromptArtifactId, draft.PromptArtifactId);
    }

    [Fact]
    public void Metadata_requires_approval_for_mutations_and_protects_prompt_content_in_audit_data()
    {
        var mutationNames = new[]
        {
            AgentToolInvocationPolicyMetadata.PromptGalleryDraftCreate,
            AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate,
            AgentToolInvocationPolicyMetadata.PromptGalleryVersionCreate
        };
        var readNames = new[]
        {
            AgentToolInvocationPolicyMetadata.PromptGalleryCatalogSearch,
            AgentToolInvocationPolicyMetadata.PromptGalleryItemEditorGet
        };

        Assert.All(mutationNames, toolName =>
        {
            Assert.True(ToolContractCatalog.IsKnownToolName(toolName));
            Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(toolName));
            Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
            Assert.True(ToolCapabilityRegistry.TryResolve(toolName, out var metadata));
            Assert.Equal(ToolCapabilitySideEffectKind.InternalStateMutation, metadata.SideEffectKind);
        });
        Assert.All(readNames, toolName =>
        {
            Assert.True(ToolContractCatalog.IsKnownToolName(toolName));
            Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(toolName));
            Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
        });

        var promptArtifactId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var updatedAtUtc = DateTimeOffset.Parse("2026-07-19T14:00:00Z");
        const string title = "Confidential customer migration";
        const string content = "Private prompt body for Project Nightfall.";
        var request = new
        {
            promptArtifactId,
            expectedUpdatedAtUtc = updatedAtUtc,
            title,
            content,
            tags = new[] { "confidential-customer" }
        };
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate,
        [
            new KeyValuePair<string, object?>("request", request)
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate,
            redacted);
        var audit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate,
            JsonSerializer.Serialize(new { request }));

        Assert.Contains(promptArtifactId.ToString("D"), signature, StringComparison.Ordinal);
        Assert.DoesNotContain(title, signature, StringComparison.Ordinal);
        Assert.DoesNotContain(content, signature, StringComparison.Ordinal);
        Assert.Contains("prompt-curator-approval-redacted-v1", audit, StringComparison.Ordinal);
        Assert.Contains(promptArtifactId.ToString("D"), audit, StringComparison.Ordinal);
        Assert.DoesNotContain(title, audit, StringComparison.Ordinal);
        Assert.DoesNotContain(content, audit, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAgentFrameworkModule_registers_curator_authorization_and_provider_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        var authorization = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(PromptsCuratorAgentRuntimeAuthorizationService) &&
                descriptor.ImplementationType == typeof(PromptsCuratorAgentRuntimeAuthorizationService));
        var provider = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
                descriptor.ImplementationType == typeof(PromptsCuratorAgentRuntimeToolProvider));

        Assert.Equal(ServiceLifetime.Scoped, authorization.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, provider.Lifetime);
    }

    private static PromptsCuratorDraftCreateInput CreateDraftInput(string title, string content)
    {
        return new PromptsCuratorDraftCreateInput(
            projectId: null,
            collectionId: null,
            title,
            "Curator runtime tool proof.",
            PromptGalleryItemKind.FullPrompt,
            "curation",
            content,
            tags: ["curator"],
            supportedConsumers: [PromptGalleryConsumer.AgentRuntime],
            recommendations: new PromptModelRecommendations(0.2, 800));
    }

    private static PromptsCuratorDraftUpdateInput CreateUpdateInput(
        PromptsCuratorItemEditorResult editor,
        string content)
    {
        return new PromptsCuratorDraftUpdateInput(
            editor.PromptArtifactId,
            editor.UpdatedAtUtc,
            editor.ProjectId,
            editor.CollectionId,
            editor.Title,
            editor.Summary,
            editor.Kind,
            editor.Phase,
            content,
            editor.Tags,
            editor.SupportedModels,
            editor.SupportedConsumers,
            editor.Recommendations);
    }

    private static RuntimeHarness CreateHarness(
        IPromptGalleryService gallery,
        IEnumerable<string>? capabilityKeys = null)
    {
        var now = DateTimeOffset.Parse("2026-07-19T12:00:00Z");
        var keys = (capabilityKeys ?? PromptsCuratorAgentCapabilityKeys.ToolNameToCapabilityKey.Values)
            .ToArray();
        var capabilities = keys
            .Select(key => new CapabilityCatalogItem(
                Guid.NewGuid(),
                CapabilityKind.Tool,
                key,
                key,
                string.Empty,
                string.Empty,
                string.Empty,
                CapabilityProofStatus.Verified,
                string.Empty,
                now,
                IsBuiltIn: true))
            .ToArray();
        var assignments = capabilities
            .Select(capability => new AgentCapabilityAssignment(
                capability.Id,
                capability.Key,
                capability.Kind,
                capability.ProofStatus,
                capability.LastVerifiedAtUtc,
                capability.ProofNotes))
            .ToArray();
        var providerProfileId = Guid.NewGuid();
        var agent = new AgentDefinition(
            PromptsCuratorAgentIdentity.AgentId,
            "Prompts Curator Agent",
            "Prompt Gallery curator",
            "Maintains canonical prompts.",
            "Use the dedicated curator tools.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "gpt-5.4-mini",
            AgentWorkloadKind.Management,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            PromptsCuratorAgentIdentity.TemplateKey,
            AgentPermissionsPolicy.Default with { CanUseTools = true },
            assignments,
            [],
            now,
            now);
        var providerProfile = new ProviderProfile(
            providerProfileId,
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            agent.Model,
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, AuthorizationWorkspaceProxy>();
        var workspace = (AuthorizationWorkspaceProxy)(object)workspaceService;
        workspace.Agents = [agent];
        workspace.Capabilities = capabilities;
        var runtimeProvider = new PromptsCuratorAgentRuntimeToolProvider(
            gallery,
            new PromptsCuratorAgentRuntimeAuthorizationService(workspaceService));
        var context = new AgentRuntimeToolProviderContext(
            agent,
            providerProfile,
            capabilities,
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "prompts-curator-runtime-test",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
        return new RuntimeHarness(runtimeProvider, context, workspace);
    }

    private static async Task<TResult> InvokeAsync<TResult>(AITool tool, object request)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(new AIFunctionArguments
        {
            ["request"] = request
        });
        return rawResult switch
        {
            TResult result => result,
            JsonElement element => JsonSerializer.Deserialize<TResult>(element.GetRawText(), JsonOptions)
                ?? throw new InvalidOperationException("Prompts Curator runtime tool returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Unexpected Prompts Curator runtime tool result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record RuntimeHarness(
        PromptsCuratorAgentRuntimeToolProvider Provider,
        AgentRuntimeToolProviderContext Context,
        AuthorizationWorkspaceProxy Workspace);

    private class AuthorizationWorkspaceProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult(Agents),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult(Capabilities),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this runtime-provider test.")
            };
        }
    }
}
