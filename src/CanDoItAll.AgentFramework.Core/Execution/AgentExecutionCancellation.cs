using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentExecutionCancellationRegistry
{
    IAgentExecutionCancellationRegistration Register(
        ExecutionRunRecord run,
        CancellationToken cancellationToken);

    int RequestCancellationByProcessRunIds(
        IReadOnlyCollection<string> processRunIds,
        string requestedBy,
        string reason);
}

public interface IAgentExecutionCancellationRegistration : IDisposable
{
    CancellationToken Token { get; }

    bool IsCancellationRequested { get; }
}

public sealed class AgentExecutionCancellationRegistry : IAgentExecutionCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, ActiveExecution> activeExecutions = new();

    public IAgentExecutionCancellationRegistration Register(
        ExecutionRunRecord run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var activeExecution = new ActiveExecution(run.Id, NormalizeRunId(run.ProcessRunId), cancellation);
        if (!activeExecutions.TryAdd(run.Id, activeExecution))
        {
            cancellation.Dispose();
            throw new InvalidOperationException($"Execution run '{run.Id:N}' is already registered for cancellation.");
        }

        return new Registration(this, activeExecution);
    }

    public int RequestCancellationByProcessRunIds(
        IReadOnlyCollection<string> processRunIds,
        string requestedBy,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(processRunIds);

        var runIdSet = processRunIds
            .Select(NormalizeRunId)
            .Where(runId => runId.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (runIdSet.Count == 0)
        {
            return 0;
        }

        var cancelledCount = 0;
        foreach (var activeExecution in activeExecutions.Values)
        {
            if (!runIdSet.Contains(activeExecution.ProcessRunId) ||
                activeExecution.Cancellation.IsCancellationRequested)
            {
                continue;
            }

            try
            {
                activeExecution.Cancellation.Cancel();
                cancelledCount++;
            }
            catch (ObjectDisposedException)
            {
            }
        }

        return cancelledCount;
    }

    private void Unregister(ActiveExecution activeExecution)
    {
        activeExecutions.TryRemove(activeExecution.ExecutionRunId, out _);
        activeExecution.Cancellation.Dispose();
    }

    private static string NormalizeRunId(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private sealed record ActiveExecution(
        Guid ExecutionRunId,
        string ProcessRunId,
        CancellationTokenSource Cancellation);

    private sealed class Registration(
        AgentExecutionCancellationRegistry owner,
        ActiveExecution activeExecution) : IAgentExecutionCancellationRegistration
    {
        private bool disposed;

        public CancellationToken Token => activeExecution.Cancellation.Token;

        public bool IsCancellationRequested => activeExecution.Cancellation.IsCancellationRequested;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            owner.Unregister(activeExecution);
        }
    }
}

public sealed class AgentExecutionCancelledException : Exception
{
    public AgentExecutionCancelledException(
        Guid executionRunId,
        string processRunId,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ExecutionRunId = executionRunId;
        ProcessRunId = processRunId;
    }

    public Guid ExecutionRunId { get; }

    public string ProcessRunId { get; }
}
