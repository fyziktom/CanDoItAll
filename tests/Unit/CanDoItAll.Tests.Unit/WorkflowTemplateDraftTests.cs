using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowTemplateDraftTests {
    [Fact]
    public async Task Missing_disabled_or_unknown_default_model_never_writes_a_component() {
        var pack = new WorkflowTemplatePackLoader();
        var validator = new WorkflowDefinitionValidator();
        var catalog = new InMemoryWorkflowCatalogService(validator);
        foreach (var providers in new IReadOnlyList<WorkflowProviderOption>[] {
            [], [Provider() with { IsEnabled = false }], [Provider() with { DefaultModel = "unlisted-model" }],
            [Provider() with { SupportsStructuredOutput = false }]
        }) {
            var service = new WorkflowTemplateDraftService(pack, new Components(catalog, providers), catalog, validator,
                NullLogger<WorkflowTemplateDraftService>.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(pack.Load().Workflows[0].Key));
            Assert.Empty(await catalog.ListComponentsAsync());
            Assert.Empty(await catalog.ListDefinitionsAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Definition_transport_failure_retains_identified_resources_without_reading_current_as_commit_proof(bool afterCommit) {
        var pack = new WorkflowTemplatePackLoader();
        var validator = new WorkflowDefinitionValidator();
        var catalog = new InMemoryWorkflowCatalogService(validator);
        var components = new Components(catalog, [Provider()]);
        var failing = new FailingCatalog(catalog, afterCommit);
        var service = new WorkflowTemplateDraftService(pack, components, failing, validator,
            NullLogger<WorkflowTemplateDraftService>.Instance);
        var unknown = await Assert.ThrowsAsync<WorkflowDraftCreationUnknownException>(() => service.CreateAsync(pack.Load().Workflows[0].Key));
        Assert.Equal(1, components.Saves);
        Assert.Equal(components.SavedId, unknown.ComponentId);
        Assert.Null(components.DeletedId);
        Assert.Equal(0, failing.DetailReads);
        Assert.Equal(unknown.ComponentId, Assert.Single(await catalog.ListComponentsAsync()).Id);
        var definitions = await catalog.ListDefinitionsAsync();
        Assert.Equal(afterCommit ? 1 : 0, definitions.Count);
        if (afterCommit) {
            Assert.Equal(unknown.WorkflowId, Assert.Single(definitions).Id);
        }
        var inspection = await service.InspectAsync(unknown.WorkflowId, unknown.ComponentId);
        Assert.Equal(afterCommit, inspection.Definition is not null);
        Assert.Equal(unknown.ComponentId, inspection.Component!.Id);
        Assert.Equal(1, failing.DetailReads);
        Assert.Equal(1, components.Saves);
        Assert.Null(components.DeletedId);
    }

    [Fact]
    public async Task A_known_definition_refusal_cleans_only_its_exact_acknowledged_component() {
        var pack = new WorkflowTemplatePackLoader();
        var validator = new WorkflowDefinitionValidator();
        var catalog = new InMemoryWorkflowCatalogService(validator);
        var components = new Components(catalog, [Provider()]);
        var service = new WorkflowTemplateDraftService(pack, components, new FailingCatalog(catalog, afterCommit: false, knownRefusal: true), validator,
            NullLogger<WorkflowTemplateDraftService>.Instance);
        await Assert.ThrowsAsync<WorkflowDefinitionValidationException>(() => service.CreateAsync(pack.Load().Workflows[0].Key));
        Assert.Equal(components.SavedId, components.DeletedId);
        Assert.Empty(await catalog.ListComponentsAsync());
        Assert.Empty(await catalog.ListDefinitionsAsync());
    }

    [Fact]
    public async Task A_peer_component_edit_cannot_be_deleted_by_template_cleanup() {
        var pack = new WorkflowTemplatePackLoader();
        var validator = new WorkflowDefinitionValidator();
        var catalog = new InMemoryWorkflowCatalogService(validator);
        var components = new Components(catalog, [Provider()]);
        LlmCallComponent? winner = null;
        var failing = new FailingCatalog(catalog, afterCommit: false, knownRefusal: true, async () => {
            var current = Assert.Single(await catalog.ListComponentsAsync());
            winner = await catalog.SaveComponentAsync(new(current.Id, "Peer component", current.ProviderProfileId,
                current.Model, current.Modality, current.ModelSettings, current.Instructions, current.InputShape,
                current.ResultShape, current.Permissions) { ExpectedRevision = current.Revision });
        });
        var service = new WorkflowTemplateDraftService(pack, components, failing, validator, NullLogger<WorkflowTemplateDraftService>.Instance);
        await Assert.ThrowsAsync<WorkflowDraftCreationUnknownException>(() => service.CreateAsync(pack.Load().Workflows[0].Key));
        Assert.NotNull(winner);
        Assert.Equal(winner.Revision, Assert.Single(await catalog.ListComponentsAsync()).Revision);
        Assert.Equal("Peer component", Assert.Single(await catalog.ListComponentsAsync()).Name);
        Assert.Equal(0, failing.DetailReads);
        Assert.Empty(await catalog.ListDefinitionsAsync());
    }

    private static WorkflowProviderOption Provider() => new(Guid.NewGuid(), "Synthetic provider", ProviderKind.OpenAi,
        ProviderTransportKind.Responses, ProviderProfilePurpose.Chat, "gpt-5.1", ["gpt-5.1"], true, true, true, true, false, false);

    private sealed class Components(IWorkflowComponentLibraryService inner, IReadOnlyList<WorkflowProviderOption> providers) : IWorkflowComponentLibraryService {
        public int Saves { get; private set; }
        public WorkflowComponentId? SavedId { get; private set; }
        public WorkflowComponentId? DeletedId { get; private set; }
        public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(providers);
        public Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(CancellationToken cancellationToken = default) => inner.ListComponentsAsync(cancellationToken);
        public Task<LlmCallComponent?> GetComponentAsync(WorkflowComponentId componentId, CancellationToken cancellationToken = default) => inner.GetComponentAsync(componentId, cancellationToken);
        public async Task<LlmCallComponent> SaveComponentAsync(LlmCallComponentSaveRequest request, CancellationToken cancellationToken = default) {
            Saves++;
            SavedId = request.Id;
            return await inner.SaveComponentAsync(request, cancellationToken);
        }
        public Task DeleteComponentAsync(WorkflowComponentDeleteRequest request, CancellationToken cancellationToken = default) {
            DeletedId = request.Id;
            return inner.DeleteComponentAsync(request, cancellationToken);
        }
    }

    private sealed class FailingCatalog(IWorkflowCatalogService inner, bool afterCommit, bool knownRefusal = false, Func<Task>? beforeRefusal = null) : IWorkflowCatalogService {
        public int DetailReads { get; private set; }
        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(CancellationToken cancellationToken = default) => inner.ListDefinitionsAsync(cancellationToken);
        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(WorkflowId workflowId, WorkflowVersionId? versionId = null, CancellationToken cancellationToken = default) {
            DetailReads++;
            return inner.GetDefinitionAsync(workflowId, versionId, cancellationToken);
        }
        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(WorkflowId workflowId, WorkflowLifecycleStatus status, CancellationToken cancellationToken = default) => inner.GetLatestDefinitionByStatusAsync(workflowId, status, cancellationToken);
        public async Task<WorkflowDefinition> SaveDefinitionAsync(WorkflowDefinitionSaveRequest request, CancellationToken cancellationToken = default) {
            if (beforeRefusal is not null) {
                await beforeRefusal();
            }
            if (knownRefusal) {
                throw new WorkflowDefinitionValidationException("Known precommit validation refusal.");
            }
            if (afterCommit) {
                await inner.SaveDefinitionAsync(request, cancellationToken);
            }
            throw new IOException("Injected definition response failure.");
        }
        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(WorkflowDefinitionStatusChangeRequest request, CancellationToken cancellationToken = default) => inner.ChangeDefinitionStatusAsync(request, cancellationToken);
        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(WorkflowId workflowId, WorkflowVersionId? versionId = null, CancellationToken cancellationToken = default) => inner.ExportDefinitionAsync(workflowId, versionId, cancellationToken);
        public Task<WorkflowDefinition> ImportDefinitionAsync(WorkflowDefinitionImportRequest request, CancellationToken cancellationToken = default) => inner.ImportDefinitionAsync(request, cancellationToken);
        public Task DeleteDefinitionAsync(WorkflowDefinitionDeleteRequest request, CancellationToken cancellationToken = default) => inner.DeleteDefinitionAsync(request, cancellationToken);
        public Task<WorkflowValidationResult> ValidateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default) => inner.ValidateDefinitionAsync(definition, cancellationToken);
    }
}
