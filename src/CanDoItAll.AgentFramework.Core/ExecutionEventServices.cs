using CanDoItAll.AgentFramework.Models;
using System.Collections.Concurrent;

namespace CanDoItAll.AgentFramework.Core;

public sealed class NullAgentExecutionEventSink : IAgentExecutionEventSink
{
    public Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class BufferedAgentExecutionEventSink : IAgentExecutionEventSink
{
    private readonly ConcurrentQueue<ExecutionEvent> buffer = new();

    public int Capacity { get; init; } = 256;

    public Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionEvent);

        buffer.Enqueue(executionEvent);
        while (buffer.Count > Math.Max(32, Capacity) && buffer.TryDequeue(out _))
        {
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<ExecutionEvent> Snapshot(int take = 64)
    {
        return buffer
            .Reverse()
            .Take(Math.Max(1, take))
            .ToList();
    }
}
