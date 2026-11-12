using Bunit;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class LlmChatFloatingDialogOwnershipTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_dialog_result_closes_only_its_opening_dialog(bool archive) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        var host = context.Render<DialogHost>();
        var dialogs = context.Services.GetRequiredService<DialogService>();
        var conversation = Conversation();
        var first = archive
            ? dialogs.OpenAsync<LlmChatFloatingArchiveDialog>("First", new Dictionary<string, object?> {
                [nameof(LlmChatFloatingArchiveDialog.Conversation)] = conversation
            })
            : dialogs.OpenAsync<LlmChatFloatingHistoryDialog>("First", new Dictionary<string, object?> {
                [nameof(LlmChatFloatingHistoryDialog.Conversations)] = new[] { conversation }
            });
        var second = dialogs.OpenAsync<LlmChatFloatingHistoryDialog>("Second");
        host.WaitForAssertion(() => Assert.Equal(2, dialogs.Dialogs.Count));
        if (archive) {
            var firstCut = host.FindComponent<LlmChatFloatingArchiveDialog>();
            await firstCut.InvokeAsync(() => firstCut.FindComponent<DangerActionDialog>().Instance.Confirmed.InvokeAsync());
        } else {
            var firstCut = host.FindComponents<LlmChatFloatingHistoryDialog>().Single(c => c.Instance.Conversations.Count == 1);
            await firstCut.InvokeAsync(() => firstCut.FindComponent<ConversationThreadHistoryList>().Instance.Selected.InvokeAsync(
                LlmChatConversationPresentationMapper.ToKey(conversation.ConversationId)));
        }
        Assert.True(first.IsCompleted, "The result must complete the opening dialog, even with another dialog above it.");
        Assert.False(second.IsCompleted);
        Assert.Equal(archive ? true : conversation.ConversationId, await first);
        await dialogs.Dialogs.Single().CloseAsync();
        await second;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queued_same_title_result_is_refused_after_A_B_A_and_after_disposal(bool archive) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        var host = context.Render<DialogHost>();
        var dialogs = context.Services.GetRequiredService<DialogService>();
        var conversation = Conversation();
        var first = archive
            ? dialogs.OpenAsync<LlmChatFloatingArchiveDialog>("First", new Dictionary<string, object?> {
                [nameof(LlmChatFloatingArchiveDialog.Conversation)] = conversation
            })
            : dialogs.OpenAsync<LlmChatFloatingHistoryDialog>("First", new Dictionary<string, object?> {
                [nameof(LlmChatFloatingHistoryDialog.Conversations)] = new[] { conversation }
            });
        host.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        Func<Task> queued;
        if (archive) {
            var cut = host.FindComponent<LlmChatFloatingArchiveDialog>();
            var action = cut.FindComponent<DangerActionDialog>().Instance.Confirmed;
            queued = () => cut.InvokeAsync(() => action.InvokeAsync());
            cut.Render(p => p.Add(c => c.Conversation, Conversation()));
            cut.Render(p => p.Add(c => c.Conversation, conversation));
        } else {
            var cut = host.FindComponent<LlmChatFloatingHistoryDialog>();
            var action = cut.FindComponent<ConversationThreadHistoryList>().Instance.Selected;
            queued = () => cut.InvokeAsync(() => action.InvokeAsync(LlmChatConversationPresentationMapper.ToKey(conversation.ConversationId)));
            cut.Render(p => p.Add(c => c.Conversations, new[] { Conversation() }));
            cut.Render(p => p.Add(c => c.Conversations, new[] { conversation }));
        }
        await queued();
        Assert.False(first.IsCompleted);
        await dialogs.Dialogs.Single().CloseAsync();
        await first;
        var second = dialogs.OpenAsync<LlmChatFloatingHistoryDialog>("Second");
        await queued();
        Assert.False(second.IsCompleted);
        await dialogs.Dialogs.Single().CloseAsync();
        await second;
    }

    private static LlmChatConversationListItem Conversation() => new(
        Guid.NewGuid(), Guid.NewGuid(), 7, "Same title", "Definition",
        new LlmConversationProviderSnapshot(Guid.NewGuid(), "Pinned", ProviderKind.OpenAi, "model"),
        LlmChatConversationStatus.Active, LlmChatConversationOrigin.Application,
        4, 0, null, DateTimeOffset.UnixEpoch);
}
