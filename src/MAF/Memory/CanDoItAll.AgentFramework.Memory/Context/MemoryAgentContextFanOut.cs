using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Memory.Routing;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Memory.Context;

internal sealed class MemoryAgentContextFanOut(
    MemoryAgentContextQueryDispatcher dispatcher,
    ILogger logger,
    int maximumConcurrency = 4)
{
    public async Task<IReadOnlyList<MemoryAgentContextQueryOutcome>> QueryAsync(
        AgentContextContributionRequest request,
        AgentMemoryAccessSettings access,
        AgentMemoryInvocationPlan plan,
        MemoryCapabilityId capability,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrency, 1);
        var outcomes = new MemoryAgentContextQueryOutcome[plan.Providers.Count];
        using var gate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        var tasks = plan.Providers.Select((binding, index) => QueryAtIndexAsync(
            index,
            binding,
            outcomes,
            gate,
            request,
            access,
            plan.Query,
            capability,
            cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return outcomes;
    }

    private async Task QueryAtIndexAsync(
        int index,
        AgentMemoryProviderBindingSetting binding,
        MemoryAgentContextQueryOutcome[] outcomes,
        SemaphoreSlim gate,
        AgentContextContributionRequest request,
        AgentMemoryAccessSettings access,
        string query,
        MemoryCapabilityId capability,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            outcomes[index] = await dispatcher.QueryProviderAsync(
                request,
                access,
                query,
                capability,
                binding,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Memory context query failed for alias {MemoryAlias}, provider {MemoryProviderId}, exception type {ExceptionType}.",
                binding.Alias.Value,
                binding.ProviderInstanceId.Value,
                exception.GetType().Name);
            outcomes[index] = MemoryAgentContextQueryOutcome.UnexpectedFailure(binding, exception);
        }
        finally
        {
            gate.Release();
        }
    }
}
