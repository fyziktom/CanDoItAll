using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Identity of one floating conversation for context affinity purposes: the
/// floating chat handle before a chat session exists, the chat session after.
/// </summary>
public readonly record struct AgentConversationKey
{
    private AgentConversationKey(AgentChatHandleId? handleId, Guid? chatSessionId)
    {
        HandleId = handleId;
        ChatSessionId = chatSessionId;
    }

    public AgentChatHandleId? HandleId { get; }

    public Guid? ChatSessionId { get; }

    public bool IsEmpty => HandleId is null && ChatSessionId is null;

    public static AgentConversationKey ForHandle(AgentChatHandleId handleId)
    {
        if (handleId.IsEmpty)
        {
            throw new ArgumentException("A floating chat handle id is required.", nameof(handleId));
        }

        return new AgentConversationKey(handleId, null);
    }

    public static AgentConversationKey ForSession(Guid chatSessionId)
    {
        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException("A chat session id is required.", nameof(chatSessionId));
        }

        return new AgentConversationKey(null, chatSessionId);
    }

    public override string ToString()
        => ChatSessionId is { } sessionId
            ? $"session:{sessionId:N}"
            : $"handle:{HandleId}";
}

/// <summary>
/// Owns what each floating conversation follows: pending bindings by floating
/// handle before a chat session exists, session-owned bindings afterwards.
/// Updates use expected-revision compare-and-swap; a lost race means a newer
/// turn already adopted context and the stale update is refused. The binding
/// never grants execution authority.
/// </summary>
public interface IAgentConversationContextService
{
    event EventHandler? Changed;

    AgentConversationContextBinding GetOrCreateBinding(AgentConversationKey key);

    AgentConversationContextBinding? TryGetBinding(AgentConversationKey key);

    /// <summary>
    /// Records that an admitted turn adopted the captured source. Returns
    /// <c>false</c> when the expected revision no longer matches (a newer turn
    /// or an explicit mode change already advanced the binding).
    /// </summary>
    bool TryCommitTurnAdoption(
        AgentConversationKey key,
        AgentConversationBindingRevision expectedRevision,
        AgentContextEpochId contextEpochId,
        AgentChatContextSourceKind sourceKind,
        AgentChatContextSourceId sourceId,
        string displayName,
        string surface,
        string view,
        string turnContextDigest,
        string selectionId = "");

    /// <summary>Explicit user action: stop following the application surface.</summary>
    AgentConversationContextBinding Detach(AgentConversationKey key);

    /// <summary>Explicit user action: resume following the current surface.</summary>
    AgentConversationContextBinding FollowCurrentSurface(AgentConversationKey key);

    /// <summary>
    /// Atomically and idempotently transfers a pending handle-keyed binding to
    /// the chat session created by the first send.
    /// </summary>
    AgentConversationContextBinding TransferToSession(AgentChatHandleId handleId, Guid chatSessionId);

    void Remove(AgentConversationKey key);
}

public sealed class AgentConversationContextService(TimeProvider timeProvider)
    : IAgentConversationContextService
{
    private const int MaximumBindings = 256;
    private readonly object gate = new();
    private readonly Dictionary<AgentConversationKey, AgentConversationContextBinding> bindings = [];

    public event EventHandler? Changed;

    public AgentConversationContextBinding GetOrCreateBinding(AgentConversationKey key)
    {
        ValidateKey(key);
        lock (gate)
        {
            if (bindings.TryGetValue(key, out var existing))
            {
                return existing;
            }

            if (bindings.Count >= MaximumBindings)
            {
                throw new InvalidOperationException(
                    $"No more than {MaximumBindings} conversation context bindings can be active in one workspace scope.");
            }

            var nowUtc = timeProvider.GetUtcNow();
            var binding = new AgentConversationContextBinding(
                AgentConversationContextMode.FollowCurrentSurface,
                AgentContextEpochId.Create(),
                new AgentConversationBindingRevision(1),
                nowUtc,
                nowUtc,
                handleId: key.HandleId,
                chatSessionId: key.ChatSessionId);
            bindings.Add(key, binding);
            return binding;
        }
    }

    public AgentConversationContextBinding? TryGetBinding(AgentConversationKey key)
    {
        ValidateKey(key);
        lock (gate)
        {
            return bindings.TryGetValue(key, out var binding) ? binding : null;
        }
    }

    public bool TryCommitTurnAdoption(
        AgentConversationKey key,
        AgentConversationBindingRevision expectedRevision,
        AgentContextEpochId contextEpochId,
        AgentChatContextSourceKind sourceKind,
        AgentChatContextSourceId sourceId,
        string displayName,
        string surface,
        string view,
        string turnContextDigest,
        string selectionId = "")
    {
        ValidateKey(key);
        if (contextEpochId.IsEmpty)
        {
            throw new ArgumentException("A context epoch id is required.", nameof(contextEpochId));
        }

        lock (gate)
        {
            if (!bindings.TryGetValue(key, out var binding) ||
                binding.Revision != expectedRevision ||
                binding.Mode != AgentConversationContextMode.FollowCurrentSurface)
            {
                return false;
            }

            bindings[key] = new AgentConversationContextBinding(
                AgentConversationContextMode.FollowCurrentSurface,
                contextEpochId,
                binding.Revision.Next(),
                binding.AdoptedAtUtc,
                timeProvider.GetUtcNow(),
                binding.HandleId,
                binding.ChatSessionId,
                sourceKind,
                sourceId,
                displayName,
                surface,
                view,
                turnContextDigest,
                selectionId);
        }

        RaiseChanged();
        return true;
    }

    public AgentConversationContextBinding Detach(AgentConversationKey key)
        => ChangeMode(key, AgentConversationContextMode.Detached);

    public AgentConversationContextBinding FollowCurrentSurface(AgentConversationKey key)
        => ChangeMode(key, AgentConversationContextMode.FollowCurrentSurface);

    public AgentConversationContextBinding TransferToSession(
        AgentChatHandleId handleId,
        Guid chatSessionId)
    {
        var handleKey = AgentConversationKey.ForHandle(handleId);
        var sessionKey = AgentConversationKey.ForSession(chatSessionId);
        lock (gate)
        {
            if (bindings.TryGetValue(sessionKey, out var alreadyTransferred))
            {
                // Idempotent: the transfer already happened.
                bindings.Remove(handleKey);
                return alreadyTransferred;
            }

            if (!bindings.TryGetValue(handleKey, out var pending))
            {
                // No pending binding: create the session binding directly.
                return GetOrCreateBindingLocked(sessionKey);
            }

            var transferred = new AgentConversationContextBinding(
                pending.Mode,
                pending.ContextEpochId,
                pending.Revision.Next(),
                pending.AdoptedAtUtc,
                timeProvider.GetUtcNow(),
                handleId: null,
                chatSessionId: chatSessionId,
                pending.SourceKind,
                pending.SourceId,
                pending.DisplayName,
                pending.LastSurface,
                pending.LastView,
                pending.LastTurnContextDigest,
                pending.LastSelectionId);
            bindings.Remove(handleKey);
            bindings[sessionKey] = transferred;
            return transferred;
        }
    }

    public void Remove(AgentConversationKey key)
    {
        ValidateKey(key);
        bool removed;
        lock (gate)
        {
            removed = bindings.Remove(key);
        }

        if (removed)
        {
            RaiseChanged();
        }
    }

    private AgentConversationContextBinding ChangeMode(
        AgentConversationKey key,
        AgentConversationContextMode mode)
    {
        ValidateKey(key);
        AgentConversationContextBinding updated;
        lock (gate)
        {
            var binding = GetOrCreateBindingLocked(key);
            if (binding.Mode == mode)
            {
                return binding;
            }

            // A mode change always starts a new context epoch; a detached
            // conversation drops its followed source identity.
            updated = new AgentConversationContextBinding(
                mode,
                AgentContextEpochId.Create(),
                binding.Revision.Next(),
                binding.AdoptedAtUtc,
                timeProvider.GetUtcNow(),
                binding.HandleId,
                binding.ChatSessionId,
                sourceKind: mode == AgentConversationContextMode.Detached ? null : binding.SourceKind,
                sourceId: mode == AgentConversationContextMode.Detached ? null : binding.SourceId,
                displayName: mode == AgentConversationContextMode.Detached ? string.Empty : binding.DisplayName,
                lastSurface: binding.LastSurface,
                lastView: binding.LastView,
                lastTurnContextDigest: binding.LastTurnContextDigest,
                lastSelectionId: mode == AgentConversationContextMode.Detached ? string.Empty : binding.LastSelectionId);
            bindings[key] = updated;
        }

        RaiseChanged();
        return updated;
    }

    private AgentConversationContextBinding GetOrCreateBindingLocked(AgentConversationKey key)
    {
        if (bindings.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var nowUtc = timeProvider.GetUtcNow();
        var binding = new AgentConversationContextBinding(
            AgentConversationContextMode.FollowCurrentSurface,
            AgentContextEpochId.Create(),
            new AgentConversationBindingRevision(1),
            nowUtc,
            nowUtc,
            handleId: key.HandleId,
            chatSessionId: key.ChatSessionId);
        bindings.Add(key, binding);
        return binding;
    }

    private static void ValidateKey(AgentConversationKey key)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException("A conversation key is required.", nameof(key));
        }
    }

    private void RaiseChanged()
        => Changed?.Invoke(this, EventArgs.Empty);
}
