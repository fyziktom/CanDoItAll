using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal static class WorkflowInMemoryRuntimeFactory
{
    public static WorkflowRuntimeManager Create(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowEventSink? eventSink,
        IWorkflowUsageObservationStore? usageStore)
        => Create(
            backends,
            store,
            new InMemoryWorkflowExternalRequestBoundaryStore(store),
            new WorkflowActiveRunRegistry(),
            TimeProvider.System,
            eventSink,
            usageStore);

    public static WorkflowRuntimeManager Create(
        IEnumerable<IWorkflowExecutionBackend> backends,
        InMemoryWorkflowRunStore store,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
        IWorkflowEventSink? eventSink,
        InMemoryWorkflowUsageObservationStore? usageStore)
        => Create(
            backends,
            store,
            new InMemoryWorkflowExternalRequestBoundaryStore(store, checkpointPayloadStore),
            new WorkflowActiveRunRegistry(),
            TimeProvider.System,
            eventSink,
            usageStore);

    public static WorkflowRuntimeManager Create(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink,
        IWorkflowUsageObservationStore? usageStore)
        => Create(
            backends,
            store,
            new InMemoryWorkflowExternalRequestBoundaryStore(store),
            activeRuns,
            timeProvider,
            eventSink,
            usageStore);

    public static WorkflowRuntimeManager Create(
        IEnumerable<IWorkflowExecutionBackend> backends,
        InMemoryWorkflowRunStore store,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink,
        InMemoryWorkflowUsageObservationStore? usageStore)
        => Create(
            backends,
            store,
            new InMemoryWorkflowExternalRequestBoundaryStore(store, checkpointPayloadStore),
            activeRuns,
            timeProvider,
            eventSink,
            usageStore);

    public static WorkflowRuntimeManager CreateCompatibility(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink,
        IWorkflowUsageObservationStore? usageStore)
        => Create(
            backends,
            store,
            new InMemoryWorkflowExternalRequestBoundaryStore(
                store,
                checkpointPayloadStore,
                allowCompatibilityComposition: true),
            activeRuns,
            timeProvider,
            eventSink,
            usageStore);

    private static WorkflowRuntimeManager Create(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        InMemoryWorkflowExternalRequestBoundaryStore requestBoundaries,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink,
        IWorkflowUsageObservationStore? usageStore)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(requestBoundaries);
        ArgumentNullException.ThrowIfNull(activeRuns);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var backendList = backends.ToArray();
        var operations = new InMemoryWorkflowExternalResponseOperationStore(
            store,
            requestBoundaries);
        var resumeBoundaries = requestBoundaries.SupportsNativeCheckpointLink &&
            store is InMemoryWorkflowRunStore inMemoryRunStore &&
            usageStore is null or InMemoryWorkflowUsageObservationStore
                ? new InMemoryWorkflowResumeBoundaryStore(
                    inMemoryRunStore,
                    requestBoundaries,
                    operations,
                    usageStore as InMemoryWorkflowUsageObservationStore)
                : new InMemoryWorkflowResumeBoundaryStore(
                    store,
                    requestBoundaries,
                    operations,
                    usageStore);
        var continuation = new WorkflowExternalResponseContinuation(
            backendList,
            operations,
            resumeBoundaries,
            activeRuns,
            new WorkflowExternalResponseValidator(),
            eventSink ?? new NullWorkflowEventSink(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowExternalResponseContinuation>.Instance,
            timeProvider);
        return new WorkflowRuntimeManager(
            backendList,
            store,
            activeRuns,
            timeProvider,
            requestBoundaries,
            resumeBoundaries,
            eventSink,
            usageStore);
    }
}
