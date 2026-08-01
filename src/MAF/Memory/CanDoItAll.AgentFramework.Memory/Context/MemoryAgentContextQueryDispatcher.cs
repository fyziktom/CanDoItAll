using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory.Context;

internal sealed class MemoryAgentContextQueryDispatcher(
    IMemoryOperationHandler operationHandler,
    TimeProvider timeProvider)
{
    public async Task<MemoryAgentContextQueryOutcome> QueryProviderAsync(
        AgentContextContributionRequest request,
        AgentMemoryAccessSettings access,
        string query,
        MemoryCapabilityId capability,
        AgentMemoryProviderBindingSetting binding,
        CancellationToken cancellationToken)
    {
        var effectiveAllowedProviders = access.AllowedProviderInstanceIds.Count > 0
            ? access.AllowedProviderInstanceIds
            : access.ProviderBindings.Select(item => item.ProviderInstanceId).ToArray();
        var policy = MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
            capability,
            binding.ProviderInstanceId,
            PreferredProviderInstanceId: null,
            DefaultProviderInstanceId: null,
            effectiveAllowedProviders,
            access.AllowedCapabilityIds,
            access.DeniedCapabilityIds,
            access.ProviderAssignments.Select(MemoryMafProviderPolicyResolver.ToProviderAssignment).ToArray(),
            MatchedAssignmentProvider: null,
            ProviderRequired: true,
            "the agent's allowed context contribution policy",
            "the agent's allowed context contribution provider policy",
            "Memory context contribution requires an explicitly bound provider."));
        if (policy.Rejection is { } rejection)
        {
            return MemoryAgentContextQueryOutcome.Rejected(binding, rejection.Status, rejection.Diagnostic);
        }

        var payload = new MemoryContextQueryRequest(
            query,
            [capability],
            MemorySourceProvenance.None)
        {
            Context = MemoryAgentRuntimeContextFactory.CreateRequestContext(
                request.Policy.WorkspaceScope,
                request.ContextIntent,
                access)
        };
        var requester = MemoryAgentRuntimeContextFactory.CreateRequester(
            request.Agent,
            request.ContextIntent);
        var handlerRequest = MemoryOperationRequestBuilder.Query(
            MemoryOperationCaller.ContextContributor(MemoryAgentContextContributor.ContributorIdValue, requester),
            policy.SelectionPolicy,
            payload,
            MemoryMafRetentionPolicyFactory.Create(timeProvider));
        var result = await operationHandler.ExecuteQueryAsync(handlerRequest, cancellationToken).ConfigureAwait(false);
        return MemoryAgentContextQueryOutcome.FromResult(binding, result);
    }
}

internal sealed record MemoryAgentContextQueryOutcome(
    AgentMemoryProviderBindingSetting Binding,
    MemoryToolResultStatus Status,
    MemoryContextPack? ContextPack,
    MemoryOperationAccepted? AcceptedOperation,
    string Diagnostic,
    bool DispatchAttempted)
{
    public static MemoryAgentContextQueryOutcome Rejected(
        AgentMemoryProviderBindingSetting binding,
        MemoryToolResultStatus status,
        string diagnostic) =>
        new(binding, status, ContextPack: null, AcceptedOperation: null, diagnostic, DispatchAttempted: false);

    public static MemoryAgentContextQueryOutcome FromResult(
        AgentMemoryProviderBindingSetting binding,
        MemoryOperationHandlerResult<MemoryContextPack> result) =>
        new(
            binding,
            MemoryToolResultMapper.ToToolStatus(result.Status),
            result.Output,
            result.AcceptedOperation,
            result.Diagnostic,
            result.DriverDispatchAttempted);

    public static MemoryAgentContextQueryOutcome UnexpectedFailure(
        AgentMemoryProviderBindingSetting binding,
        Exception exception) =>
        new(
            binding,
            MemoryToolResultStatus.Failed,
            ContextPack: null,
            AcceptedOperation: null,
            $"Provider query raised an unexpected {exception.GetType().Name}.",
            DispatchAttempted: true);
}
