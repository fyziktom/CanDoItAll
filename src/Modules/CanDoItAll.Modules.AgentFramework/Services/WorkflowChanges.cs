using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowChanges(EditingChangeFeed feed, EditingScope scope, EditingOperationContext operations, ILogger<WorkflowChanges> logger) {
    private readonly AsyncLocal<Func<CancellationToken, ValueTask>?> commitAdmission = new();
    public EditingScope Scope { get; } = scope;
    public IEditingChangeSubscription Subscribe(WorkflowId? id = null)
        => feed.Subscribe(new(Scope, EditingOwner.WorkflowDefinition, id?.Value));

    public IEditingChangeSubscription SubscribeComponents()
        => feed.Subscribe(new(Scope, EditingOwner.WorkflowComponent));

    public void PublishComponent(WorkflowComponentId id, EditingChangeKind kind) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.WorkflowComponent, id.Value), kind, origin?.EditorId, origin?.OperationId);
    }

    internal void ReportComponentFollowUpFailure(WorkflowComponentId id, Guid revision, Exception error)
        => logger.LogWarning("Workflow component {ComponentId}/{Revision} committed; refresh is needed after {FailureType}. Do not replay creation.",
            id.Value, revision, error.GetType().Name);

    public IDisposable Enter(EditingOperationIdentity operation) => operations.Enter(operation);

    public void PublishSettings<T>(EditingOwner owner, T accepted) where T : class {
        var origin = operations.Current;
        try {
            feed.Publish(new(Scope, owner, Scope.Id), EditingChangeKind.EditableContentChanged, origin?.EditorId, origin?.OperationId);
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            logger.LogWarning("Settings {Owner} committed; refresh is required after {FailureType}.", owner, failure.GetType().Name);
            throw new CanDoItAll.AgentFramework.Workflows.Abstractions.SettingsCommittedException<T>(accepted, failure);
        }
    }

    public IDisposable EnterUiWrite(Func<CancellationToken, ValueTask> admission) {
        var previous = commitAdmission.Value;
        commitAdmission.Value = admission;
        return new AdmissionLease(() => commitAdmission.Value = previous);
    }

    internal ValueTask DemandCommitAsync(CancellationToken cancellationToken)
        => commitAdmission.Value?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;

    private sealed class AdmissionLease(Action restore) : IDisposable {
        private Action? release = restore;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }

    public void Publish(WorkflowId id, EditingChangeKind kind) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.WorkflowDefinition, id.Value), kind, origin?.EditorId, origin?.OperationId);
    }
}
