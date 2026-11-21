using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class RemoteWorkflowImportFeature(RemoteTemplateConnectionReader reader, RemoteWorkflowDraftWriter writer,
    IWorkflowExecutorCatalog executors) : IRemoteTemplateImportFeature {
    public RemoteTemplateKind Kind => RemoteTemplateKind.Workflow;

    public async Task<IReadOnlyList<RemoteTemplateRow>> ListAsync(RemoteInstanceSelection source, CancellationToken token) {
        var rows = await reader.ReadAsync<RemoteTemplateRow[]>(source.Id, source.Version, RemoteWorkflowRoutes.Catalog, token);
        if (rows.Any(row => !RemoteWorkflowProjection.ValidRow(row) || row.PublisherId != rows[0].PublisherId) || rows.DistinctBy(row => row.Id).Count() != rows.Length) {
            throw new RemoteInstanceReadException("The remote workflow catalog contains invalid definitions.");
        }
        return rows;
    }

    public async Task<RemoteTemplatePreview> PreviewAsync(RemoteInstanceSelection source, RemoteTemplateRow row, CancellationToken token) {
        var document = await reader.ReadAsync<RemoteWorkflowDocument>(source.Id, source.Version, RemoteWorkflowRoutes.Details(row.Id), token);
        if (document.Row != row || row.VersionId.GetValueOrDefault() == Guid.Empty) {
            throw new RemoteInstanceReadException("The shared workflow or its components changed. Refresh the catalog.");
        }
        RemoteWorkflowProjection.Validate(document);
        foreach (var node in document.Graph.Nodes) {
            if (node.Settings.ExecutorId is { } id && (!executors.TryGetExecutor(id, out var descriptor) || !descriptor.CanExecute)) {
                throw RemoteWorkflowExecutorSettings.Error(node, $"Make executor '{id.Value}' available on this instance before importing.");
            }
        }
        return new Preview(document);
    }

    public async Task<Guid> ImportAsync(RemoteInstanceSelection source, RemoteTemplatePreview preview, IReadOnlyList<RemoteTemplateModelChoice> models,
        AgentExecutionActivityWorkspaceIdentity original, CancellationToken token) {
        if (preview is not Preview workflow) {
            throw new RemoteInstanceReadException("Select a shared workflow to import.");
        }
        async ValueTask CheckSource(CancellationToken current) {
            await reader.RequireCurrentAsync(source.Id, source.Version, current);
        }
        try {
            return await writer.CreateAsync(source.Id, workflow.Document, models, original, CheckSource, token);
        } catch (WorkflowDefinitionValidationException error) {
            throw new RemoteInstanceReadException(error.Message);
        }
    }

    private sealed record Preview(RemoteWorkflowDocument Document) : RemoteTemplatePreview(Document.Row, Document.Models) {
        public override IReadOnlyList<string> Dependencies => [.. Document.Components.Select(component => component.Name),
            .. Document.Graph.Nodes.Select(node => node.Settings.ExecutorId).OfType<WorkflowExecutorId>().Distinct().Select(id => id.Value)];
    }
}
