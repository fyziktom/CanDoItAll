using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Authoring;

public sealed class WorkflowDocumentOwner(IWorkflowCatalogService catalog, ILogger logger) {
    private bool pending;
    private bool unknown;
    public WorkflowDefinition? Accepted { get; private set; }

    public WorkflowDocumentOperations Operations => new(SaveAsync, catalog.ValidateDefinitionAsync);

    private async Task<WorkflowDefinitionSaveOutcome> SaveAsync(WorkflowDefinitionSaveRequest request, CancellationToken owner) {
        if (owner.IsCancellationRequested) {
            return new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Retired);
        }
        if (pending || unknown) {
            return new WorkflowDefinitionSaveOutcome.Unknown();
        }
        if (string.IsNullOrWhiteSpace(request.Name)) {
            return new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Validation);
        }
        pending = true;
        try {
            Accepted = await catalog.SaveDefinitionAsync(request, CancellationToken.None);
            return new WorkflowDefinitionSaveOutcome.Accepted(Accepted);
        } catch (WorkflowDefinitionValidationException) {
            return new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Validation);
        } catch (WorkflowDefinitionConcurrencyException) {
            return new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Conflict);
        } catch (Exception error) {
            unknown = true;
            logger.LogWarning("Workflow save acknowledgement unavailable for {WorkflowId}/{ExpectedVersionId}: {FailureType}",
                request.Id, request.ExpectedVersionId, error.GetType().Name);
            return new WorkflowDefinitionSaveOutcome.Unknown();
        } finally {
            pending = false;
        }
    }
}
