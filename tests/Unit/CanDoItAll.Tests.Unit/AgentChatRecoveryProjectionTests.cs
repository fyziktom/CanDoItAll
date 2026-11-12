using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentChatRecoveryProjectionTests {
    [Fact]
    public void First_recovery_is_hidden_and_never_claims_an_unsent_browser_draft() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var incoming = Chat() with { Visibility = ActiveAgentChatVisibility.Visible, HasUnsentDraft = true };

        var restored = registry.Restore(incoming);

        Assert.False(restored.IsVisible);
        Assert.False(restored.HasUnsentDraft);
        Assert.NotNull(restored.HiddenAtUtc);
        Assert.Equal(incoming.ExecutionRunId, restored.ExecutionRunId);
        Assert.Equal(incoming.ActivityStreamId, restored.ActivityStreamId);
    }

    [Fact]
    public void New_execution_state_preserves_this_circuits_visibility_and_draft_and_rejects_late_updates() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var first = registry.Restore(Chat());
        registry.Show(first.HandleId);
        registry.SetDraftPending(first.HandleId, true);
        var next = first with { RunState = ActiveAgentChatRunState.AwaitingApproval, ExecutionRevision = first.ExecutionRevision + 1 };

        var current = registry.Restore(next);
        var late = registry.Restore(first with { RunState = ActiveAgentChatRunState.Idle });

        Assert.True(current.IsVisible);
        Assert.True(current.HasUnsentDraft);
        Assert.Null(current.HiddenAtUtc);
        Assert.Equal(first.CreatedAtUtc, current.CreatedAtUtc);
        Assert.Equal(ActiveAgentChatRunState.AwaitingApproval, late.RunState);
        Assert.Equal(current, late);
    }

    [Fact]
    public void Recovery_binds_the_original_unsaved_handle_as_soon_as_execution_creates_its_session() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var incoming = Chat();
        var original = registry.Open(incoming.Agent, null, FloatingAgentChatSettings.Default);
        registry.SetDraftPending(original.HandleId, true);

        var restored = registry.Restore(incoming with { HandleId = original.HandleId });

        Assert.Equal(original.HandleId, restored.HandleId);
        Assert.Equal(incoming.ChatSessionId, restored.ChatSessionId);
        Assert.Equal(incoming.ActivityStreamId, restored.ActivityStreamId);
        Assert.Equal(incoming.RunState, restored.RunState);
        Assert.True(restored.IsVisible);
        Assert.True(restored.HasUnsentDraft);
        Assert.Equal(original.CreatedAtUtc, restored.CreatedAtUtc);
        Assert.Equal(restored, Assert.Single(registry.Snapshot()));
    }

    [Fact]
    public void Binding_an_unsaved_handle_cannot_replace_its_agent_or_duplicate_another_handles_session() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var existing = registry.Restore(Chat());
        var original = registry.Open(existing.Agent, null, FloatingAgentChatSettings.Default);

        Assert.Throws<InvalidOperationException>(() => registry.Restore(existing with { HandleId = original.HandleId }));
        Assert.Throws<InvalidOperationException>(() => registry.Restore(Chat() with { HandleId = original.HandleId }));

        Assert.Null(registry.Snapshot().Single(item => item.HandleId == original.HandleId).ChatSessionId);
    }

    [Fact]
    public void Recovery_cannot_rebind_an_existing_handle_or_duplicate_a_session() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var first = registry.Restore(Chat());

        Assert.Throws<InvalidOperationException>(() => registry.Restore(first with {
            ChatSessionId = Guid.NewGuid(), ExecutionRevision = first.ExecutionRevision + 1
        }));
        Assert.Throws<InvalidOperationException>(() => registry.Restore(first with {
            ChatSessionId = null, ExecutionRevision = first.ExecutionRevision + 1
        }));
        Assert.Throws<InvalidOperationException>(() => registry.Restore(first with { HandleId = AgentChatHandleId.Create() }));
        Assert.Equal(first, Assert.Single(registry.Snapshot()));
    }

    [Fact]
    public void Removing_a_running_projection_does_not_remove_an_unsubmitted_browser_chat() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var running = registry.Restore(Chat());
        var draft = registry.Open(new(Guid.NewGuid(), "Draft", null, null), null, FloatingAgentChatSettings.Default);
        registry.SetDraftPending(draft.HandleId, true);

        Assert.True(registry.RemoveRecoveryProjection(running.HandleId));
        Assert.False(registry.RemoveRecoveryProjection(draft.HandleId));
        Assert.False(registry.RemoveRecoveryProjection(running.HandleId));

        var remaining = Assert.Single(registry.Snapshot());
        Assert.Equal(draft.HandleId, remaining.HandleId);
        Assert.True(remaining.HasUnsentDraft);
    }

    [Fact]
    public void Restored_binding_preserves_newer_local_mode_and_refuses_equal_revision_conflicts() {
        var service = new AgentConversationContextService(TimeProvider.System);
        var session = Guid.NewGuid();
        var key = AgentConversationKey.ForSession(session);
        var original = Binding(session, 3);
        service.RestoreBinding(key, original);
        var detached = service.Detach(key);

        service.RestoreBinding(key, original);

        Assert.Equal(detached, service.TryGetBinding(key));
        Assert.Equal(AgentConversationContextMode.Detached, service.TryGetBinding(key)!.Mode);
        Assert.Throws<InvalidOperationException>(() => service.RestoreBinding(key, Binding(session, detached.Revision.Value)));
        Assert.Equal(detached, service.TryGetBinding(key));
    }

    [Fact]
    public void Binding_recovery_is_idempotent_and_cannot_restore_a_different_session() {
        var service = new AgentConversationContextService(TimeProvider.System);
        var session = Guid.NewGuid();
        var key = AgentConversationKey.ForSession(session);
        var original = Binding(session, 2);
        var changes = 0;
        service.Changed += (_, _) => changes++;

        service.RestoreBinding(key, original);
        service.RestoreBinding(key, original);

        Assert.Equal(1, changes);
        Assert.Throws<InvalidOperationException>(() => service.RestoreBinding(key, Binding(Guid.NewGuid(), 3)));
        Assert.Equal(original, service.TryGetBinding(key));
    }

    private static ActiveAgentChat Chat() {
        var now = DateTimeOffset.UtcNow;
        return new(AgentChatHandleId.Create(), new(Guid.NewGuid(), "Recovered agent", null, null), Guid.NewGuid(),
            ActiveAgentChatVisibility.Hidden, ActiveAgentChatRunState.Running, now, now, now) {
            ActivityStreamId = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1), new(Guid.NewGuid())),
            ExecutionRunId = Guid.NewGuid(), ExecutionRevision = 5,
            ReturnLocation = new("/projects")
        };
    }

    private static AgentConversationContextBinding Binding(Guid session, long revision) => new(
        AgentConversationContextMode.FollowCurrentSurface, AgentContextEpochId.Create(), new(revision),
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, chatSessionId: session,
        sourceKind: new("project-structure"), sourceId: new("source"), displayName: "Project");
}
