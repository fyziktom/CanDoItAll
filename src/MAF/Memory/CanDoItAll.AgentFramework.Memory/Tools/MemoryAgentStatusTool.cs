using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory.Tools;

internal sealed class MemoryAgentStatusTool(
    IMemoryOperationHandler operationHandler,
    TimeProvider timeProvider)
{
    public async Task<MemoryOperationStatusToolResult> GetStatusAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryOperationStatusToolInput input,
        CancellationToken cancellationToken)
    {
        if (input.OperationId == Guid.Empty)
        {
            return MemoryMafToolResultShaper.RejectedStatus(
                MemoryToolResultStatus.InvalidRequest,
                "Memory operation status requires a valid operation id.");
        }

        var policy = MemoryAgentToolPolicyFactory.Resolve(
            context,
            access,
            MemoryCapabilityIds.OperationStatus,
            requestedProviderAliasOrId: null,
            providerRequired: false);
        if (policy.Resolution.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedStatus(rejection.Status, rejection.Diagnostic);
        }

        var request = MemoryOperationRequestBuilder.Status(
            MemoryAgentToolPolicyFactory.CreateCaller(policy, MemoryAgentRuntimeToolNames.OperationStatus),
            policy.Resolution.SelectionPolicy,
            new MemoryOperationStatusRequest(new MemoryOperationId(input.OperationId)),
            MemoryMafRetentionPolicyFactory.Create(timeProvider));
        var result = await operationHandler.GetStatusAsync(request, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToStatusResult(result);
    }
}
