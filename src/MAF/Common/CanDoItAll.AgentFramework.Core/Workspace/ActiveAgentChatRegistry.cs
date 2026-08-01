using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IActiveAgentChatRegistry
{
    event EventHandler? Changed;

    IReadOnlyList<ActiveAgentChat> Snapshot();

    ActiveAgentChat Open(
        AgentChatIdentity agent,
        Guid? chatSessionId,
        FloatingAgentChatSettings settings);

    ActiveAgentChat AttachSession(AgentChatHandleId handleId, Guid chatSessionId);

    ActiveAgentChat Show(AgentChatHandleId handleId);

    ActiveAgentChat KeepActive(AgentChatHandleId handleId);

    ActiveAgentChat SetRunState(
        AgentChatHandleId handleId,
        ActiveAgentChatRunState runState);

    void Stop(
        AgentChatHandleId handleId,
        AgentChatHandleId? restoreIfStoppedChatWasVisible = null);

    int PruneExpired(FloatingAgentChatSettings settings);
}

public sealed class ActiveAgentChatRegistry(TimeProvider timeProvider) : IActiveAgentChatRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<AgentChatHandleId, ActiveAgentChat> chats = [];

    public event EventHandler? Changed;

    public IReadOnlyList<ActiveAgentChat> Snapshot()
    {
        lock (gate)
        {
            return CreateSnapshot();
        }
    }

    public ActiveAgentChat Open(
        AgentChatIdentity agent,
        Guid? chatSessionId,
        FloatingAgentChatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ValidateSessionId(chatSessionId);
        settings = FloatingAgentChatSettingsValidator.Normalize(settings);
        ActiveAgentChat? opened = null;
        string? failureMessage = null;
        var prunedCount = 0;

        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            prunedCount = PruneExpiredCore(settings, now);
            var existing = chatSessionId.HasValue
                ? chats.Values.FirstOrDefault(item =>
                    item.ChatSessionId == chatSessionId)
                : null;
            if (existing is not null && existing.Agent.AgentId != agent.AgentId)
            {
                failureMessage =
                    $"Chat session '{chatSessionId:N}' is already active for agent '{existing.Agent.AgentId:N}'.";
            }
            else if (existing is not null)
            {
                HideOtherChats(existing.HandleId, now);
                opened = existing with
                {
                    Agent = agent,
                    Visibility = ActiveAgentChatVisibility.Visible,
                    HiddenAtUtc = null,
                    LastActivityUtc = now
                };
                chats[existing.HandleId] = opened;
            }
            else
            {
                if (chats.Count >= settings.MaximumActiveChats)
                {
                    failureMessage =
                        $"The maximum of {settings.MaximumActiveChats} active agent chats has been reached. Stop an inactive chat before opening another one.";
                }
                else
                {
                    var handleId = AgentChatHandleId.Create();
                    HideOtherChats(handleId, now);
                    opened = new ActiveAgentChat(
                        handleId,
                        agent,
                        chatSessionId,
                        ActiveAgentChatVisibility.Visible,
                        ActiveAgentChatRunState.Idle,
                        now,
                        now,
                        HiddenAtUtc: null);
                    chats.Add(handleId, opened);
                }
            }
        }

        if (prunedCount > 0 || opened is not null)
        {
            RaiseChanged();
        }

        if (failureMessage is not null)
        {
            throw new InvalidOperationException(failureMessage);
        }

        return opened!;
    }

    public ActiveAgentChat AttachSession(AgentChatHandleId handleId, Guid chatSessionId)
    {
        ValidateHandleId(handleId);
        ValidateSessionId(chatSessionId);
        ActiveAgentChat? updated = null;

        lock (gate)
        {
            var chat = RequireChat(handleId);
            if (chat.ChatSessionId.HasValue)
            {
                if (chat.ChatSessionId.Value != chatSessionId)
                {
                    throw new InvalidOperationException(
                        $"Active chat handle '{handleId}' is already attached to session '{chat.ChatSessionId.Value:N}' and cannot be rebound.");
                }

                return chat;
            }

            var duplicate = chats.Values.FirstOrDefault(item =>
                item.HandleId != handleId &&
                item.ChatSessionId == chatSessionId);
            if (duplicate is not null)
            {
                throw new InvalidOperationException(
                    $"Chat session '{chatSessionId:N}' is already active in handle '{duplicate.HandleId}'.");
            }

            updated = chat with
            {
                ChatSessionId = chatSessionId,
                LastActivityUtc = timeProvider.GetUtcNow()
            };
            chats[handleId] = updated;
        }

        RaiseChanged();
        return updated!;
    }

    public ActiveAgentChat Show(AgentChatHandleId handleId)
    {
        ValidateHandleId(handleId);
        ActiveAgentChat updated;
        var changed = false;
        lock (gate)
        {
            var chat = RequireChat(handleId);
            var now = timeProvider.GetUtcNow();
            HideOtherChats(handleId, now);
            updated = chat with
            {
                Visibility = ActiveAgentChatVisibility.Visible,
                HiddenAtUtc = null,
                LastActivityUtc = now
            };
            chats[handleId] = updated;
            changed = true;
        }

        if (changed)
        {
            RaiseChanged();
        }

        return updated;
    }

    public ActiveAgentChat KeepActive(AgentChatHandleId handleId)
    {
        ValidateHandleId(handleId);
        ActiveAgentChat updated;
        lock (gate)
        {
            var chat = RequireChat(handleId);
            var now = timeProvider.GetUtcNow();
            updated = chat with
            {
                Visibility = ActiveAgentChatVisibility.Hidden,
                HiddenAtUtc = now,
                LastActivityUtc = now
            };
            chats[handleId] = updated;
        }

        RaiseChanged();
        return updated;
    }

    public ActiveAgentChat SetRunState(
        AgentChatHandleId handleId,
        ActiveAgentChatRunState runState)
    {
        ValidateHandleId(handleId);
        if (!Enum.IsDefined(runState))
        {
            throw new ArgumentOutOfRangeException(nameof(runState), runState, "The active chat run state is invalid.");
        }

        ActiveAgentChat updated;
        lock (gate)
        {
            var chat = RequireChat(handleId);
            if (chat.RunState == runState)
            {
                return chat;
            }

            var now = timeProvider.GetUtcNow();
            updated = chat with
            {
                RunState = runState,
                LastActivityUtc = now,
                HiddenAtUtc = runState == ActiveAgentChatRunState.Idle && !chat.IsVisible
                    ? now
                    : chat.HiddenAtUtc
            };
            chats[handleId] = updated;
        }

        RaiseChanged();
        return updated;
    }

    public void Stop(
        AgentChatHandleId handleId,
        AgentChatHandleId? restoreIfStoppedChatWasVisible = null)
    {
        ValidateHandleId(handleId);
        lock (gate)
        {
            var chat = RequireChat(handleId);
            if (!chat.CanStop)
            {
                throw new InvalidOperationException(
                    "A running chat cannot be stopped until interactive execution cancellation is available.");
            }

            chats.Remove(handleId);
            if (chat.IsVisible &&
                restoreIfStoppedChatWasVisible is { } restoreHandleId &&
                chats.TryGetValue(restoreHandleId, out var restoreChat))
            {
                var now = timeProvider.GetUtcNow();
                HideOtherChats(restoreHandleId, now);
                chats[restoreHandleId] = restoreChat with
                {
                    Visibility = ActiveAgentChatVisibility.Visible,
                    HiddenAtUtc = null,
                    LastActivityUtc = now
                };
            }
        }

        RaiseChanged();
    }

    public int PruneExpired(FloatingAgentChatSettings settings)
    {
        settings = FloatingAgentChatSettingsValidator.Normalize(settings);
        int removedCount;

        lock (gate)
        {
            removedCount = PruneExpiredCore(settings, timeProvider.GetUtcNow());
        }

        if (removedCount > 0)
        {
            RaiseChanged();
        }

        return removedCount;
    }

    private int PruneExpiredCore(
        FloatingAgentChatSettings settings,
        DateTimeOffset now)
    {
        var expiredHandleIds = chats.Values
            .Where(item =>
                item.Visibility == ActiveAgentChatVisibility.Hidden &&
                item.RunState == ActiveAgentChatRunState.Idle &&
                item.HiddenAtUtc.HasValue &&
                now - item.HiddenAtUtc.Value >= settings.HiddenActiveChatRetention)
            .Select(item => item.HandleId)
            .ToArray();

        foreach (var handleId in expiredHandleIds)
        {
            chats.Remove(handleId);
        }

        return expiredHandleIds.Length;
    }

    private void HideOtherChats(AgentChatHandleId visibleHandleId, DateTimeOffset now)
    {
        foreach (var chat in chats.Values.Where(item => item.HandleId != visibleHandleId && item.IsVisible).ToArray())
        {
            chats[chat.HandleId] = chat with
            {
                Visibility = ActiveAgentChatVisibility.Hidden,
                HiddenAtUtc = now,
                LastActivityUtc = now
            };
        }
    }

    private ActiveAgentChat RequireChat(AgentChatHandleId handleId)
    {
        return chats.TryGetValue(handleId, out var chat)
            ? chat
            : throw new InvalidOperationException(
                $"Active agent chat handle '{handleId}' was not found.");
    }

    private IReadOnlyList<ActiveAgentChat> CreateSnapshot()
    {
        return chats.Values
            .OrderByDescending(item => item.IsVisible)
            .ThenByDescending(item => item.LastActivityUtc)
            .ThenBy(item => item.HandleId.Value)
            .ToArray();
    }

    private static void ValidateSessionId(Guid? chatSessionId)
    {
        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException("A chat session id cannot be empty.", nameof(chatSessionId));
        }
    }

    private static void ValidateHandleId(AgentChatHandleId handleId)
    {
        if (handleId.IsEmpty)
        {
            throw new ArgumentException("An active chat handle id is required.", nameof(handleId));
        }
    }

    private void RaiseChanged()
        => Changed?.Invoke(this, EventArgs.Empty);
}
