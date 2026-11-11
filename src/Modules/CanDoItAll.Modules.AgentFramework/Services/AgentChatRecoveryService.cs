using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatRecoveryService(
    IAgentChatExecutionCatalog executions,
    IAgentChatOwnerProvider owners,
    IDatabaseProfileRuntimeAccessor profiles,
    IAgentExecutionProfileGenerationSource generations,
    IAgentFrameworkWorkspaceService workspace,
    IActiveAgentChatRegistry chats,
    IAgentConversationContextService contexts,
    IAgentChatExecutionNotificationHub notifications,
    IDatabaseSwitchNotificationService profileNotifications,
    ILogger<AgentChatRecoveryService> logger) : IAsyncDisposable {
    private readonly Lock gate = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<Guid, AgentChatIdentity> identities = [];
    private readonly HashSet<AgentExecutionActivityStreamId> notified = [];
    private AgentChatRecoveryPartition? partition;
    private Task refreshTask = Task.CompletedTask;
    private bool subscribed;
    private bool refreshRequested;
    private bool refreshRunning;
    private bool disposed;

    public event EventHandler? Changed;

    public string? FailureMessage { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        lock (gate) {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!subscribed) {
                executions.Changed += HandleExecutionChanged;
                owners.Changed += HandleExecutionChanged;
                profileNotifications.Changed += HandleProfileChanged;
                subscribed = true;
            }
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        await RefreshAsync(request.Token);
    }

    public async Task<AgentChatReturnLocation?> GetReturnLocationAsync(AgentChatHandleId handleId,
        CancellationToken cancellationToken = default) {
        await refreshGate.WaitAsync(cancellationToken);
        try {
            var batch = await ReadCurrentAsync(cancellationToken);
            AdoptPartition(batch.Partition);
            var chat = chats.Snapshot().SingleOrDefault(item => item.HandleId == handleId)
                ?? throw new UnauthorizedAccessException("This chat is no longer available in the current workspace.");
            var current = batch.Executions.SingleOrDefault(item => Matches(item, chat));
            if (current is null && chat.ActivityStreamId is not null) {
                RemoveProjection(chat);
                throw new UnauthorizedAccessException("This execution is no longer available to the current chat owner.");
            }
            return current?.ReturnLocation;
        } finally {
            refreshGate.Release();
        }
    }

    public async Task DismissAsync(AgentChatHandleId handleId, CancellationToken cancellationToken = default) {
        await refreshGate.WaitAsync(cancellationToken);
        try {
            var batch = await ReadCurrentAsync(cancellationToken);
            AdoptPartition(batch.Partition);
            var chat = chats.Snapshot().SingleOrDefault(item => item.HandleId == handleId)
                ?? throw new UnauthorizedAccessException("This chat is no longer available in the current workspace.");
            if (chat.ActivityStreamId is not null && !batch.Executions.Any(item => Matches(item, chat))) {
                RemoveProjection(chat);
                throw new UnauthorizedAccessException("This execution is no longer available to the current chat owner.");
            }
            if (chat.ChatSessionId is { } sessionId && !executions.TryDismiss(batch.Partition.Owner,
                    batch.Partition.ProfileId, batch.Partition.Generation, chat.Agent.AgentId, sessionId)) {
                throw new InvalidOperationException("Resolve or cancel the active run before removing its chat.");
            }
        } finally {
            refreshGate.Release();
        }
    }

    private async Task<AgentChatRecoveryBatch> ReadCurrentAsync(CancellationToken cancellationToken) {
        try {
            var current = await CapturePartitionAsync(cancellationToken);
            var snapshot = executions.Snapshot(current.Owner, current.ProfileId, current.Generation);
            await RequirePartitionAsync(current, cancellationToken);
            if (snapshot.Any(item => item.StreamId.DatabaseProfileId != current.ProfileId ||
                    item.StreamId.DatabaseProfileGeneration != current.Generation)) {
                throw new InvalidOperationException("The execution catalog returned a different database profile lifetime.");
            }
            return new(current, snapshot);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch {
            ClearProjections();
            throw;
        }
    }

    private async ValueTask<AgentChatRecoveryPartition> CapturePartitionAsync(CancellationToken cancellationToken) {
        var profile = profiles.ResolveCurrentProfile().Profile.Id;
        var generation = generations.GetGeneration();
        var owner = await owners.GetOwnerAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (profiles.ResolveCurrentProfile().Profile.Id != profile || generations.GetGeneration() != generation) {
            throw new InvalidOperationException("The database profile changed while recovering active chats.");
        }
        return new(owner, profile, generation);
    }

    private async ValueTask RequirePartitionAsync(AgentChatRecoveryPartition expected, CancellationToken cancellationToken) {
        try {
            if (await CapturePartitionAsync(cancellationToken) != expected) {
                throw new UnauthorizedAccessException("The chat owner or database profile changed during recovery.");
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch {
            ClearProjections();
            throw;
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken) {
        await refreshGate.WaitAsync(cancellationToken);
        try {
            var batch = await ReadCurrentAsync(cancellationToken);
            AdoptPartition(batch.Partition);
            var snapshot = batch.Executions;
            if (snapshot.Any(item => !identities.ContainsKey(item.AgentId))) {
                var agents = await workspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
                await RequirePartitionAsync(batch.Partition, cancellationToken);
                foreach (var agent in agents) {
                    identities[agent.Id] = new(agent.Id, agent.Name, agent.RoleTitle, agent.AvatarImageUrl);
                }
                var refreshed = await ReadCurrentAsync(cancellationToken);
                if (refreshed.Partition != batch.Partition) {
                    ClearProjections();
                    throw new UnauthorizedAccessException("The chat owner changed while resolving recovered agents.");
                }
                snapshot = refreshed.Executions;
            }
            foreach (var chat in chats.Snapshot().Where(chat => chat.ActivityStreamId is not null &&
                         !snapshot.Any(item => Matches(item, chat)))) {
                RemoveProjection(chat);
            }
            foreach (var item in snapshot) {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.ChatSessionId is not { } sessionId) {
                    continue;
                }
                if (!identities.TryGetValue(item.AgentId, out var identity)) {
                    throw new InvalidOperationException("An active execution's agent is unavailable in the current workspace.");
                }
                var existing = chats.Snapshot().FirstOrDefault(chat => chat.ChatSessionId == sessionId && chat.Agent.AgentId == item.AgentId);
                var handle = existing?.HandleId ?? item.ConversationHandleId ?? new AgentChatHandleId(sessionId);
                chats.Restore(new(handle, identity, sessionId, ActiveAgentChatVisibility.Hidden, item.RunState,
                    item.UpdatedAtUtc, item.UpdatedAtUtc, item.UpdatedAtUtc) {
                    ActivityStreamId = item.StreamId,
                    ExecutionRunId = item.ExecutionRunId,
                    ExecutionRevision = item.Revision,
                    ReturnLocation = item.ReturnLocation
                });
                if (item.Binding is { } binding) {
                    var key = AgentConversationKey.ForSession(sessionId);
                    var current = contexts.TryGetBinding(key);
                    if (current is null || current.Revision.Value < binding.Revision.Value) {
                        contexts.RestoreBinding(key, binding);
                    }
                }
            }
            var currentStreams = snapshot.Select(item => item.StreamId).ToHashSet();
            notified.RemoveWhere(stream => !currentStreams.Contains(stream));
            foreach (var item in snapshot.Where(item => item.CompletionNotification is not null)) {
                await RequirePartitionAsync(batch.Partition, cancellationToken);
                if (notified.Add(item.StreamId)) {
                    await notifications.PublishAsync(item.CompletionNotification!, cancellationToken);
                }
            }
            await RequirePartitionAsync(batch.Partition, cancellationToken);
            FailureMessage = null;
        } finally {
            refreshGate.Release();
        }
    }

    private void AdoptPartition(AgentChatRecoveryPartition current) {
        if (partition != current) {
            ClearProjections();
            partition = current;
        }
    }

    private void ClearProjections() {
        foreach (var chat in chats.Snapshot().Where(chat => chat.ActivityStreamId is not null)) {
            RemoveProjection(chat);
        }
        identities.Clear();
        notified.Clear();
        partition = null;
    }

    private void RemoveProjection(ActiveAgentChat chat) {
        chats.RemoveRecoveryProjection(chat.HandleId);
        if (chat.ChatSessionId is { } sessionId) {
            contexts.Remove(AgentConversationKey.ForSession(sessionId));
        }
    }

    private static bool Matches(AgentChatExecutionSnapshot item, ActiveAgentChat chat)
        => item.AgentId == chat.Agent.AgentId && (item.ConversationHandleId == chat.HandleId ||
            item.ChatSessionId is { } sessionId && sessionId == chat.ChatSessionId);

    private void HandleExecutionChanged(object? sender, EventArgs args) {
        lock (gate) {
            if (disposed) {
                return;
            }
            refreshRequested = true;
            if (!refreshRunning) {
                refreshRunning = true;
                refreshTask = RefreshLoopAsync();
            }
        }
    }

    private void HandleProfileChanged(object? sender, DatabaseProfileChangedNotification notification)
        => HandleExecutionChanged(sender, EventArgs.Empty);

    private async Task RefreshLoopAsync() {
        await Task.Yield();
        while (true) {
            lock (gate) {
                if (disposed || !refreshRequested) {
                    refreshRunning = false;
                    return;
                }
                refreshRequested = false;
            }
            try {
                await RefreshAsync(lifetime.Token);
                RaiseChanged();
            } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
                return;
            } catch (Exception exception) {
                FailureMessage = "Active chat recovery could not be refreshed. Reopen the catalog to retry.";
                logger.LogError(exception, "Unable to refresh owned agent chats. FailureType={FailureType}.", exception.GetType().Name);
                RaiseChanged();
            }
        }
    }

    private void RaiseChanged() {
        foreach (EventHandler handler in Changed?.GetInvocationList() ?? []) {
            try {
                handler(this, EventArgs.Empty);
            } catch (Exception exception) {
                logger.LogWarning(exception, "An agent chat recovery subscriber failed. FailureType={FailureType}.", exception.GetType().Name);
            }
        }
    }

    public async ValueTask DisposeAsync() {
        Task pending;
        lock (gate) {
            if (disposed) {
                return;
            }
            disposed = true;
            executions.Changed -= HandleExecutionChanged;
            owners.Changed -= HandleExecutionChanged;
            profileNotifications.Changed -= HandleProfileChanged;
            pending = refreshTask;
        }
        await lifetime.CancelAsync();
        await pending;
        lifetime.Dispose();
    }
}

internal sealed record AgentChatRecoveryPartition(AgentChatOwnerId Owner, Guid ProfileId, DatabaseProfileGeneration Generation);

internal sealed record AgentChatRecoveryBatch(AgentChatRecoveryPartition Partition, IReadOnlyList<AgentChatExecutionSnapshot> Executions);
