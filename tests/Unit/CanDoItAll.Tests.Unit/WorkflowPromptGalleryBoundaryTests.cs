using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class WorkflowPromptGalleryBoundaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PersistentComponentSaveRejectsIncompatibleGalleryBindingBeforePersisting()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(AgentFrameworkModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-prompt-boundary-{Guid.NewGuid():N}")
            .Options;
        var factory = new PromptGalleryTestSupport.TestDbContextFactory(options);
        var promptArtifactId = Guid.NewGuid();
        var promptVersionId = Guid.NewGuid();
        var promptGallery = new IncompatiblePromptGallery(promptArtifactId, promptVersionId);
        var provider = CreateProvider();
        var catalog = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            promptGallery,
            new UnusedPromptGalleryImporter(),
            new TestProviderProfileRegistry(provider),
            new ProviderProfileService());
        var request = new LlmCallComponentSaveRequest(
            Id: null,
            "Pinned Gallery prompt",
            provider.Id,
            provider.DefaultModel,
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            "Pinned prompt content.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default)
        {
            PromptArtifactId = promptArtifactId,
            PromptVersionId = promptVersionId
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SaveComponentAsync(request));

        Assert.Contains("not declared as supported", exception.Message, StringComparison.OrdinalIgnoreCase);

        await using var dbContext = factory.CreateDbContext();
        Assert.Empty(await dbContext.Set<WorkflowComponentRecord>().ToListAsync());
    }

    [Fact]
    public async Task Workflow_publication_rejects_an_intervening_gallery_writer()
    {
        var factory = CreateWorkflowFactory("publication-concurrency");
        var gallery = PromptGalleryTestSupport.CreateService(factory);
        var provider = CreateProvider();
        var initialSave = await gallery.SaveDraftAsync(new PromptGalleryDraft(
            Id: null,
            ProjectId: null,
            CollectionId: null,
            "Concurrent workflow prompt",
            "Concurrency test prompt.",
            PromptGalleryItemKind.FullPrompt,
            "workflow",
            "Initial published content.",
            Tags: ["workflow"],
            SupportedModels: [new PromptProviderModel("OpenAi", provider.DefaultModel, IsPreferred: true)],
            SupportedConsumers: [PromptGalleryConsumer.Workflow]));
        Assert.True(initialSave.IsSuccess);
        var promptId = initialSave.Value.PromptArtifactId;
        Assert.True((await gallery.CreateVersionAsync(
            promptId,
            new PromptVersionCreateRequest(
                "Initial publication",
                initialSave.Value.UpdatedAtUtc))).IsSuccess);
        var concurrentGallery = new InterveningWritePromptGallery(gallery);
        var catalog = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            concurrentGallery,
            gallery,
            new TestProviderProfileRegistry(provider),
            new ProviderProfileService());
        var legacyComponent = CreateLegacyComponent();
        var request = new LlmCallComponentSaveRequest(
            Id: null,
            "Concurrent workflow component",
            provider.Id,
            provider.DefaultModel,
            legacyComponent.Modality,
            legacyComponent.ModelSettings,
            "Workflow writer content.",
            legacyComponent.InputShape,
            legacyComponent.ResultShape,
            legacyComponent.Permissions)
        {
            PromptArtifactId = promptId
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SaveComponentAsync(request));

        Assert.Contains("concurrency-conflict", exception.Message, StringComparison.Ordinal);
        var current = Assert.IsType<PromptGalleryItemDetails>((await gallery.GetItemAsync(promptId)).Value);
        Assert.Equal("Concurrent writer content.", current.DraftContent);
        Assert.Equal(PromptArtifactStatus.Draft, current.Status);
        Assert.Single(current.Versions);
        await using var dbContext = factory.CreateDbContext();
        Assert.Empty(await dbContext.Set<WorkflowComponentRecord>().ToListAsync());
    }

    [Fact]
    public async Task StartupMigrationBatchesLegacyRecordsAndPersistsDurableMarkers()
    {
        var factory = CreateWorkflowFactory("legacy-migration");
        var gallery = PromptGalleryTestSupport.CreateService(factory);
        var catalog = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            gallery,
            gallery);
        var providerProfileId = Guid.NewGuid();
        var component = CreateLegacyComponent() with { ProviderProfileId = providerProfileId };
        var definition = CreateLegacyDefinition(component.Id);
        await using (var arrangeContext = factory.CreateDbContext())
        {
            var definitionRecord = WorkflowDefinitionRecord.FromDefinition(definition, revision: 1);
            definitionRecord.InstructionSnapshotSchemaVersion = 0;
            arrangeContext.Add(new WorkflowComponentRecord
            {
                Id = component.Id.Value,
                Name = component.Name,
                ProviderProfileId = component.ProviderProfileId,
                Model = component.Model,
                Modality = component.Modality,
                ComponentJson = JsonSerializer.Serialize(component, JsonOptions),
                PromptGalleryBindingSchemaVersion = 0,
                CreatedAtUtc = component.CreatedAtUtc,
                UpdatedAtUtc = component.UpdatedAtUtc
            });
            arrangeContext.Add(definitionRecord);
            arrangeContext.Add(new WorkflowDefinitionHeadRecord
            {
                WorkflowId = definition.Id.Value,
                VersionId = definition.VersionId.Value
            });
            await arrangeContext.SaveChangesAsync();
        }

        var migration = new WorkflowPromptGalleryMigrationService(
            catalog,
            factory,
            NullLogger<WorkflowPromptGalleryMigrationService>.Instance);

        await migration.EnsureMigratedAsync();

        string migratedComponentJson;
        string migratedDefinitionJson;
        await using (var assertContext = factory.CreateDbContext())
        {
            var componentRecord = await assertContext.Set<WorkflowComponentRecord>().SingleAsync();
            Assert.Equal(1, componentRecord.PromptGalleryBindingSchemaVersion);
            Assert.NotNull(componentRecord.PromptArtifactId);
            Assert.NotNull(componentRecord.PromptVersionId);
            Assert.DoesNotContain("instructions", componentRecord.ComponentJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(component.Instructions, componentRecord.ComponentJson, StringComparison.Ordinal);
            migratedComponentJson = componentRecord.ComponentJson;

            var definitionRecord = await assertContext.Set<WorkflowDefinitionRecord>().SingleAsync();
            Assert.Equal(2, definitionRecord.InstructionSnapshotSchemaVersion);
            migratedDefinitionJson = definitionRecord.DefinitionJson;
            var migratedDefinition = JsonSerializer.Deserialize<WorkflowDefinition>(
                definitionRecord.DefinitionJson,
                JsonOptions);
            var llmNode = Assert.Single(migratedDefinition!.Graph.Nodes);
            Assert.Equal(component.Instructions, llmNode.Settings.Instructions);
            Assert.Equal(providerProfileId, llmNode.Settings.ProviderProfileId);
            Assert.Equal(component.Model, llmNode.Settings.Model);

            Assert.Single(await assertContext.Set<PromptArtifact>().ToArrayAsync());
            Assert.Single(await assertContext.Set<PromptVersion>().ToArrayAsync());
        }

        var hydratedComponent = await catalog.GetComponentAsync(component.Id);
        Assert.NotNull(hydratedComponent);
        Assert.Equal(component.Instructions, hydratedComponent.Instructions);

        await migration.EnsureMigratedAsync();

        await using var idempotencyContext = factory.CreateDbContext();
        var persistedComponent = await idempotencyContext.Set<WorkflowComponentRecord>().SingleAsync();
        var persistedDefinition = await idempotencyContext.Set<WorkflowDefinitionRecord>().SingleAsync();
        Assert.Equal(migratedComponentJson, persistedComponent.ComponentJson);
        Assert.Equal(migratedDefinitionJson, persistedDefinition.DefinitionJson);
        Assert.Single(await idempotencyContext.Set<PromptArtifact>().ToArrayAsync());
        Assert.Single(await idempotencyContext.Set<PromptVersion>().ToArrayAsync());
    }

    [Fact]
    public async Task ComponentHydrationFailsExplicitlyWhenBoundPromptVersionIsMissing()
    {
        var factory = CreateWorkflowFactory("missing-version");
        var gallery = PromptGalleryTestSupport.CreateService(factory);
        var catalog = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            gallery,
            gallery);
        var promptVersionId = Guid.NewGuid();
        var component = CreateLegacyComponent() with
        {
            PromptArtifactId = Guid.NewGuid(),
            PromptVersionId = promptVersionId
        };
        await using (var arrangeContext = factory.CreateDbContext())
        {
            arrangeContext.Add(WorkflowComponentRecord.FromComponent(component));
            await arrangeContext.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.GetComponentAsync(component.Id));

        Assert.Contains("Prompt Gallery versions operation failed", exception.Message, StringComparison.Ordinal);
        Assert.Contains(promptVersionId.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefinitionValidationFailsWhenBoundGalleryItemIsArchivedAfterComponentSave()
    {
        var factory = CreateWorkflowFactory("archived-after-save");
        var gallery = PromptGalleryTestSupport.CreateService(factory);
        var catalog = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            gallery,
            gallery);
        var legacyComponent = CreateLegacyComponent();
        var component = await catalog.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            legacyComponent.Name,
            legacyComponent.ProviderProfileId,
            legacyComponent.Model,
            legacyComponent.Modality,
            legacyComponent.ModelSettings,
            legacyComponent.Instructions,
            legacyComponent.InputShape,
            legacyComponent.ResultShape,
            legacyComponent.Permissions));
        var definition = CreateValidationDefinition(component);

        var beforeArchive = await catalog.ValidateDefinitionAsync(definition);

        Assert.True(beforeArchive.Succeeded);
        var archiveResult = await gallery.ArchiveAsync(component.PromptArtifactId!.Value, archived: true);
        Assert.True(archiveResult.IsSuccess);

        var afterArchive = await catalog.ValidateDefinitionAsync(definition);

        var issue = Assert.Single(afterArchive.Issues, item =>
            item.Message.Contains("Archived Gallery items", StringComparison.Ordinal));
        Assert.Equal(WorkflowValidationIssueCode.InvalidComponentReference, issue.Code);
        Assert.False(afterArchive.Succeeded);
    }

    [Fact]
    public async Task DefinitionValidationRejectsIncompatibleNodeProviderModelOverride()
    {
        var factory = CreateWorkflowFactory("node-provider-model-override");
        var gallery = PromptGalleryTestSupport.CreateService(factory);
        var provider = CreateProvider();
        var catalog = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            gallery,
            gallery,
            new TestProviderProfileRegistry(provider),
            new ProviderProfileService());
        var legacyComponent = CreateLegacyComponent();
        var component = await catalog.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            legacyComponent.Name,
            provider.Id,
            provider.DefaultModel,
            legacyComponent.Modality,
            legacyComponent.ModelSettings,
            legacyComponent.Instructions,
            legacyComponent.InputShape,
            legacyComponent.ResultShape,
            legacyComponent.Permissions));
        var definition = CreateValidationDefinition(component);
        var nodes = definition.Graph.Nodes
            .Select(node => node.Kind == WorkflowNodeKind.LlmCall
                ? node with
                {
                    Settings = node.Settings with
                    {
                        ProviderProfileId = provider.Id,
                        Model = "incompatible-node-model"
                    }
                }
                : node)
            .ToArray();
        definition = definition with
        {
            Graph = new WorkflowGraph(
                definition.Graph.StartNodeId,
                nodes,
                definition.Graph.Edges)
        };

        var validation = await catalog.ValidateDefinitionAsync(definition);

        Assert.Contains(validation.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidProviderModel &&
            issue.Message.Contains("incompatible-node-model", StringComparison.Ordinal));
        Assert.Contains(validation.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidComponentReference &&
            issue.Message.Contains("not declared as supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartupMigrationDoesNotDeserializeRecordsMarkedCurrent()
    {
        var factory = CreateWorkflowFactory("current-marker");
        await using (var arrangeContext = factory.CreateDbContext())
        {
            arrangeContext.Add(new WorkflowComponentRecord
            {
                Id = Guid.NewGuid(),
                Name = "Current component",
                Model = "gpt-5.4",
                ComponentJson = "not-json",
                PromptArtifactId = Guid.NewGuid(),
                PromptVersionId = Guid.NewGuid(),
                PromptGalleryBindingSchemaVersion = 1
            });
            arrangeContext.Add(new WorkflowDefinitionRecord
            {
                WorkflowId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                Name = "Current definition",
                DefinitionJson = "not-json",
                InstructionSnapshotSchemaVersion = 2
            });
            await arrangeContext.SaveChangesAsync();
        }

        var migration = new WorkflowPromptGalleryMigrationService(
            new ThrowingWorkflowComponentLibrary(),
            factory,
            NullLogger<WorkflowPromptGalleryMigrationService>.Instance);

        await migration.EnsureMigratedAsync();
    }

    private static PromptGalleryTestSupport.TestDbContextFactory CreateWorkflowFactory(string testName)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
            [typeof(AgentFrameworkModuleAssemblyMarker).Assembly, typeof(PromptsModuleAssemblyMarker).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-prompt-migration-{testName}-{Guid.NewGuid():N}")
            .Options;
        return new PromptGalleryTestSupport.TestDbContextFactory(options);
    }

    private static LlmCallComponent CreateLegacyComponent()
    {
        var now = DateTimeOffset.UnixEpoch;
        return new LlmCallComponent(
            WorkflowComponentId.New(),
            "Summarize",
            ProviderProfileId: null,
            "gpt-5.4",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            "Summarize the input.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default,
            now,
            now);
    }

    private static WorkflowDefinition CreateLegacyDefinition(WorkflowComponentId componentId)
    {
        var nodeId = new WorkflowNodeId("llm");
        var now = DateTimeOffset.UnixEpoch;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Legacy workflow",
            "Workflow requiring an instruction snapshot backfill.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                nodeId,
                [
                    new WorkflowNode(
                        nodeId,
                        WorkflowNodeKind.LlmCall,
                        "LLM",
                        [],
                        new WorkflowNodeSettings(
                            componentId,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: string.Empty,
                            WorkflowValueShape.Text,
                            WorkflowValueShape.Text))
                ],
                []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowDefinition CreateValidationDefinition(LlmCallComponent component)
    {
        var now = DateTimeOffset.UnixEpoch;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Validated workflow",
            "Workflow used to verify live Prompt Gallery policy.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateValidationNode("start", WorkflowNodeKind.Start),
                    CreateValidationNode(
                        "llm",
                        WorkflowNodeKind.LlmCall,
                        component.Id,
                        component.Instructions),
                    CreateValidationNode("end", WorkflowNodeKind.End)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-llm"),
                        new WorkflowNodeId("start"),
                        SourcePortId: null,
                        new WorkflowNodeId("llm"),
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("llm-to-end"),
                        new WorkflowNodeId("llm"),
                        SourcePortId: null,
                        new WorkflowNodeId("end"),
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowNode CreateValidationNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        string instructions = "")
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                instructions,
                WorkflowValueShape.Text,
                WorkflowValueShape.Text));

    private static ProviderProfile CreateProvider()
        => new(
            Guid.NewGuid(),
            "Workflow provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "TEST_OPENAI_API_KEY",
            "gpt-5.4",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-5.4"],
            Purpose: ProviderProfilePurpose.Chat)
        {
            ModelPrices = [new ProviderModelTokenPrice("gpt-5.4", 2.50m, 0.25m, 15.00m)]
        };

    private sealed class IncompatiblePromptGallery(
        Guid promptArtifactId,
        Guid promptVersionId) : IPromptGalleryService
    {
        public Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(
            Guid requestedPromptArtifactId,
            PromptGalleryConsumerContext context,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
            Guid requestedPromptVersionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(promptVersionId, requestedPromptVersionId);
            return Task.FromResult(Result<PromptVersionSnapshot>.Success(new PromptVersionSnapshot(
                promptArtifactId,
                promptVersionId,
                VersionNumber: 1,
                "Pinned Gallery prompt",
                "Pinned prompt",
                PromptGalleryItemKind.FullPrompt,
                "Pinned prompt content.",
                "Markdown",
                new PromptModelRecommendations(),
                DateTimeOffset.UnixEpoch)));
        }

        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
            PromptGalleryQuery query,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptGalleryItemDetails>> GetItemAsync(
            Guid requestedPromptArtifactId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(promptArtifactId, requestedPromptArtifactId);
            var now = DateTimeOffset.UnixEpoch;
            return Task.FromResult(Result<PromptGalleryItemDetails>.Success(new PromptGalleryItemDetails(
                promptArtifactId,
                ProjectId: null,
                CollectionId: null,
                "Pinned Gallery prompt",
                "Pinned prompt",
                PromptGalleryItemKind.FullPrompt,
                "workflow",
                PromptArtifactStatus.Final,
                IsArchived: false,
                "Pinned prompt content.",
                CurrentVersionNumber: 1,
                Tags: [],
                TemplateTokens: [],
                SupportedModels: [new PromptProviderModel("Anthropic", "claude-sonnet")],
                SupportedConsumers: [PromptGalleryConsumer.Workflow],
                WarningSuppressions: [],
                new PromptModelRecommendations(),
                new PromptGallerySourceInfo(
                    PromptArtifactProvenance.User,
                    Catalog: null,
                    Key: null,
                    GroupKey: null,
                    GroupName: null,
                    ItemKind: null,
                    OrderIndex: null),
                Versions: [new PromptGalleryVersionInfo(promptVersionId, 1, "Pinned", "Markdown", now)],
                now,
                now)));
        }

        public Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(
            PromptGalleryDraft draft,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptVersionSnapshot>> CreateVersionAsync(
            Guid promptArtifactId,
            PromptVersionCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
            Guid promptArtifactId,
            int versionNumber,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(
            IReadOnlyCollection<Guid> promptVersionIds,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(
            IReadOnlyCollection<Guid> promptArtifactIds,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result> ArchiveAsync(
            Guid promptArtifactId,
            bool archived,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result> SetFavoriteAsync(
            Guid promptArtifactId,
            bool favorite,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Result> SetWarningSuppressionAsync(
            Guid promptArtifactId,
            PromptGalleryConsumer consumer,
            PromptCompatibilityIssueCode issueCode,
            bool suppressed,
            CancellationToken cancellationToken = default)
            => throw Unused();

        private static NotSupportedException Unused() => new("This test dependency member is not used.");
    }

    private sealed class InterveningWritePromptGallery(IPromptGalleryService inner) : IPromptGalleryService
    {
        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
            PromptGalleryQuery query,
            CancellationToken cancellationToken = default)
            => inner.SearchAsync(query, cancellationToken);

        public Task<Result<PromptGalleryItemDetails>> GetItemAsync(
            Guid promptArtifactId,
            CancellationToken cancellationToken = default)
            => inner.GetItemAsync(promptArtifactId, cancellationToken);

        public async Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(
            PromptGalleryDraft draft,
            CancellationToken cancellationToken = default)
        {
            var workflowSave = await inner.SaveDraftAsync(draft, cancellationToken);
            if (workflowSave.IsFailure)
            {
                return workflowSave;
            }

            var concurrentSave = await inner.SaveDraftAsync(
                draft with
                {
                    Content = "Concurrent writer content.",
                    ExpectedUpdatedAtUtc = workflowSave.Value.UpdatedAtUtc
                },
                cancellationToken);
            Assert.True(concurrentSave.IsSuccess);
            return workflowSave;
        }

        public Task<Result<PromptVersionSnapshot>> CreateVersionAsync(
            Guid promptArtifactId,
            PromptVersionCreateRequest request,
            CancellationToken cancellationToken = default)
            => inner.CreateVersionAsync(promptArtifactId, request, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
            Guid promptVersionId,
            CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotAsync(promptVersionId, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
            Guid promptArtifactId,
            int versionNumber,
            CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotAsync(promptArtifactId, versionNumber, cancellationToken);

        public Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(
            IReadOnlyCollection<Guid> promptVersionIds,
            CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotsAsync(promptVersionIds, cancellationToken);

        public Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(
            IReadOnlyCollection<Guid> promptArtifactIds,
            CancellationToken cancellationToken = default)
            => inner.GetCompatibilitySnapshotsAsync(promptArtifactIds, cancellationToken);

        public Task<Result> ArchiveAsync(
            Guid promptArtifactId,
            bool archived,
            CancellationToken cancellationToken = default)
            => inner.ArchiveAsync(promptArtifactId, archived, cancellationToken);

        public Task<Result> SetFavoriteAsync(
            Guid promptArtifactId,
            bool favorite,
            CancellationToken cancellationToken = default)
            => inner.SetFavoriteAsync(promptArtifactId, favorite, cancellationToken);

        public Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(
            Guid promptArtifactId,
            PromptGalleryConsumerContext context,
            CancellationToken cancellationToken = default)
            => inner.EvaluateCompatibilityAsync(promptArtifactId, context, cancellationToken);

        public Task<Result> SetWarningSuppressionAsync(
            Guid promptArtifactId,
            PromptGalleryConsumer consumer,
            PromptCompatibilityIssueCode issueCode,
            bool suppressed,
            CancellationToken cancellationToken = default)
            => inner.SetWarningSuppressionAsync(
                promptArtifactId,
                consumer,
                issueCode,
                suppressed,
                cancellationToken);
    }

    private sealed class UnusedPromptGalleryImporter : IPromptGalleryImportService
    {
        public Task<Result<PromptVersionSnapshot>> ImportVersionAsync(
            PromptGalleryImportRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("This test does not import a Gallery prompt.");
    }

    private sealed class ThrowingWorkflowComponentLibrary : IWorkflowComponentLibraryService
    {
        public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<LlmCallComponent?> GetComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<LlmCallComponent> SaveComponentAsync(
            LlmCallComponentSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task DeleteComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => throw Unused();

        private static InvalidOperationException Unused()
            => new("Current migration markers must prevent component library access.");
    }

    private sealed class TestProviderProfileRegistry(ProviderProfile provider) : IProviderProfileRegistry
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);
        }

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(provider.Id == providerId ? provider : null);
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
            Guid? providerId = null,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<Guid> SaveProviderAsync(
            ProviderProfileEditorModel model,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task DeleteProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
            => throw Unused();

        private static NotSupportedException Unused() => new("This test dependency member is not used.");
    }
}
