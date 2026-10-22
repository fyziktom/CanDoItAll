using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentExecutionCancellationRegistry {
    bool RequestCancellation(Guid executionRunId);

    IAgentExecutionCancellationRegistration Register(
        ExecutionRunRecord run,
        CancellationToken cancellationToken);

    int RequestCancellationByProcessRunIds(
        IReadOnlyCollection<string> processRunIds,
        string requestedBy,
        string reason);
}

public interface IAgentExecutionCancellationRegistration : IDisposable, IAsyncDisposable {
    CancellationToken Token { get; }

    bool IsCancellationRequested { get; }
}

public sealed class AgentExecutionCancellationRegistry : IAgentExecutionCancellationRegistry {
    private readonly ConcurrentDictionary<Guid, AgentExecutionCancellationRegistration> activeExecutions = new();
    private readonly ILogger logger;

    public AgentExecutionCancellationRegistry(ILogger<AgentExecutionCancellationRegistry> logger) : this((ILogger)logger) { }

    internal AgentExecutionCancellationRegistry(ILogger logger) {
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;
    }

    public bool RequestCancellation(Guid executionRunId) {
        if (executionRunId == Guid.Empty) {
            throw new ArgumentException("Cancellation requires an execution identity.", nameof(executionRunId));
        }
        return activeExecutions.TryGetValue(executionRunId, out var active) && active.RequestCancellation(onlyNew: false);
    }

    public IAgentExecutionCancellationRegistration Register(
        ExecutionRunRecord run,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Id == Guid.Empty) {
            throw new ArgumentException("Cancellation registration requires an execution identity.", nameof(run));
        }
        var activeExecution = new AgentExecutionCancellationRegistration(run.Id, NormalizeRunId(run.ProcessRunId), Unregister, logger);
        if (!activeExecutions.TryAdd(run.Id, activeExecution)) {
            activeExecution.Dispose();
            throw new InvalidOperationException($"Execution run '{run.Id:N}' is already registered for cancellation.");
        }
        try {
            activeExecution.Link(cancellationToken);
            return activeExecution;
        } catch {
            activeExecution.Dispose();
            throw;
        }
    }

    public int RequestCancellationByProcessRunIds(
        IReadOnlyCollection<string> processRunIds,
        string requestedBy,
        string reason) {
        ArgumentNullException.ThrowIfNull(processRunIds);

        var runIdSet = processRunIds
            .Select(NormalizeRunId)
            .Where(runId => runId.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (runIdSet.Count == 0) {
            return 0;
        }

        var cancelledCount = 0;
        foreach (var activeExecution in activeExecutions.Values) {
            if (runIdSet.Contains(activeExecution.ProcessRunId) && activeExecution.RequestCancellation(onlyNew: true)) {
                cancelledCount++;
            }
        }

        return cancelledCount;
    }

    private void Unregister(AgentExecutionCancellationRegistration activeExecution)
        => ((ICollection<KeyValuePair<Guid, AgentExecutionCancellationRegistration>>)activeExecutions)
            .Remove(new(activeExecution.ExecutionRunId, activeExecution));

    private static string NormalizeRunId(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

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
