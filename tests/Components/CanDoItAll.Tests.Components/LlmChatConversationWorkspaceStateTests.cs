using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class LlmChatConversationWorkspaceStateTests
{
    [Fact]
    public void Page_state_deduplicates_caps_and_disables_paging_at_capacity()
    {
        var page = new LlmChatWorkspacePage<PageItem, Guid, int>(item => item.Id, maximumCount: 3);
        var first = new PageItem(Guid.NewGuid(), "first");
        var second = new PageItem(Guid.NewGuid(), "second");
        var third = new PageItem(Guid.NewGuid(), "third");

        page.Replace([first, second], nextCursor: 2);
        page.Append([second with { Name = "duplicate" }, third, new(Guid.NewGuid(), "overflow")], nextCursor: 4);

        Assert.Equal([first, second, third], page.Items);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void Page_state_upserts_existing_items_and_inserts_new_items_first()
    {
        var page = new LlmChatWorkspacePage<PageItem, Guid, int>(item => item.Id, maximumCount: 2);
        var first = new PageItem(Guid.NewGuid(), "first");
        var second = new PageItem(Guid.NewGuid(), "second");
        var replacement = first with { Name = "updated" };
        var newest = new PageItem(Guid.NewGuid(), "newest");

        page.Replace([first, second], nextCursor: null);
        page.UpsertFirst(replacement);
        page.UpsertFirst(newest);

        Assert.Equal([newest, replacement], page.Items);
    }

    [Fact]
    public void Operation_state_reuses_only_matching_admission_identity()
    {
        var state = new LlmChatOperationWorkspaceState();
        var conversationId = Guid.NewGuid();

        var first = state.GetAdmissionOperationId(conversationId, "hello");
        var retry = state.GetAdmissionOperationId(conversationId, "hello");
        var changedMessage = state.GetAdmissionOperationId(conversationId, "different");
        var changedConversation = state.GetAdmissionOperationId(Guid.NewGuid(), "different");

        Assert.Equal(first, retry);
        Assert.NotEqual(first, changedMessage);
        Assert.NotEqual(changedMessage, changedConversation);
    }

    [Fact]
    public void Operation_state_applies_only_matching_projection_and_resets_transient_state()
    {
        var state = new LlmChatOperationWorkspaceState();
        var operation = CreateOperation(LlmChatOperationStatus.Running);
        state.Start(operation, "hello", DateTimeOffset.Parse("2026-08-17T20:00:00Z"));

        state.ApplyProjection(LlmChatOperationProjectionState.Initial(Guid.NewGuid()));
        Assert.Equal(LlmChatOperationStatus.Running, state.ActiveOperation?.Status);

        state.ApplyProjection(LlmChatOperationProjectionState.Initial(operation.OperationId) with
        {
            Status = LlmChatOperationStatus.RecoveryRequired
        });
        state.CompleteMutation(state.ActiveOperation!, recoveryEvidenceConfirmed: true);

        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, state.ActiveOperation?.Status);
        Assert.True(state.RecoveryEvidenceConfirmed);
        Assert.NotNull(state.PendingTurn);

        state.Reset();

        Assert.Null(state.ActiveOperation);
        Assert.Null(state.PendingTurn);
        Assert.Null(state.Projection);
        Assert.False(state.RecoveryEvidenceConfirmed);
    }

    private static LlmChatOperationView CreateOperation(LlmChatOperationStatus status)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            DateTimeOffset.Parse("2026-08-17T20:00:00Z"),
            null,
            null,
            0,
            string.Empty,
            string.Empty,
            null);

    private sealed record PageItem(Guid Id, string Name);
}
