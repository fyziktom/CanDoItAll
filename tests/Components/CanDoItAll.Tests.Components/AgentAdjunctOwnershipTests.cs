using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentAdjunctOwnershipTests {
    [Fact]
    public async Task Old_context_action_is_refused_after_A_B_A_and_after_binding_revision_changes() {
        using var context = Context();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var bindings = new AgentConversationContextService(TimeProvider.System);
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentConversationContextService>(bindings);
        var first = Chat();
        var second = Chat();
        var firstKey = AgentConversationKey.ForHandle(first.HandleId);
        var initial = bindings.GetOrCreateBinding(firstKey);
        var cut = context.Render<AgentFloatingConversationContent>(p => p.Add(c => c.Chat, first));
        var queued = cut.FindComponent<AgentConversationContextSurface>().FindComponent<Button>().Instance.Click;
        cut.Render(p => p.Add(c => c.Chat, second));
        cut.Render(p => p.Add(c => c.Chat, first));
        await cut.InvokeAsync(() => queued.InvokeAsync());
        Assert.Equal(initial, bindings.TryGetBinding(firstKey));
        var staleRevision = cut.FindComponent<AgentConversationContextSurface>().FindComponent<Button>().Instance.Click;
        await cut.InvokeAsync(() => bindings.Detach(firstKey));
        var detached = bindings.TryGetBinding(firstKey);
        await cut.InvokeAsync(() => staleRevision.InvokeAsync());
        Assert.Equal(detached, bindings.TryGetBinding(firstKey));
        await cut.Find("[data-testid='floating-agent-chat-affinity-toggle']").ClickAsync();
        Assert.Equal(AgentConversationContextMode.FollowCurrentSurface, bindings.TryGetBinding(firstKey)!.Mode);
        Assert.Null(bindings.TryGetBinding(AgentConversationKey.ForHandle(second.HandleId)));
    }

    [Fact]
    public async Task Close_choice_closes_its_own_dialog_while_a_second_dialog_remains_open() {
        using var context = Context();
        var host = context.Render<DialogHost>();
        var dialogs = context.Services.GetRequiredService<DialogService>();
        var first = Chat() with { RunState = ActiveAgentChatRunState.AwaitingApproval };
        var firstResult = dialogs.OpenAsync<FloatingAgentChatCloseDialog>("First", new Dictionary<string, object?> { [nameof(FloatingAgentChatCloseDialog.Chat)] = first });
        var secondResult = dialogs.OpenAsync<FloatingAgentChatCloseDialog>("Second", new Dictionary<string, object?> { [nameof(FloatingAgentChatCloseDialog.Chat)] = Chat() });
        host.WaitForAssertion(() => Assert.Equal(2, host.FindComponents<FloatingAgentChatCloseDialog>().Count));
        var firstCut = host.FindComponents<FloatingAgentChatCloseDialog>().Single(c => c.Instance.Chat.HandleId == first.HandleId);
        await firstCut.Find("[data-testid='floating-agent-chat-keep-active']").ClickAsync();
        Assert.Equal(FloatingAgentChatCloseDecision.KeepActive, await firstResult);
        Assert.False(secondResult.IsCompleted);
        Assert.Single(dialogs.Dialogs);
        await host.Find("[data-testid='floating-agent-chat-cancel-close']").ClickAsync();
        Assert.Equal(FloatingAgentChatCloseDecision.Cancel, await secondResult);
    }

    [Fact]
    public async Task Queued_close_decision_cannot_close_a_rebound_same_name_handle() {
        using var context = Context();
        var first = Chat();
        var cut = context.Render<FloatingAgentChatCloseDialog>(p => p.Add(c => c.Chat, first));
        var original = cut.FindComponent<AgentFloatingCloseSurface>().Instance.State;
        var queued = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Stop chat").Instance.Click;
        cut.Render(p => p.Add(c => c.Chat, Chat()));
        cut.Render(p => p.Add(c => c.Chat, first));
        Assert.NotEqual(original.Origin, cut.FindComponent<AgentFloatingCloseSurface>().Instance.State.Origin);
        await cut.InvokeAsync(() => queued.InvokeAsync());
        Assert.NotNull(cut.Find("[data-testid='floating-agent-chat-close-dialog']"));
    }

    [Fact]
    public async Task Stale_attachment_input_does_not_forward_files_to_a_new_native_selection() {
        using var context = Context();
        var selected = 0;
        var cut = context.Render<ChatWorkspacePanel>(p => p.Add(c => c.DraftPrompt, string.Empty)
            .Add(c => c.SelectionGeneration, 1).Add(c => c.AttachmentFilesSelected, _ => selected++));
        var old = cut.FindComponent<AgentImageAttachmentPicker>().FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().Instance.OnChange;
        cut.Render(p => p.Add(c => c.SelectionGeneration, 3));
        await cut.InvokeAsync(() => old.InvokeAsync(new Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs([])));
        Assert.Equal(0, selected);
    }

    private static ActiveAgentChat Chat() => new(new(Guid.NewGuid()), new(Guid.NewGuid(), "Same name", null, null), null,
        ActiveAgentChatVisibility.Visible, ActiveAgentChatRunState.Idle, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<AgentToolPolicyCatalog>();
        return context;
    }
}
