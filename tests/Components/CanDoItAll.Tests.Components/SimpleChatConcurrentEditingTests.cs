using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.Conversations.Components;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatDefinitionUiTests;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class SimpleChatConcurrentEditingTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Undo_comparison_restores_only_the_local_draft_and_requires_another_read_before_save(bool newerHint) {
        var original = CreateEditor(name: "Original name");
        var current = CreateEditor(name: "Peer name", concurrencyToken: 9);
        var gateway = new StubDefinitionGateway(original, current);
        var access = new ControlledAccess();
        using var context = Context(gateway, access);
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-summary']").Change("Local summary");
        await access.HintAsync();
        cut.Find("[data-testid='llm-chat-definition-editor-reload']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-review-apply']").Click();
        Assert.Equal("Peer name", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        if (newerHint) {
            var surface = cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance;
            var generation = surface.Presentation.Generation;
            await access.HintAsync();
            await cut.InvokeAsync(() => surface.Intent.InvokeAsync(new(generation, DefinitionEditorAction.UndoComparison)));
            Assert.Equal("Peer name", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        } else {
            cut.Find("[data-testid='llm-chat-definition-review-undo']").Click();
            Assert.Equal("Original name", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        }
        Assert.Equal("Local summary", cut.Find("[data-testid='llm-chat-definition-summary']").GetAttribute("value"));
        Assert.Equal("Peer name", cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance.Presentation.Definition!.Name);
        Assert.Equal(0, gateway.UpdateCalls);
        Assert.Equal(2, gateway.GetEditorCalls);
        Assert.True(cut.Find("[data-testid='llm-chat-definition-editor-save']").HasAttribute("disabled"));
    }

    [Fact]
    public void Wrong_identity_acknowledgement_preserves_the_local_draft_and_blocks_another_submission() {
        var original = CreateEditor();
        var wrong = CreateEditor(name: "Private wrong target", definitionId: Guid.NewGuid());
        var gateway = new StubDefinitionGateway(original) { SaveHandler = (_, _) => Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(wrong)) };
        using var context = Context(gateway, new ControlledAccess());
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-name']").Change("My retained draft");
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        Assert.True(cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance.Presentation.Unconfirmed);
        Assert.True(cut.Find("[data-testid='llm-chat-definition-editor-save']").HasAttribute("disabled"));
        Assert.Equal("My retained draft", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        Assert.DoesNotContain("Private wrong target", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, gateway.UpdateCalls);
        Assert.Equal(1, gateway.GetEditorCalls);
    }

    [Fact]
    public async Task Hint_keeps_unfinished_input_and_review_does_not_read_or_write_automatically() {
        var original = CreateEditor();
        var current = CreateEditor(name: "Peer name", concurrencyToken: 9) with { Temperature = 0.7 };
        var gateway = new StubDefinitionGateway(original, current);
        var access = new ControlledAccess();
        using var context = Context(gateway, access);
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-temperature']").Input("-");
        cut.Find("[data-testid='llm-chat-definition-tags-input']").Input("unfinished");
        var identity = cut.FindComponent<ConversationIdentityFields>().Instance;
        await access.HintAsync();
        Assert.Equal(1, gateway.GetEditorCalls);
        Assert.Null(gateway.UpdatedMutation);
        Assert.Equal("-", cut.Find("[data-testid='llm-chat-definition-temperature']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='llm-chat-definition-editor-save']").HasAttribute("disabled"));
        cut.Find("[data-testid='llm-chat-definition-editor-reload']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-comparison']");
        cut.Find("[data-testid='llm-chat-definition-review-temperature'] button").Click();
        cut.Find("[data-testid='llm-chat-definition-review-pending-tag'] button").Click();
        cut.Find("[data-testid='llm-chat-definition-review-apply']").Click();
        Assert.Same(identity, cut.FindComponent<ConversationIdentityFields>().Instance);
        Assert.Equal("unfinished", cut.Find("[data-testid='llm-chat-definition-tags-input']").GetAttribute("value"));
        Assert.Equal("-", cut.Find("[data-testid='llm-chat-definition-temperature']").GetAttribute("value"));
        Assert.Equal("Peer name", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        Assert.Equal(0, gateway.UpdateCalls);
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        Assert.Equal(0, gateway.UpdateCalls);
        cut.Find("[data-testid='llm-chat-definition-tags-input']").Input("");
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        Assert.Contains("Temperature", cut.Find("[data-testid='llm-chat-definition-editor-error']").TextContent, StringComparison.Ordinal);
        Assert.Equal(0, gateway.UpdateCalls);
        cut.Find("[data-testid='llm-chat-definition-temperature']").Input("0.4");
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        Assert.Equal(1, gateway.UpdateCalls);
        Assert.Equal(9, gateway.UpdateExpectedConcurrencyToken);
        Assert.Equal(0.4, gateway.UpdatedMutation!.Temperature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_save_keeps_later_input_and_the_exact_accepted_original(bool warning) {
        var original = CreateEditor();
        var accepted = CreateEditor(name: "Submitted", concurrencyToken: 8);
        var reply = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionEditor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubDefinitionGateway(original) { SaveHandler = (_, _) => reply.Task };
        using var context = Context(gateway, new ControlledAccess());
        var saved = 0;
        var closed = 0;
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId)
            .Add(x => x.Saved, _ => saved++).Add(x => x.Cancelled, () => closed++));
        cut.WaitForElement("[data-testid='llm-chat-definition-name']").Change("Submitted");
        var identity = cut.FindComponent<ConversationIdentityFields>().Instance;
        var save = cut.InvokeAsync(() => cut.Find("[data-testid='llm-chat-definition-editor-save']").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(1, gateway.UpdateCalls));
        Assert.False(cut.Find("[data-testid='llm-chat-definition-name']").HasAttribute("disabled"));
        cut.Find("[data-testid='llm-chat-definition-name']").Change("Later input");
        var surface = cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance;
        await cut.InvokeAsync(() => surface.Intent.InvokeAsync(new(surface.Presentation.Generation, DefinitionEditorAction.ConfirmDiscard)));
        var closedWhilePending = closed;
        var stillSaving = cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance.Presentation.IsSaving;
        reply.SetResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(accepted, warning ? "Saved, notification needs refresh." : null));
        await save;
        Assert.Equal(0, closedWhilePending);
        Assert.True(stillSaving);
        Assert.Equal(0, saved);
        Assert.Same(identity, cut.FindComponent<ConversationIdentityFields>().Instance);
        Assert.Equal("Later input", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        var presentation = cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance.Presentation;
        Assert.False(presentation.Unconfirmed);
        Assert.Equal(1, gateway.GetEditorCalls);
        Assert.Equal(1, gateway.UpdateCalls);
        gateway.SaveHandler = (_, _) => Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(CreateEditor(name: "Later input", concurrencyToken: 9)));
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        Assert.Equal(2, gateway.UpdateCalls);
        Assert.Equal(8, gateway.UpdateExpectedConcurrencyToken);
        Assert.Equal("Later input", gateway.UpdatedMutation!.Name);
    }

    [Fact]
    public void Unknown_review_requires_acknowledgement_and_a_separate_new_save() {
        var original = CreateEditor();
        var current = CreateEditor(name: "Peer name", concurrencyToken: 9);
        var gateway = new StubDefinitionGateway(original, current) { SaveHandler = (_, _) => throw new IOException("Private lost response.") };
        using var context = Context(gateway, new ControlledAccess());
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-name']").Change("My draft");
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-editor-reload']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-review-name'] button").Click();
        Assert.True(cut.Find("[data-testid='llm-chat-definition-review-apply']").HasAttribute("disabled"));
        cut.Find("[data-testid='llm-chat-definition-review-acknowledge']").Change(true);
        cut.Find("[data-testid='llm-chat-definition-review-apply']").Click();
        Assert.Equal(1, gateway.UpdateCalls);
        Assert.Contains("earlier save remains unconfirmed", cut.Markup, StringComparison.OrdinalIgnoreCase);
        gateway.SaveHandler = (_, _) => Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(CreateEditor(name: "My draft", concurrencyToken: 10)));
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        Assert.Equal(2, gateway.UpdateCalls);
        Assert.Equal(9, gateway.UpdateExpectedConcurrencyToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_access_refusal_hides_protected_content_and_never_starts_a_write(bool afterRead) {
        var original = CreateEditor();
        var gateway = new StubDefinitionGateway(original);
        var access = new ControlledAccess {
            Validate = (write, _) => afterRead ? gateway.GetEditorCalls == 0 ? ValueTask.CompletedTask : throw new UnauthorizedAccessException()
                : write ? throw new UnauthorizedAccessException() : ValueTask.CompletedTask
        };
        using var context = Context(gateway, access);
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId));
        if (!afterRead) {
            cut.WaitForElement("[data-testid='llm-chat-definition-editor-save']").Click();
        }
        cut.WaitForAssertion(() => Assert.Equal(DefinitionEditorPhase.Denied, cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance.Presentation.Phase));
        Assert.Equal(0, gateway.UpdateCalls);
        Assert.DoesNotContain("secret system prompt", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance.Presentation.Unconfirmed);
    }

    [Fact]
    public async Task Hint_or_input_during_current_read_invalidates_the_comparison_without_changing_the_draft() {
        var original = CreateEditor();
        var gateway = new StubDefinitionGateway(original);
        var access = new ControlledAccess();
        using var context = Context(gateway, access);
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-name']").Change("Draft");
        await access.HintAsync();
        var reply = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionEditor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.EditorHandler = _ => reply.Task;
        var read = cut.InvokeAsync(() => cut.Find("[data-testid='llm-chat-definition-editor-reload']").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, gateway.GetEditorCalls));
        cut.Find("[data-testid='llm-chat-definition-name']").Change("Later draft");
        await access.HintAsync();
        reply.SetResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(CreateEditor(name: "Peer", concurrencyToken: 9)));
        await read;
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-definition-comparison']"));
        Assert.Equal("Later draft", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        Assert.Equal(0, gateway.UpdateCalls);
        Assert.True(cut.Find("[data-testid='llm-chat-definition-editor-save']").HasAttribute("disabled"));
    }

    [Fact]
    public void Closing_an_unfinished_draft_requires_explicit_discard() {
        var original = CreateEditor();
        using var context = Context(new StubDefinitionGateway(original), new ControlledAccess());
        var closed = 0;
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, original.Definition.DefinitionId).Add(x => x.Cancelled, () => closed++));
        cut.WaitForElement("[data-testid='llm-chat-definition-name']").Change("Keep this draft");
        cut.Find("[data-testid='llm-chat-definition-editor-cancel']").Click();
        Assert.Equal(0, closed);
        Assert.Equal("Keep this draft", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        cut.Find("[data-testid='llm-chat-definition-discard']").Click();
        Assert.Equal(1, closed);
    }

    private static BunitContext Context(StubDefinitionGateway gateway, ControlledAccess access) {
        var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        context.Services.AddSingleton<ILlmChatDefinitionEditorAccess>(access);
        return context;
    }

    private sealed class ControlledAccess : ILlmChatDefinitionEditorAccess {
        public Task<LlmChatEditorOpening> OpenCatalogAsync(Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken)
            => throw new NotSupportedException("This fixture owns only the definition editor.");
        private Func<Task>? hint;
        public Func<bool, CancellationToken, ValueTask> Validate { get; init; } = (_, _) => ValueTask.CompletedTask;
        public Task HintAsync() => (hint ?? throw new InvalidOperationException("The editor has not opened."))();
        public Task<LlmChatEditorOpening> OpenAsync(Guid? target, Guid editorId, Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken) {
            hint = changed;
            var lifetime = new CancellationTokenSource();
            return Task.FromResult(new LlmChatEditorOpening(target, editorId, Validate, lifetime.Token, lifetime.Cancel, () => {
                lifetime.Dispose();
                return ValueTask.CompletedTask;
            }));
        }
    }
}
