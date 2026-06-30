using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowRuntimeBackendCatalog : IWorkflowRuntimeBackendCatalog
{
    private static readonly WorkflowRuntimeBackendDescriptor[] BackendDefinitions =
    [
        new(
            WorkflowRuntimeBackendKind.InProcess,
            "MAF in-process workflow runtime",
            IsDurable: false,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: false,
            OperationalNotes: "Use for local development, tests, previews, and approved short non-durable runs only."),
        new(
            WorkflowRuntimeBackendKind.DurableTask,
            "MAF DurableTask workflow runtime",
            IsDurable: true,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: true,
            OperationalNotes: "Preferred for production, long-running, distributed, or restart-resilient workflows."),
        new(
            WorkflowRuntimeBackendKind.AzureFunctions,
            "MAF Azure Functions durable workflow hosting",
            IsDurable: true,
            SupportsStreaming: false,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: true,
            OperationalNotes: "Evaluate for generated HTTP, status/respond, and MCP tool triggers behind product authorization.")
    ];

    private readonly IReadOnlyList<WorkflowRuntimeBackendDescriptor> backends;

    public WorkflowRuntimeBackendCatalog()
        : this([WorkflowRuntimeBackendKind.InProcess])
    {
    }

    public WorkflowRuntimeBackendCatalog(IEnumerable<WorkflowRuntimeBackendKind> registeredBackends)
    {
        ArgumentNullException.ThrowIfNull(registeredBackends);

        var registeredBackendSet = registeredBackends.ToHashSet();
        backends = BackendDefinitions
            .Select(descriptor => registeredBackendSet.Contains(descriptor.Kind)
                ? MarkRegistered(descriptor)
                : MarkPlanned(descriptor))
            .ToArray();
    }

    public IReadOnlyList<WorkflowRuntimeBackendDescriptor> ListBackends() => backends;

    public WorkflowRuntimeBackendDescriptor GetRequiredBackend(WorkflowRuntimeBackendKind backend)
    {
        foreach (var descriptor in backends)
        {
            if (descriptor.Kind == backend)
            {
                return descriptor;
            }
        }

        throw new InvalidOperationException($"Workflow runtime backend '{backend}' is not recognized by this host.");
    }

    private static WorkflowRuntimeBackendDescriptor MarkRegistered(WorkflowRuntimeBackendDescriptor descriptor)
        => descriptor with
        {
            Availability = WorkflowRuntimeBackendAvailabilityKind.Registered,
            IsRegistered = true,
            IsRunnable = true,
            AvailabilityReason = "Runtime backend is registered and runnable in this host."
        };

    private static WorkflowRuntimeBackendDescriptor MarkPlanned(WorkflowRuntimeBackendDescriptor descriptor)
        => descriptor with
        {
            Availability = WorkflowRuntimeBackendAvailabilityKind.Planned,
            IsRegistered = false,
            IsRunnable = false,
            AvailabilityReason = $"Runtime backend '{descriptor.Kind}' is planned but not registered in this host."
        };
}
