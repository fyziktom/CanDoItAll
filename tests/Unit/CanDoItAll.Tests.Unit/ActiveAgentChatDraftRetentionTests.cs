using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ActiveAgentChatDraftRetentionTests {
    [Fact]
    public void A_hidden_unsent_draft_survives_expiry_until_its_composer_is_cleared() {
        var clock = new DraftClock();
        var registry = new ActiveAgentChatRegistry(clock);
        var chat = registry.Open(Identity(), Guid.NewGuid(), FloatingAgentChatSettings.Default);
        registry.SetDraftPending(chat.HandleId, true);
        registry.KeepActive(chat.HandleId);
        clock.Now = clock.Now.AddHours(1);
        Assert.Equal(0, registry.PruneExpired(FloatingAgentChatSettings.Default));
        Assert.True(Assert.Single(registry.Snapshot()).HasUnsentDraft);
        registry.SetDraftPending(chat.HandleId, false);
        Assert.Equal(1, registry.PruneExpired(FloatingAgentChatSettings.Default));
        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void Draft_presence_changes_publish_once_per_transition_without_broadcasting_content() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var chat = registry.Open(Identity(), Guid.NewGuid(), FloatingAgentChatSettings.Default);
        var changes = 0;
        registry.Changed += (_, _) => changes++;
        for (var index = 0; index < 100; index++) {
            registry.SetDraftPending(chat.HandleId, true);
        }
        Assert.Equal(1, changes);
        registry.SetDraftPending(chat.HandleId, false);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Capacity_cannot_evict_a_hidden_unsent_draft_after_retention_expires() {
        var clock = new DraftClock();
        var registry = new ActiveAgentChatRegistry(clock);
        var settings = FloatingAgentChatSettings.Default with { MaximumActiveChats = 1 };
        var chat = registry.Open(Identity(), Guid.NewGuid(), settings);
        registry.SetDraftPending(chat.HandleId, true);
        registry.KeepActive(chat.HandleId);
        clock.Now = clock.Now.AddHours(1);
        Assert.Throws<InvalidOperationException>(() => registry.Open(Identity(), Guid.NewGuid(), settings));
        Assert.Equal(chat.HandleId, Assert.Single(registry.Snapshot()).HandleId);
    }

    [Fact]
    public void An_explicit_stop_discards_the_active_handle_and_its_transient_draft_flag() {
        var registry = new ActiveAgentChatRegistry(TimeProvider.System);
        var chat = registry.Open(Identity(), Guid.NewGuid(), FloatingAgentChatSettings.Default);
        registry.SetDraftPending(chat.HandleId, true);
        registry.Stop(chat.HandleId);
        Assert.Empty(registry.Snapshot());
        Assert.Throws<InvalidOperationException>(() => registry.SetDraftPending(chat.HandleId, true));
        Assert.Empty(registry.Snapshot());
    }

    private static AgentChatIdentity Identity() => new(Guid.NewGuid(), "Draft owner", "Operator", null);
    private sealed class DraftClock : TimeProvider {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
