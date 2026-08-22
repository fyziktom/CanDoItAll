using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal interface IWorkflowRedactedExternalResponseAcceptanceStore
{
    Task<WorkflowExternalResponseAcceptanceResult> TryAcceptRedactedExternalResponseAsync(
        WorkflowExternalRequestId requestId,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default);
}
