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

    [Fact]
    public async Task Definition_failure_compensates_only_the_component_owned_by_this_attempt() {
        var pack = new WorkflowTemplatePackLoader();
        var validator = new WorkflowDefinitionValidator();
        var catalog = new InMemoryWorkflowCatalogService(validator);
        var components = new Components(catalog, [Provider()]);
        var service = new WorkflowTemplateDraftService(pack, components, new FailingCatalog(catalog, afterCommit: false), validator,
            NullLogger<WorkflowTemplateDraftService>.Instance);
        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(pack.Load().Workflows[0].Key));
        Assert.Equal(1, components.Saves);
        Assert.Equal(components.SavedId, components.DeletedId);
        Assert.Empty(await catalog.ListComponentsAsync());
        Assert.Empty(await catalog.ListDefinitionsAsync());
    }

    [Fact]
    public async Task Failure_after_definition_commit_returns_the_stored_draft_without_deleting_its_component() {
        var pack = new WorkflowTemplatePackLoader();
        var validator = new WorkflowDefinitionValidator();
        var catalog = new InMemoryWorkflowCatalogService(validator);
        var components = new Components(catalog, [Provider()]);
        var service = new WorkflowTemplateDraftService(pack, components, new FailingCatalog(catalog, afterCommit: true), validator,
            NullLogger<WorkflowTemplateDraftService>.Instance);
        var saved = await service.CreateAsync(pack.Load().Workflows[0].Key);
        Assert.Equal(WorkflowLifecycleStatus.Draft, saved.Status);
        Assert.Equal(saved.Id, Assert.Single(await catalog.ListDefinitionsAsync()).Id);
        var component = Assert.Single(await catalog.ListComponentsAsync());
        Assert.True(component.Permissions.RequiresApprovalForExternalCalls);
        Assert.Null(components.DeletedId);
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
        public Task DeleteComponentAsync(WorkflowComponentId componentId, CancellationToken cancellationToken = default) {
            DeletedId = componentId;
            return inner.DeleteComponentAsync(componentId, cancellationToken);
        }
    }

    private sealed class FailingCatalog(IWorkflowCatalogService inner, bool afterCommit) : IWorkflowCatalogService {
        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(CancellationToken cancellationToken = default) => inner.ListDefinitionsAsync(cancellationToken);
        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(WorkflowId workflowId, WorkflowVersionId? versionId = null, CancellationToken cancellationToken = default) => inner.GetDefinitionAsync(workflowId, versionId, cancellationToken);
        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(WorkflowId workflowId, WorkflowLifecycleStatus status, CancellationToken cancellationToken = default) => inner.GetLatestDefinitionByStatusAsync(workflowId, status, cancellationToken);
        public async Task<WorkflowDefinition> SaveDefinitionAsync(WorkflowDefinitionSaveRequest request, CancellationToken cancellationToken = default) {
            if (afterCommit) {
                await inner.SaveDefinitionAsync(request, cancellationToken);
            }
            throw new IOException("Injected definition response failure.");
        }
        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(WorkflowDefinitionStatusChangeRequest request, CancellationToken cancellationToken = default) => inner.ChangeDefinitionStatusAsync(request, cancellationToken);
        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(WorkflowId workflowId, WorkflowVersionId? versionId = null, CancellationToken cancellationToken = default) => inner.ExportDefinitionAsync(workflowId, versionId, cancellationToken);
        public Task<WorkflowDefinition> ImportDefinitionAsync(WorkflowDefinitionImportRequest request, CancellationToken cancellationToken = default) => inner.ImportDefinitionAsync(request, cancellationToken);
        public Task DeleteDefinitionAsync(WorkflowId workflowId, CancellationToken cancellationToken = default) => inner.DeleteDefinitionAsync(workflowId, cancellationToken);
        public Task<WorkflowValidationResult> ValidateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default) => inner.ValidateDefinitionAsync(definition, cancellationToken);
    }
}
