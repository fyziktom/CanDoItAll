using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Authoring;

public sealed class WorkflowDocumentOwner(IWorkflowCatalogService catalog, ILogger logger, WorkflowChanges changes,
    Func<bool, CancellationToken, ValueTask> authorize) {
    private readonly Guid editorId = Guid.NewGuid();
    private bool pending;
    private bool unknown;
    public WorkflowDefinition? Accepted { get; private set; }

    public WorkflowDocumentOperations Operations => new(SaveAsync, ValidateAsync) { ReadCurrent = ReadCurrentAsync };

    private async Task<WorkflowDefinition?> ReadCurrentAsync(WorkflowId id, CancellationToken owner) {
        await authorize(false, owner);
        var current = await catalog.GetDefinitionAsync(id, cancellationToken: owner);
        await authorize(false, owner);
        owner.ThrowIfCancellationRequested();
        if (current is not null && current.Definition.Id != id) {
            throw new InvalidOperationException("The current workflow read returned another document.");
        }
        return current?.Definition;
    }

    private async Task<WorkflowValidationResult> ValidateAsync(WorkflowDefinition definition, CancellationToken owner) {
        await authorize(false, owner);
        var validation = await catalog.ValidateDefinitionAsync(definition, owner);
        await authorize(false, owner);
        return validation;
    }

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
            await authorize(true, owner);
            using var admission = changes.EnterUiWrite(token => authorize(true, token));
            using var operation = changes.Enter(new(editorId, Guid.NewGuid()));
            Accepted = await catalog.SaveDefinitionAsync(request, CancellationToken.None);
            return new WorkflowDefinitionSaveOutcome.Accepted(Accepted);
        } catch (UnauthorizedAccessException) {
            return new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Access);
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
