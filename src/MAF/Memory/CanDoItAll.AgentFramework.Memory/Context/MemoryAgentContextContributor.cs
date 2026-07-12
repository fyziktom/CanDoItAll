using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Memory.Context;
using CanDoItAll.AgentFramework.Memory.Routing;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.AgentFramework.Memory.Context;

public sealed class MemoryAgentContextContributor : IAgentContextContributor
{
    public const string ContributorIdValue = "memory.context";

    private const int ContributorOrder = 50;

    private readonly MemoryAgentContextFanOut fanOut;

    public MemoryAgentContextContributor(
        IMemoryOperationHandler operationHandler,
        TimeProvider timeProvider,
        ILogger<MemoryAgentContextContributor>? logger = null)
    {
        var dispatcher = new MemoryAgentContextQueryDispatcher(operationHandler, timeProvider);
        fanOut = new MemoryAgentContextFanOut(
            dispatcher,
            logger ?? NullLogger<MemoryAgentContextContributor>.Instance);
    }

    public AgentContextContributorDescriptor Descriptor { get; } = new(
        new AgentContextContributorId(ContributorIdValue),
        "Memory context",
        ContributorOrder);

    public async ValueTask<AgentContextContributionResult> ContributeAsync(
        AgentContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var access = AgentMemoryAccessMetadata.Read(request.Agent.ConfigurationJson);
        var prompt = request.RequestMessages
            .LastOrDefault(message => message.Role == AgentContextMessageRole.User)
            ?.Text;
        var plan = AgentMemoryInvocationPlanner.Plan(access, prompt);
        var capability = ResolveCapability(access);
        if (plan.Decision == AgentMemoryInvocationPlanDecision.Skip)
        {
            return Skip(access, plan, capability);
        }

        if (plan.Decision == AgentMemoryInvocationPlanDecision.Reject)
        {
            return Reject(access, plan, capability);
        }

        var outcomes = await fanOut.QueryAsync(
            request,
            access,
            plan,
            capability,
            cancellationToken).ConfigureAwait(false);
        var result = MemoryAgentContextResultMerger.Merge(access, capability, outcomes);
        return plan.TransformRequestMessages
            ? result.WithRequestMessageTextReplacement(
                FindLastUserPromptIndex(request.RequestMessages),
                plan.Query)
            : result;
    }

    private static AgentContextContributionResult Skip(
        AgentMemoryAccessSettings access,
        AgentMemoryInvocationPlan plan,
        MemoryCapabilityId capability)
    {
        var reason = access.InvocationMode == AgentMemoryInvocationMode.Disabled
            ? MemoryAgentContextContributionTraceReasons.Disabled
            : MemoryAgentContextContributionTraceReasons.DirectiveRequired;
        return AgentContextContributionResult.Skipped(MemoryAgentContextResultMerger.CreateTrace(
            reason,
            MemoryToolResultStatus.ToolDisabled,
            capability,
            diagnostic: plan.Diagnostic));
    }

    private static AgentContextContributionResult Reject(
        AgentMemoryAccessSettings access,
        AgentMemoryInvocationPlan plan,
        MemoryCapabilityId capability)
    {
        var noProvider = plan.Diagnostic.StartsWith("No memory provider", StringComparison.Ordinal);
        var reason = noProvider
            ? MemoryAgentContextContributionTraceReasons.NoProviderConfigured
            : MemoryAgentContextContributionTraceReasons.InvalidDirective;
        var status = noProvider
            ? MemoryToolResultStatus.NoProviderConfigured
            : MemoryToolResultStatus.InvalidRequest;
        var trace = MemoryAgentContextResultMerger.CreateTrace(
            reason,
            status,
            capability,
            diagnostic: plan.Diagnostic);
        return noProvider && !access.RequireContextContributions
            ? AgentContextContributionResult.Skipped(trace)
            : AgentContextContributionResult.Failed(plan.Diagnostic, trace);
    }

    private static MemoryCapabilityId ResolveCapability(AgentMemoryAccessSettings access)
    {
        _ = access;
        return MemoryCapabilityIds.ContextQuerySync;
    }

    private static int FindLastUserPromptIndex(
        IReadOnlyList<AgentContextRequestMessage> requestMessages)
    {
        for (var index = requestMessages.Count - 1; index >= 0; index--)
        {
            if (requestMessages[index].Role != AgentContextMessageRole.User)
            {
                continue;
            }

            return index;
        }

        throw new AgentMemoryConfigurationException(
            "A memory directive cannot be sanitized because the request contains no user message.");
    }
}
