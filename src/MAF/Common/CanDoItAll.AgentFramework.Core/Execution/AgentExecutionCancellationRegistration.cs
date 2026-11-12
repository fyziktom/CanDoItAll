using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentExecutionCancellationRegistration : IAgentExecutionCancellationRegistration {
    private readonly object gate = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Action<AgentExecutionCancellationRegistration> release;
    private readonly ILogger logger;
    private CancellationTokenRegistration upstream;
    private Task notification = Task.CompletedTask;
    private bool notifying;
    private bool disposed;
    private bool released;

    internal AgentExecutionCancellationRegistration(Guid executionRunId, string processRunId, AgentToolSessionAdmission? session,
        Action<AgentExecutionCancellationRegistration> release, ILogger logger) {
        ExecutionRunId = executionRunId;
        ProcessRunId = processRunId;
        Session = session;
        this.release = release;
        this.logger = logger;
        Token = cancellation.Token;
    }

    internal Guid ExecutionRunId { get; }
    internal string ProcessRunId { get; }
    internal AgentToolSessionAdmission? Session { get; }
    public CancellationToken Token { get; }
    public bool IsCancellationRequested => Token.IsCancellationRequested;

    internal void Link(CancellationToken token) {
        lock (gate) {
            ObjectDisposedException.ThrowIf(disposed, this);
            upstream = token.UnsafeRegister(static state =>
                ((AgentExecutionCancellationRegistration)state!).RequestCancellation(onlyNew: false), this);
        }
    }

    internal bool RequestCancellation(bool onlyNew) {
        lock (gate) {
            if (disposed) {
                return false;
            }
            if (Token.IsCancellationRequested) {
                return !onlyNew;
            }
            notifying = true;
            notification = ObserveAsync(cancellation.CancelAsync());
            return true;
        }
    }

    public void Dispose() {
        lock (gate) {
            if (disposed) {
                return;
            }
            disposed = true;
            upstream.Unregister();
            ReleaseIfReady();
        }
    }

    public ValueTask DisposeAsync() {
        Dispose();
        lock (gate) {
            return new(notification);
        }
    }

    private async Task ObserveAsync(Task callbacks) {
        try {
            await callbacks.ConfigureAwait(false);
        } catch (Exception exception) {
            IReadOnlyList<Exception> failures = exception is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : [exception];
            logger.LogError("Cancellation callbacks failed for execution {ExecutionRunId}: {FailureCount} failures, first type {FailureType}. Cancellation remains requested.",
                ExecutionRunId, failures.Count, failures[0].GetType().Name);
        } finally {
            lock (gate) {
                notifying = false;
                ReleaseIfReady();
            }
        }
    }

    private void ReleaseIfReady() {
        if (!disposed || notifying || released) {
            return;
        }
        released = true;
        cancellation.Dispose();
        release(this);
    }
}
