using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatBackgroundExecution(
    IAgentChatExecutionScopeFactory scopes,
    TimeProvider clock,
    ILogger<AgentChatBackgroundExecution> logger,
    IDatabaseSwitchNotificationService? profileNotifications = null) : IAgentChatExecutionCatalog, IHostedService, IAsyncDisposable {
    public const int MaximumRetainedConversations = 256;
    public const int MaximumOwnerConversations = 48;
    private static readonly TimeSpan RecentRetention = TimeSpan.FromDays(1);
    private readonly Lock gate = new();
    private readonly List<AgentChatExecutionSession> sessions = [];
    private readonly List<Task> retirements = [];
    private bool stopping;
    private bool subscribed;
    private Guid? currentProfileId;
    private long currentProfileGeneration;
    private Task? stopTask;
    private long revision;

    public event EventHandler? Changed;

    public IReadOnlyList<AgentChatExecutionSnapshot> Snapshot(AgentChatOwnerId owner, Guid profileId, DatabaseProfileGeneration generation) {
        RequireOwner(owner);
        lock (gate) {
            return sessions.Where(entry => !entry.Retired && entry.Owner == owner && entry.Snapshot.StreamId.DatabaseProfileId == profileId &&
                    entry.Snapshot.StreamId.DatabaseProfileGeneration == generation)
                .Select(entry => entry.Snapshot).OrderByDescending(entry => entry.UpdatedAtUtc).ToArray();
        }
    }

    public bool TryDismiss(AgentChatOwnerId owner, Guid profileId, DatabaseProfileGeneration generation, Guid agentId, Guid chatSessionId) {
        RequireOwner(owner);
        lock (gate) {
            var entry = sessions.FirstOrDefault(item => item.Owner == owner && item.Snapshot.AgentId == agentId &&
                item.Snapshot.ChatSessionId == chatSessionId && item.Snapshot.StreamId.DatabaseProfileId == profileId &&
                item.Snapshot.StreamId.DatabaseProfileGeneration == generation);
            if (entry is null) {
                return true;
            }
            if (entry.Busy || entry.Snapshot.RunState != ActiveAgentChatRunState.Idle || !entry.ScopeRelease.IsCompletedSuccessfully) {
                return false;
            }
            sessions.Remove(entry);
        }
        RaiseChanged();
        return true;
    }

    public Task<AgentChatRunResult> SendAsync(ValueTask<AgentChatOwnerId> owner, IAgentExecutionActivityOperationLease operation,
        AgentChatSendRequest request, AgentTurnContextObservation observation)
        => ExecuteAsync(owner, operation, request.ConversationHandleId, AgentChatReturnLocation.FromContext(observation.Context),
            observation.ConversationBinding, (executor, activity, token) => executor.SendAsync(activity, request, observation, token), requiresIdle: true);

    public Task<AgentChatRunResult> ApproveAsync(ValueTask<AgentChatOwnerId> owner, IAgentExecutionActivityOperationLease operation,
        IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprove)
        => ExecuteAsync(owner, operation, null, null, null, (executor, activity, token) =>
            executor.ApproveAsync(activity, operation.AgentId!.Value, operation.ChatSessionId!.Value, decisions, autoApprove, token));

    public Task<ExecutionRunResult> RecoverAsync(ValueTask<AgentChatOwnerId> owner, IAgentExecutionActivityOperationLease operation, Guid runId)
        => ExecuteAsync(owner, operation, null, null, null, (executor, activity, token) => executor.RecoverAsync(activity, runId, token));

    public Task<ExecutionRunDetail> PlanAsync(ValueTask<AgentChatOwnerId> owner, IAgentExecutionActivityOperationLease operation,
        AgentPlanningActionRequest request)
        => ExecuteAsync(owner, operation, null, null, null, (executor, activity, token) => executor.PlanAsync(activity, request, token));

    public Task<ExecutionRunDetail> CancelPendingAsync(ValueTask<AgentChatOwnerId> owner, IAgentExecutionActivityOperationLease operation, Guid runId)
        => ExecuteAsync(owner, operation, null, null, null, (executor, activity, token) => executor.CancelPendingAsync(activity, runId, token));

    public bool RequestCancellation(AgentChatOwnerId owner, Guid profileId, DatabaseProfileGeneration generation,
        Guid agentId, Guid sessionId) {
        RequireOwner(owner);
        lock (gate) {
            var entry = sessions.FirstOrDefault(entry => entry.Owner == owner && entry.Snapshot.AgentId == agentId &&
                entry.Snapshot.ChatSessionId == sessionId && entry.Snapshot.StreamId.DatabaseProfileId == profileId &&
                entry.Snapshot.StreamId.DatabaseProfileGeneration == generation && entry.Busy);
            if (entry?.Cancellation is not { } cancellation) {
                return false;
            }
            entry.CancellationNotification = ObserveCancellationAsync(cancellation.CancelAsync());
        }
        return true;
    }

    private Task<TResult> ExecuteAsync<TResult>(ValueTask<AgentChatOwnerId> ownerResolution,
        IAgentExecutionActivityOperationLease operation, AgentChatHandleId? handle, AgentChatReturnLocation? location,
        AgentConversationContextBinding? binding,
        Func<IAgentChatOperationExecutor, IAgentExecutionActivityOperationLease, CancellationToken, Task<TResult>> execute,
        bool requiresIdle = false) {
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = completion.Task.ContinueWith(static task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _ = QueueAfterOwnerAsync(ownerResolution, operation, handle, location, binding, execute, requiresIdle, completion);
        return completion.Task;
    }

    private async Task QueueAfterOwnerAsync<TResult>(ValueTask<AgentChatOwnerId> ownerResolution,
        IAgentExecutionActivityOperationLease operation, AgentChatHandleId? handle, AgentChatReturnLocation? location,
        AgentConversationContextBinding? binding,
        Func<IAgentChatOperationExecutor, IAgentExecutionActivityOperationLease, CancellationToken, Task<TResult>> execute,
        bool requiresIdle, TaskCompletionSource<TResult> completion) {
        try {
            var owner = await ownerResolution.ConfigureAwait(false);
            ownerResolution = default;
            RequireOwner(owner);
            using (ExecutionContext.SuppressFlow()) {
                _ = Task.Run(async () => {
                    try {
                        completion.TrySetResult(await ExecuteOwnedAsync(owner, operation, handle, location, binding, execute, requiresIdle)
                            .ConfigureAwait(false));
                    } catch (Exception exception) {
                        logger.LogWarning("Owned agent operation failed. Stream={StreamId} FailureType={FailureType}.",
                            operation.StreamId, exception.GetType().Name);
                        completion.TrySetException(exception);
                    }
                });
            }
        } catch (Exception exception) {
            logger.LogWarning("Agent conversation owner admission failed. Stream={StreamId} FailureType={FailureType}.",
                operation.StreamId, exception.GetType().Name);
            try {
                if (!operation.IsTerminal) {
                    operation.Fail("The conversation owner could not be verified.", AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
                }
                operation.Dispose();
            } catch (Exception cleanup) {
                exception = new AggregateException("Conversation admission and activity cleanup failed.", exception, cleanup);
            }
            completion.TrySetException(exception);
        } finally {
            ownerResolution = default;
        }
    }

    private async Task<TResult> ExecuteOwnedAsync<TResult>(AgentChatOwnerId owner,
        IAgentExecutionActivityOperationLease operation, AgentChatHandleId? handle, AgentChatReturnLocation? location,
        AgentConversationContextBinding? binding,
        Func<IAgentChatOperationExecutor, IAgentExecutionActivityOperationLease, CancellationToken, Task<TResult>> execute,
        bool requiresIdle) {
        AgentChatExecutionSession? entry = null;
        Exception? failure = null;
        try {
            lock (gate) {
                ObjectDisposedException.ThrowIf(stopping, this);
                entry = Admit(owner, operation, handle, location, binding, requiresIdle);
            }
            RaiseChanged();
            entry.Cancellation!.Token.ThrowIfCancellationRequested();
            entry.Scope ??= await scopes.CreateAsync().ConfigureAwait(false);
            var activity = new AgentChatCatalogActivityLease(operation, (state, sessionId, runId) =>
                Update(entry, state, sessionId, runId));
            var result = await execute(entry.Scope.Executor, activity, entry.Cancellation.Token).ConfigureAwait(false);
            if (!operation.IsTerminal) {
                throw new InvalidOperationException("The owned agent operation completed without terminal activity.");
            }
            var runState = result switch {
                AgentChatRunResult { EffectContinuation: not null } => ActiveAgentChatRunState.AwaitingContinuation,
                ExecutionRunResult { EffectContinuation: not null } => ActiveAgentChatRunState.AwaitingContinuation,
                AgentChatRunResult { State: ExecutionState.WaitingOnTool } => ActiveAgentChatRunState.AwaitingApproval,
                ExecutionRunResult { State: ExecutionState.WaitingOnTool } => ActiveAgentChatRunState.AwaitingApproval,
                ExecutionRunDetail { Run.PendingApprovals.Count: > 0 } => ActiveAgentChatRunState.AwaitingApproval,
                _ => ActiveAgentChatRunState.Idle
            };
            lock (gate) {
                entry.Snapshot = entry.Snapshot with {
                    Binding = entry.Scope.Executor.LastCapture?.ConversationBinding ?? entry.Snapshot.Binding
                };
                entry.SettledNotification = result switch {
                    AgentChatRunResult chat => chat.ContextCompletionNotification,
                    ExecutionRunResult run => run.ContextCompletionNotification,
                    ExecutionRunDetail detail => AgentChatContextInvocationFactory.CreateCompletionNotification(detail.Run),
                    _ => null
                };
            }
            Update(entry, runState, operation.ChatSessionId, operation.ExecutionRunId);
            return result;
        } catch (Exception exception) {
            failure = exception;
            if (!operation.IsTerminal) {
                if (exception is OperationCanceledException) {
                    operation.Cancel("The owned agent operation was cancelled.");
                } else {
                    operation.Fail("The owned agent operation failed.", AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
                }
            }
            if (entry is not null) {
                Update(entry, AgentEffectActivityFinalization.IsEffectFailure(exception)
                    ? ActiveAgentChatRunState.AwaitingContinuation
                    : exception is AgentChatRunFailedException or AgentRunFailedException
                        ? ActiveAgentChatRunState.Idle : entry.PreviousRunState,
                    operation.ChatSessionId, operation.ExecutionRunId);
            }
            throw;
        } finally {
            try {
                operation.Dispose();
            } catch (Exception cleanup) when (failure is not null) {
                logger.LogError(cleanup, "Agent activity disposal failed after {FailureType}. Stream={StreamId}.",
                    failure.GetType().Name, operation.StreamId);
            } finally {
                if (entry is not null) {
                    await FinishAsync(entry, failure).ConfigureAwait(false);
                }
            }
        }
    }

    private AgentChatExecutionSession Admit(AgentChatOwnerId owner, IAgentExecutionActivityOperationLease operation,
        AgentChatHandleId? handle, AgentChatReturnLocation? location, AgentConversationContextBinding? binding, bool requiresIdle) {
        if (currentProfileId is not null && (operation.StreamId.DatabaseProfileId != currentProfileId ||
                operation.StreamId.DatabaseProfileGeneration.Value != currentProfileGeneration)) {
            throw new InvalidOperationException("The accepted conversation belongs to a superseded database profile lifetime.");
        }
        var agentId = operation.AgentId ?? throw new ArgumentException("An agent identity is required.", nameof(operation));
        var entry = sessions.FirstOrDefault(item => item.Snapshot.StreamId.DatabaseProfileId == operation.StreamId.DatabaseProfileId &&
            item.Snapshot.StreamId.DatabaseProfileGeneration == operation.StreamId.DatabaseProfileGeneration &&
            item.Snapshot.AgentId == agentId && (operation.ChatSessionId is { } sessionId
                ? item.Snapshot.ChatSessionId == sessionId : handle is not null && item.Snapshot.ConversationHandleId == handle));
        if (entry is not null && entry.Owner != owner) {
            throw new UnauthorizedAccessException("This conversation belongs to another operator.");
        }
        if (entry?.Retired == true) {
            throw new InvalidOperationException("The conversation belongs to a retired database profile lifetime.");
        }
        if (entry?.Busy == true) {
            throw AgentChatAdmissionConflictException.ForActiveExecution(agentId, operation.ChatSessionId);
        }
        if (entry is not null && !entry.ScopeRelease.IsCompletedSuccessfully) {
            throw new InvalidOperationException("The previous execution scope could not be released. Inspect the host diagnostics before resuming this conversation.");
        }
        if (requiresIdle && entry is { Snapshot.RunState: not ActiveAgentChatRunState.Idle }) {
            throw AgentChatAdmissionConflictException.ForActiveExecution(agentId, operation.ChatSessionId);
        }
        if (entry is null) {
            PruneRecent(owner);
            if (sessions.Count >= MaximumRetainedConversations || sessions.Count(item => item.Owner == owner) >= MaximumOwnerConversations) {
                throw new InvalidOperationException("The active conversation capacity is full. Finish or dismiss a conversation before starting another.");
            }
            entry = new(owner, new(agentId, operation.ChatSessionId, handle, operation.StreamId, null,
                ActiveAgentChatRunState.Running, location, clock.GetUtcNow(), ++revision, binding));
            sessions.Add(entry);
        } else {
            entry.PreviousRunState = entry.Snapshot.RunState;
            entry.Snapshot = entry.Snapshot with {
                StreamId = operation.StreamId,
                ConversationHandleId = handle ?? entry.Snapshot.ConversationHandleId,
                RunState = ActiveAgentChatRunState.Running,
                ReturnLocation = requiresIdle ? location : location ?? entry.Snapshot.ReturnLocation,
                Binding = requiresIdle ? binding : binding ?? entry.Snapshot.Binding,
                CompletionNotification = null,
                UpdatedAtUtc = clock.GetUtcNow(),
                Revision = ++revision
            };
        }
        entry.Busy = true;
        entry.SettledRunState = null;
        entry.SettledNotification = null;
        entry.Cancellation ??= new CancellationTokenSource();
        entry.CancellationNotification = Task.CompletedTask;
        entry.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return entry;
    }

    private void PruneRecent(AgentChatOwnerId owner) {
        var now = clock.GetUtcNow();
        sessions.RemoveAll(entry => !entry.Busy && entry.Scope is null && entry.ScopeRelease.IsCompletedSuccessfully && entry.Snapshot.RunState == ActiveAgentChatRunState.Idle &&
            now - entry.Snapshot.UpdatedAtUtc >= RecentRetention);
        while (sessions.Count >= MaximumRetainedConversations || sessions.Count(entry => entry.Owner == owner) >= MaximumOwnerConversations) {
            var oldest = sessions.Where(entry => !entry.Busy && entry.Scope is null && entry.ScopeRelease.IsCompletedSuccessfully && entry.Snapshot.RunState == ActiveAgentChatRunState.Idle &&
                    (sessions.Count < MaximumRetainedConversations ? entry.Owner == owner : true))
                .MinBy(entry => entry.Snapshot.UpdatedAtUtc);
            if (oldest is null) {
                return;
            }
            sessions.Remove(oldest);
        }
    }

    private void Update(AgentChatExecutionSession entry, ActiveAgentChatRunState? state, Guid? sessionId, Guid? runId) {
        var binding = runId is not null ? entry.Scope?.Executor.AdoptContext() : null;
        lock (gate) {
            entry.SettledRunState = state ?? entry.SettledRunState;
            entry.Snapshot = entry.Snapshot with {
                ChatSessionId = sessionId ?? entry.Snapshot.ChatSessionId,
                ExecutionRunId = runId ?? entry.Snapshot.ExecutionRunId,
                RunState = entry.Busy ? ActiveAgentChatRunState.Running : state ?? entry.Snapshot.RunState,
                Binding = binding ?? entry.Snapshot.Binding,
                UpdatedAtUtc = clock.GetUtcNow(),
                Revision = ++revision
            };
        }
        RaiseChanged();
    }

    private async Task FinishAsync(AgentChatExecutionSession entry, Exception? failure) {
        try {
            bool releaseScope;
            CancellationTokenSource? cancellation;
            Task cancellationNotification;
            lock (gate) {
                releaseScope = (entry.SettledRunState ?? entry.Snapshot.RunState) == ActiveAgentChatRunState.Idle || stopping || entry.Retired;
                cancellation = entry.Cancellation;
                entry.Cancellation = null;
                cancellationNotification = entry.CancellationNotification;
            }
            try {
                await cancellationNotification.ConfigureAwait(false);
                if (releaseScope) {
                    await ReleaseRetainedScopeAsync(entry).ConfigureAwait(false);
                }
            } finally {
                cancellation?.Dispose();
            }
        } catch (Exception cleanup) {
            logger.LogError(cleanup, "Agent execution scope cleanup failed. PriorFailureType={FailureType} Stream={StreamId}.",
                failure?.GetType().Name, entry.Snapshot.StreamId);
            if (failure is null) {
                throw;
            }
        } finally {
            lock (gate) {
                entry.Busy = false;
                entry.Snapshot = entry.Snapshot with {
                    RunState = entry.SettledRunState ?? entry.Snapshot.RunState,
                    CompletionNotification = entry.SettledNotification,
                    UpdatedAtUtc = clock.GetUtcNow(),
                    Revision = ++revision
                };
                entry.Completion!.TrySetResult();
            }
            RaiseChanged();
        }
    }

    private void RaiseChanged() {
        foreach (EventHandler subscriber in Changed?.GetInvocationList() ?? []) {
            try {
                subscriber(this, EventArgs.Empty);
            } catch (Exception exception) {
                logger.LogWarning(exception, "An agent execution catalog observer failed. Execution remains owned by the application.");
            }
        }
    }

    private static void RequireOwner(AgentChatOwnerId owner) {
        if (owner.IsEmpty) {
            throw new UnauthorizedAccessException("A verified conversation owner is required.");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        lock (gate) {
            if (!subscribed && !stopping && profileNotifications is not null) {
                profileNotifications.Changed += HandleProfileChanged;
                subscribed = true;
            }
        }
        return Task.CompletedTask;
    }

    private void HandleProfileChanged(object? sender, DatabaseProfileChangedNotification notification) {
        lock (gate) {
            if (stopping) {
                return;
            }
            if (currentProfileId is not null && notification.Generation < currentProfileGeneration) {
                return;
            }
            currentProfileId = notification.CurrentProfileId;
            currentProfileGeneration = notification.Generation;
            var retired = sessions.Where(entry => !entry.Retired &&
                (entry.Snapshot.StreamId.DatabaseProfileId != notification.CurrentProfileId ||
                entry.Snapshot.StreamId.DatabaseProfileGeneration.Value != notification.Generation)).ToArray();
            if (retired.Length == 0) {
                return;
            }
            foreach (var entry in retired) {
                entry.Retired = true;
                if (entry.Cancellation is { } cancellation) {
                    entry.CancellationNotification = ObserveCancellationAsync(cancellation.CancelAsync());
                }
            }
            retirements.RemoveAll(task => task.IsCompletedSuccessfully);
            using (ExecutionContext.SuppressFlow()) {
                retirements.Add(Task.Run(() => RetireAsync(retired)));
            }
        }
        RaiseChanged();
    }

    private async Task RetireAsync(AgentChatExecutionSession[] retired) {
        List<Exception>? failures = null;
        foreach (var entry in retired) {
            try {
                Task completion;
                lock (gate) {
                    completion = entry.Busy ? entry.Completion!.Task : Task.CompletedTask;
                }
                await completion.ConfigureAwait(false);
                await ReleaseRetainedScopeAsync(entry).ConfigureAwait(false);
                lock (gate) {
                    sessions.Remove(entry);
                }
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
                logger.LogError(exception, "Failed to retire an agent execution scope after a database profile switch. Stream={StreamId}.",
                    entry.Snapshot.StreamId);
            }
        }
        RaiseChanged();
        if (failures is not null) {
            throw new AggregateException("One or more retired agent execution scopes could not be released.", failures);
        }
    }

    private Task ReleaseRetainedScopeAsync(AgentChatExecutionSession entry) {
        IAgentChatExecutionScope? scope;
        TaskCompletionSource completion;
        lock (gate) {
            scope = entry.Scope;
            if (scope is null) {
                return entry.ScopeRelease;
            }
            entry.Scope = null;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            entry.ScopeRelease = completion.Task;
        }
        _ = ReleaseScopeCoreAsync(scope, completion);
        return completion.Task;
    }

    private static async Task ReleaseScopeCoreAsync(IAgentChatExecutionScope scope, TaskCompletionSource completion) {
        try {
            await scope.DisposeAsync().ConfigureAwait(false);
            completion.TrySetResult();
        } catch (Exception exception) {
            completion.TrySetException(exception);
        }
    }

    private async Task ObserveCancellationAsync(Task cancellation) {
        try {
            await cancellation.ConfigureAwait(false);
        } catch (Exception exception) {
            logger.LogError(exception, "An owned agent cancellation callback failed; cancellation remains requested.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        lock (gate) {
            stopping = true;
            if (subscribed) {
                profileNotifications!.Changed -= HandleProfileChanged;
                subscribed = false;
            }
            stopTask ??= StopCoreAsync();
            return stopTask.WaitAsync(cancellationToken);
        }
    }

    private async Task StopCoreAsync() {
        AgentChatExecutionSession[] entries;
        Task[] running;
        Task[] retiring;
        lock (gate) {
            entries = sessions.ToArray();
            retiring = retirements.ToArray();
            running = entries.Where(entry => entry.Busy).Select(entry => entry.Completion!.Task).ToArray();
            foreach (var entry in entries) {
                if (entry.Cancellation is { } cancellation) {
                    entry.CancellationNotification = ObserveCancellationAsync(cancellation.CancelAsync());
                }
            }
        }
        await Task.WhenAll(running).ConfigureAwait(false);
        await Task.WhenAll(entries.Select(ReleaseRetainedScopeAsync).Concat(retiring)).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));
}
