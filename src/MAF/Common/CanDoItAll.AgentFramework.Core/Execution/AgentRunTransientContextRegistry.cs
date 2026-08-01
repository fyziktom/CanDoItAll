using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentRunTransientContextRegistry
{
    private const int MaximumEntries = 64;
    private readonly object gate = new();
    private readonly Dictionary<Guid, AgentRuntimeTransientContext> contexts = [];

    public void Register(
        ExecutionRunRecord run,
        AgentRuntimeTransientContext context)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);

        var expectedDigest = ExecutionInvocationMetadata.ResolveTransientContextDigest(run);
        if (string.IsNullOrWhiteSpace(expectedDigest))
        {
            throw new InvalidOperationException(
                $"Execution run '{run.Id:N}' does not declare a transient context digest.");
        }

        var actualDigest = AgentChatContextDigest.Compute(context);
        if (!string.Equals(expectedDigest, actualDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Execution run '{run.Id:N}' transient context does not match its captured context digest.");
        }

        lock (gate)
        {
            if (contexts.TryGetValue(run.Id, out var existing))
            {
                if (!ReferenceEquals(existing, context))
                {
                    throw new InvalidOperationException(
                        $"Execution run '{run.Id:N}' already has a different transient context lease.");
                }

                return;
            }

            if (contexts.Count >= MaximumEntries)
            {
                throw new InvalidOperationException(
                    $"No more than {MaximumEntries} execution runs can retain approval context in one workspace service.");
            }

            contexts.Add(run.Id, context);
        }
    }

    public AgentRuntimeTransientContext? Resolve(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!ExecutionInvocationMetadata.RequiresTransientContext(run))
        {
            return null;
        }

        lock (gate)
        {
            if (contexts.TryGetValue(run.Id, out var context))
            {
                return context;
            }
        }

        throw new AgentRunTransientContextUnavailableException(run.Id);
    }

    public void Remove(Guid executionRunId)
    {
        if (executionRunId == Guid.Empty)
        {
            return;
        }

        lock (gate)
        {
            contexts.Remove(executionRunId);
        }
    }
}

internal sealed class AgentRunTransientContextUnavailableException(Guid executionRunId)
    : InvalidOperationException(
        $"Execution run '{executionRunId:N}' requires its original application context to continue, but that bounded context lease is no longer available. Start a new message from the current application surface instead of continuing with potentially stale context.")
{
}
