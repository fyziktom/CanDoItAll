using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory;

internal sealed class MemoryWorkflowStatusOperation(
    IMemoryOperationHandler operationHandler,
    MemoryWorkflowRequestFactory requests)
{
    public async Task<object> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.OperationId == Guid.Empty)
        {
            return MemoryMafToolResultShaper.RejectedStatus(
                MemoryToolResultStatus.InvalidRequest,
                "Memory workflow operation status requires a valid operation id.");
        }

        var policy = requests.ResolvePolicy(
            context,
            settings,
            MemoryCapabilityIds.OperationStatus,
            requestedProviderId: null,
            providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedStatus(rejection.Status, rejection.Diagnostic);
        }

        var request = MemoryOperationRequestBuilder.Status(
            MemoryWorkflowRequestFactory.CreateWorkflowCaller(context),
            policy.SelectionPolicy,
            new MemoryOperationStatusRequest(new MemoryOperationId(settings.OperationId)),
            requests.CreateRetention());
        var result = await operationHandler.GetStatusAsync(request, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToStatusResult(result);
    }
}
