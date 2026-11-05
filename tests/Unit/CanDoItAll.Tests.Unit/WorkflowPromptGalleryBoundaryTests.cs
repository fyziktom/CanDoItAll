using CanDoItAll.Tests.Support;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Authoring;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowPromptGalleryBoundaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prompt_binding_assigns_a_diagnostic_identity_before_dispatch_and_never_replays_unknown(bool committed) {
        var factory = CreateWorkflowFactory("prompt-binding-original");
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var provider = CreateProvider();
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery,
            new TestProviderProfileRegistry(provider), new ProviderProfileService());
        var legacy = CreateLegacyComponent();
        var source = await catalog.SaveComponentAsync(new(null, legacy.Name, provider.Id, provider.DefaultModel,
            legacy.Modality, legacy.ModelSettings, legacy.Instructions, legacy.InputShape, legacy.ResultShape, legacy.Permissions));
        var library = new LostBindingAcknowledgement(catalog, committed);
        var owner = new WorkflowPromptBindingOwner(gallery, library, NullLogger.Instance,
            WorkflowObservationTestSupport.Create(), static (_, _) => ValueTask.CompletedTask);
        var selection = new PromptGallerySelection(source.PromptArtifactId!.Value, source.PromptVersionId!.Value, 1,
            source.Name, "Fixture", PromptGalleryItemKind.FullPrompt, source.Instructions, [], [], new());
        var request = new WorkflowPromptBindingRequest(selection, Assert.Single(await catalog.ListProviderOptionsAsync()), source.Model, null);

        var outcome = Assert.IsType<WorkflowPromptBindingOutcome.Unknown>(await owner.BindAsync(request, CancellationToken.None));
        Assert.NotNull(library.SubmittedId);
        Assert.NotEqual(Guid.Empty, library.SubmittedId.Value.Value);
        Assert.Equal(library.SubmittedId, outcome.ComponentId);
        Assert.Equal(outcome, Assert.IsType<WorkflowPromptBindingOutcome.Unknown>(await owner.BindAsync(request, CancellationToken.None)));
        Assert.Equal(1, library.Saves);
        Assert.Empty(owner.Accepted);
        var stored = (await catalog.ListComponentsAsync()).Where(component => component.Id != source.Id).ToArray();
        if (committed) {
            Assert.Equal(library.SubmittedId, Assert.Single(stored).Id);
        } else {
            Assert.Empty(stored);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prompt_binding_carries_the_original_editor_access_into_the_native_writer(bool revokeAtDispatch) {
        var factory = CreateWorkflowFactory("prompt-binding-access");
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var provider = CreateProvider();
        var changes = WorkflowObservationTestSupport.Create();
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery,
            new TestProviderProfileRegistry(provider), new ProviderProfileService(), changes);
        var legacy = CreateLegacyComponent();
        var source = await catalog.SaveComponentAsync(new(null, legacy.Name, provider.Id, provider.DefaultModel,
            legacy.Modality, legacy.ModelSettings, legacy.Instructions, legacy.InputShape, legacy.ResultShape, legacy.Permissions));
        var allowed = revokeAtDispatch;
        var library = new LostBindingAcknowledgement(catalog, committed: true) { BeforeSave = () => allowed = false };
        var owner = new WorkflowPromptBindingOwner(gallery, library, NullLogger.Instance, changes,
            (_, _) => allowed ? ValueTask.CompletedTask : ValueTask.FromException(new UnauthorizedAccessException()));
        var selection = new PromptGallerySelection(source.PromptArtifactId!.Value, source.PromptVersionId!.Value, 1,
            source.Name, "Fixture", PromptGalleryItemKind.FullPrompt, source.Instructions, [], [], new());
        var request = new WorkflowPromptBindingRequest(selection, Assert.Single(await catalog.ListProviderOptionsAsync()), source.Model, null);

        Assert.IsType<WorkflowPromptBindingOutcome.Rejected>(await owner.BindAsync(request, CancellationToken.None));
        Assert.Equal(revokeAtDispatch ? 1 : 0, library.Saves);
        Assert.Equal(source.Id, Assert.Single(await catalog.ListComponentsAsync()).Id);
        Assert.Empty(owner.Accepted);
    }

    [Fact]
    public async Task Known_component_refusal_does_not_turn_into_an_unknown_binding_or_block_an_explicit_retry() {
        var factory = CreateWorkflowFactory("prompt-binding-conflict");
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var provider = CreateProvider();
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery,
            new TestProviderProfileRegistry(provider), new ProviderProfileService());
        var legacy = CreateLegacyComponent();
        var source = await catalog.SaveComponentAsync(new(null, legacy.Name, provider.Id, provider.DefaultModel,
            legacy.Modality, legacy.ModelSettings, legacy.Instructions, legacy.InputShape, legacy.ResultShape, legacy.Permissions));
        var library = new LostBindingAcknowledgement(catalog, committed: false) {
            BeforeSave = () => throw new WorkflowComponentConcurrencyException(source.Id)
        };
        var owner = new WorkflowPromptBindingOwner(gallery, library, NullLogger.Instance,
            WorkflowObservationTestSupport.Create(), static (_, _) => ValueTask.CompletedTask);
        var selection = new PromptGallerySelection(source.PromptArtifactId!.Value, source.PromptVersionId!.Value, 1,
            source.Name, "Fixture", PromptGalleryItemKind.FullPrompt, source.Instructions, [], [], new());
        var request = new WorkflowPromptBindingRequest(selection, Assert.Single(await catalog.ListProviderOptionsAsync()), source.Model, null);

        Assert.IsType<WorkflowPromptBindingOutcome.Rejected>(await owner.BindAsync(request, CancellationToken.None));
        Assert.IsType<WorkflowPromptBindingOutcome.Rejected>(await owner.BindAsync(request, CancellationToken.None));
        Assert.Equal(2, library.Saves);
        Assert.Equal(source.Id, Assert.Single(await catalog.ListComponentsAsync()).Id);
    }

    private sealed class LostBindingAcknowledgement(IWorkflowComponentLibraryService inner, bool committed) : IWorkflowComponentLibraryService {
        public WorkflowComponentId? SubmittedId { get; private set; }
        public int Saves { get; private set; }
        public Action? BeforeSave { get; init; }
        public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(CancellationToken cancellationToken = default)
            => inner.ListProviderOptionsAsync(cancellationToken);
        public Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default)
            => inner.ListComponentsAsync(cancellationToken);
        public Task<LlmCallComponent?> GetComponentAsync(WorkflowComponentId id, CancellationToken cancellationToken = default)
            => inner.GetComponentAsync(id, cancellationToken);
        public Task DeleteComponentAsync(WorkflowComponentDeleteRequest request, CancellationToken cancellationToken = default)
            => inner.DeleteComponentAsync(request, cancellationToken);
        public async Task<LlmCallComponent> SaveComponentAsync(LlmCallComponentSaveRequest request, CancellationToken cancellationToken = default) {
            SubmittedId = request.Id;
            Saves++;
            BeforeSave?.Invoke();
            if (committed) {
                await inner.SaveComponentAsync(request, cancellationToken);
            }
            throw new IOException("Synthetic component acknowledgement failure");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Component_model_upgrade_updates_only_its_implicitly_owned_prompt(bool explicitBinding) {
        var factory = CreateWorkflowFactory(nameof(Component_model_upgrade_updates_only_its_implicitly_owned_prompt));
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var provider = CreateProvider();
        provider = provider with {
            SuggestedModels = [provider.DefaultModel, OpenAiModelIds.Gpt6Luna],
            ModelPrices = ProviderPricingDefaults.MergeAuthoritativeKnownDefaultPrices(provider.Kind, OpenAiModelIds.Gpt6Luna, provider.ModelPrices)
        };
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery,
            new TestProviderProfileRegistry(provider), new ProviderProfileService());
        var legacy = CreateLegacyComponent();
        var request = new LlmCallComponentSaveRequest(null, legacy.Name, provider.Id, provider.DefaultModel,
            legacy.Modality, legacy.ModelSettings, legacy.Instructions, legacy.InputShape, legacy.ResultShape, legacy.Permissions);
        var original = await catalog.SaveComponentAsync(request);
        var upgrade = request with { Id = original.Id, ExpectedRevision = original.Revision, Model = OpenAiModelIds.Gpt6Luna,
            PromptArtifactId = explicitBinding ? original.PromptArtifactId : null };

        if (explicitBinding) {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.SaveComponentAsync(upgrade));
            Assert.Contains("not declared as supported", failure.Message, StringComparison.Ordinal);
            Assert.Equal(original.Model, (await catalog.GetComponentAsync(original.Id))!.Model);
        } else {
            var updated = await catalog.SaveComponentAsync(upgrade);
            Assert.Equal(OpenAiModelIds.Gpt6Luna, updated.Model);
            Assert.Equal(original.PromptArtifactId, updated.PromptArtifactId);
            Assert.NotEqual(original.PromptVersionId, updated.PromptVersionId);
            Assert.Equal(original.Instructions, (await gallery.GetVersionSnapshotAsync(original.PromptVersionId!.Value)).Value!.Content);
            var item = (await gallery.GetItemAsync(updated.PromptArtifactId!.Value)).Value!;
            Assert.Contains(item.SupportedModels, model => model.Model == original.Model);
            Assert.Contains(item.SupportedModels, model => model.Model == updated.Model && model.IsPreferred);
        }
    }

    [Fact]
    public async Task PersistentComponentSaveRejectsIncompatibleGalleryBindingBeforePersisting()
    {
        var factory = CreateWorkflowFactory(nameof(PersistentComponentSaveRejectsIncompatibleGalleryBindingBeforePersisting));
        var promptArtifactId = Guid.NewGuid();
        var promptVersionId = Guid.NewGuid();
        var promptGallery = new IncompatiblePromptGallery(promptArtifactId, promptVersionId);
        var provider = CreateProvider();
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), promptGallery, PromptGalleryTestSupport.CreateService(factory.Prompts),
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
    public async Task Foreign_prompt_binding_requires_an_immutable_version_and_cannot_update_the_foreign_draft()
    {
        var factory = CreateWorkflowFactory("publication-concurrency");
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
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
        var version = await gallery.CreateVersionAsync(promptId, new("Initial publication", initialSave.Value.UpdatedAtUtc));
        Assert.True(version.IsSuccess);
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery,
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

        Assert.Contains("explicit immutable version", exception.Message, StringComparison.Ordinal);
        await using (var dbContext = factory.CreateDbContext()) {
            Assert.Empty(await dbContext.Set<WorkflowComponentRecord>().ToListAsync());
        }
        var bound = await catalog.SaveComponentAsync(request with { PromptVersionId = version.Value!.PromptVersionId });
        Assert.Equal("Initial published content.", bound.Instructions);
        await Assert.ThrowsAsync<WorkflowComponentConcurrencyException>(() => catalog.SaveComponentAsync(request with {
            Id = bound.Id, ExpectedRevision = bound.Revision, PromptArtifactId = null, Instructions = "Unrelated replacement"
        }));
        var current = Assert.IsType<PromptGalleryItemDetails>((await gallery.GetItemAsync(promptId)).Value);
        Assert.Equal("Initial published content.", current.DraftContent);
        Assert.Equal(PromptArtifactStatus.Final, current.Status);
        Assert.Single(current.Versions);
    }

    [Fact]
    public async Task StartupMigrationBatchesLegacyRecordsAndPersistsDurableMarkers()
    {
        var factory = CreateWorkflowFactory("legacy-migration");
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery);
        var providerProfileId = Guid.NewGuid();
        var component = CreateLegacyComponent() with { ProviderProfileId = providerProfileId };
        var definition = CreateLegacyDefinition(component.Id);
        var originalDefinitionJson = JsonSerializer.Serialize(definition, JsonOptions);
        await using (var arrangeContext = factory.CreateDbContext())
        {
            var definitionRecord = WorkflowDefinitionRecord.FromDefinition(definition, revision: 1);
            definitionRecord.InstructionSnapshotSchemaVersion = 0;
            arrangeContext.Add(new WorkflowComponentRecord
            {
                Id = component.Id.Value,
                Revision = Guid.NewGuid(),
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
            factory.Changes,
            NullLogger<WorkflowPromptGalleryMigrationService>.Instance);

        await migration.EnsureMigratedAsync();

        string migratedComponentJson;
        string migratedDefinitionJson;
        Guid migratedVersionId;
        await using (var assertContext = factory.CreateDbContext())
        {
            var componentRecord = await assertContext.Set<WorkflowComponentRecord>().SingleAsync();
            Assert.Equal(1, componentRecord.PromptGalleryBindingSchemaVersion);
            Assert.NotNull(componentRecord.PromptArtifactId);
            Assert.NotNull(componentRecord.PromptVersionId);
            Assert.DoesNotContain("instructions", componentRecord.ComponentJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(component.Instructions, componentRecord.ComponentJson, StringComparison.Ordinal);
            migratedComponentJson = componentRecord.ComponentJson;

            var originalRecord = await assertContext.Set<WorkflowDefinitionRecord>().SingleAsync(record => record.VersionId == definition.VersionId.Value);
            Assert.Equal(originalDefinitionJson, originalRecord.DefinitionJson);
            Assert.Equal(definition.UpdatedAtUtc, originalRecord.UpdatedAtUtc);
            migratedVersionId = (await assertContext.Set<WorkflowDefinitionHeadRecord>().SingleAsync()).VersionId;
            Assert.NotEqual(definition.VersionId.Value, migratedVersionId);
            Assert.Equal(2, await assertContext.Set<WorkflowDefinitionRecord>().CountAsync());
            var definitionRecord = await assertContext.Set<WorkflowDefinitionRecord>().SingleAsync(record => record.VersionId == migratedVersionId);
            Assert.Equal(3, definitionRecord.InstructionSnapshotSchemaVersion);
            migratedDefinitionJson = definitionRecord.DefinitionJson;
            var migratedDefinition = JsonSerializer.Deserialize<WorkflowDefinition>(
                definitionRecord.DefinitionJson,
                JsonOptions);
            var llmNodes = migratedDefinition!.Graph.Nodes.ToDictionary(node => node.Id.Value);
            Assert.Equal(component.Instructions, llmNodes["llm-blank"].Settings.Instructions);
            Assert.Equal(component.Instructions, llmNodes["llm-placeholder"].Settings.Instructions);
            Assert.Equal(
                "Keep this custom immutable workflow instruction.",
                llmNodes["llm-custom"].Settings.Instructions);
            Assert.All(llmNodes.Values, llmNode =>
            {
                Assert.Equal(providerProfileId, llmNode.Settings.ProviderProfileId);
                Assert.Equal(component.Model, llmNode.Settings.Model);
            });

            await using var promptContext = factory.Prompts.CreateDbContext();
            Assert.Single(await promptContext.Set<PromptArtifact>().ToArrayAsync());
            Assert.Single(await promptContext.Set<PromptVersion>().ToArrayAsync());
        }

        var hydratedComponent = await catalog.GetComponentAsync(component.Id);
        Assert.NotNull(hydratedComponent);
        Assert.Equal(component.Instructions, hydratedComponent.Instructions);

        await migration.EnsureMigratedAsync();

        await using var idempotencyContext = factory.CreateDbContext();
        var persistedComponent = await idempotencyContext.Set<WorkflowComponentRecord>().SingleAsync();
        var persistedDefinition = await idempotencyContext.Set<WorkflowDefinitionRecord>().SingleAsync(record => record.VersionId == migratedVersionId);
        Assert.Equal(migratedComponentJson, persistedComponent.ComponentJson);
        Assert.Equal(migratedDefinitionJson, persistedDefinition.DefinitionJson);
        Assert.Equal(2, await idempotencyContext.Set<WorkflowDefinitionRecord>().CountAsync());
        Assert.Equal(originalDefinitionJson, (await idempotencyContext.Set<WorkflowDefinitionRecord>()
            .SingleAsync(record => record.VersionId == definition.VersionId.Value)).DefinitionJson);
        await using var idempotencyPrompts = factory.Prompts.CreateDbContext();
        Assert.Single(await idempotencyPrompts.Set<PromptArtifact>().ToArrayAsync());
        Assert.Single(await idempotencyPrompts.Set<PromptVersion>().ToArrayAsync());
    }

    [Fact]
    public async Task StartupMigrationPreservesLegacyDefinitionWhenReferencedComponentIsMissing()
    {
        var factory = CreateWorkflowFactory("missing-component-migration");
        var componentId = WorkflowComponentId.New();
        var definition = CreateLegacyDefinition(componentId);
        var originalDefinitionJson = string.Empty;
        await using (var arrangeContext = factory.CreateDbContext())
        {
            var record = WorkflowDefinitionRecord.FromDefinition(definition, revision: 1);
            record.InstructionSnapshotSchemaVersion = 0;
            originalDefinitionJson = record.DefinitionJson;
            arrangeContext.Add(record);
            await arrangeContext.SaveChangesAsync();
        }

        var componentLibrary = new MissingWorkflowComponentLibrary();
        var logger = new RecordingLogger<WorkflowPromptGalleryMigrationService>();
        var migration = new WorkflowPromptGalleryMigrationService(
            componentLibrary,
            factory,
            factory.Changes,
            logger);

        await migration.EnsureMigratedAsync();

        await using (var assertContext = factory.CreateDbContext())
        {
            var record = await assertContext.Set<WorkflowDefinitionRecord>().SingleAsync();
            Assert.Equal(3, record.InstructionSnapshotSchemaVersion);
            Assert.Equal(originalDefinitionJson, record.DefinitionJson);
        }

        Assert.Equal(1, componentLibrary.GetComponentCallCount);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains(definition.Id.Value.ToString(), StringComparison.OrdinalIgnoreCase) &&
            entry.Message.Contains(definition.VersionId.Value.ToString(), StringComparison.OrdinalIgnoreCase) &&
            entry.Message.Contains("llm-placeholder", StringComparison.Ordinal) &&
            entry.Message.Contains(componentId.Value.ToString(), StringComparison.OrdinalIgnoreCase));

        await migration.EnsureMigratedAsync();

        Assert.Equal(1, componentLibrary.GetComponentCallCount);
        await using var idempotencyContext = factory.CreateDbContext();
        var persistedRecord = await idempotencyContext.Set<WorkflowDefinitionRecord>().SingleAsync();
        Assert.Equal(3, persistedRecord.InstructionSnapshotSchemaVersion);
        Assert.Equal(originalDefinitionJson, persistedRecord.DefinitionJson);
    }

    [Fact]
    public async Task ComponentHydrationFailsExplicitlyWhenBoundPromptVersionIsMissing()
    {
        var factory = CreateWorkflowFactory("missing-version");
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery);
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
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery);
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
        var gallery = PromptGalleryTestSupport.CreateService(factory.Prompts);
        var provider = CreateProvider();
        var catalog = factory.CreateCatalog(new WorkflowDefinitionValidator(), gallery, gallery,
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
                InstructionSnapshotSchemaVersion = 3
            });
            await arrangeContext.SaveChangesAsync();
        }

        var migration = new WorkflowPromptGalleryMigrationService(
            new ThrowingWorkflowComponentLibrary(),
            factory,
            factory.Changes,
            NullLogger<WorkflowPromptGalleryMigrationService>.Instance);

        await migration.EnsureMigratedAsync();
    }

    private static WorkflowTestDbContextFactory CreateWorkflowFactory(string testName) {

        var databaseName = $"workflow-prompt-migration-{testName}-{Guid.NewGuid():N}";
        var store = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<WorkflowDbContext>().UseInMemoryDatabase(databaseName, store).Options;
        var promptOptions = new DbContextOptionsBuilder<PromptsDbContext>().UseInMemoryDatabase(databaseName, store).Options;
        return new(options, new PromptGalleryTestSupport.TestDbContextFactory(promptOptions), databaseName);
    }

    private sealed class WorkflowTestDbContextFactory(DbContextOptions<WorkflowDbContext> options,
        IDbContextFactory<PromptsDbContext> prompts, string databaseName) : IDbContextFactory<WorkflowDbContext> {
        public IDbContextFactory<PromptsDbContext> Prompts { get; } = prompts;
        public WorkflowChanges Changes { get; } = WorkflowObservationTestSupport.Create();
        public PersistentWorkflowCatalogService CreateCatalog(IWorkflowDefinitionValidator validator,
            IPromptGalleryService reader, PromptsService owner, IProviderProfileRegistry? providers = null,
            IProviderProfileService? providerService = null, WorkflowChanges? changes = null) {
            var mutation = PromptGalleryTestSupport.CreateMutations(Prompts, owner, databaseName);
            return new(changes ?? Changes, this, validator, reader, mutation.Mutations, mutation.Transactions,
                providers, providerService);
        }

        public WorkflowDbContext CreateDbContext() => new(options);
        public Task<WorkflowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
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
        var nodeId = new WorkflowNodeId("llm-blank");
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
                    CreateLegacyNode("llm-blank", componentId, string.Empty),
                    CreateLegacyNode(
                        "llm-placeholder",
                        componentId,
                        WorkflowInstructionSnapshotPolicy.LegacyTemplatePlaceholder),
                    CreateLegacyNode(
                        "llm-custom",
                        componentId,
                        "Keep this custom immutable workflow instruction.")
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

    private static WorkflowNode CreateLegacyNode(
        string id,
        WorkflowComponentId componentId,
        string instructions)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.LlmCall,
            "LLM",
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                instructions,
                WorkflowValueShape.Text,
                WorkflowValueShape.Text));

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
            WorkflowComponentDeleteRequest request,
            CancellationToken cancellationToken = default)
            => throw Unused();

        private static InvalidOperationException Unused()
            => new("Current migration markers must prevent component library access.");
    }

    private sealed class MissingWorkflowComponentLibrary : IWorkflowComponentLibraryService
    {
        public int GetComponentCallCount { get; private set; }

        public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task<LlmCallComponent?> GetComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetComponentCallCount++;
            return Task.FromResult<LlmCallComponent?>(null);
        }

        public Task<LlmCallComponent> SaveComponentAsync(
            LlmCallComponentSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw Unused();

        public Task DeleteComponentAsync(
            WorkflowComponentDeleteRequest request,
            CancellationToken cancellationToken = default)
            => throw Unused();

        private static NotSupportedException Unused()
            => new("This migration test only looks up a missing workflow component.");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class TestProviderProfileRegistry(ProviderProfile provider) :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource
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
