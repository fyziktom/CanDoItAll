using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatDefinitionUiTests;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class DefinitionEditorSessionTests {
    [Fact]
    public async Task Target_replacement_accepts_B_while_A_read_is_pending_and_ignores_late_A() {
        var a = CreateEditor(name: "Editor A", definitionId: Guid.NewGuid());
        var b = CreateEditor(name: "Editor B", definitionId: Guid.NewGuid());
        var pending = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionEditor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubDefinitionGateway(a) {
            EditorHandler = id => id == a.Definition.DefinitionId
                ? pending.Task
                : Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(b))
        };
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, a.Definition.DefinitionId));
        cut.WaitForAssertion(() => Assert.Contains(a.Definition.DefinitionId, gateway.EditorTargets));
        cut.Render(p => p.Add(x => x.DefinitionId, b.Definition.DefinitionId));
        try {
            cut.WaitForAssertion(() => Assert.Contains(b.Definition.DefinitionId, gateway.EditorTargets));
            Assert.Equal("Editor B", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
        } finally {
            pending.TrySetResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(a));
            await pending.Task;
        }
        cut.WaitForAssertion(() => Assert.Equal("Editor B", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value")));
    }

    [Fact]
    public async Task New_target_clears_existing_revision_and_same_target_echo_does_not_reload() {
        var editor = CreateEditor();
        var gateway = new StubDefinitionGateway(editor);
        using var session = CreateSession(gateway);
        await session.SetTargetAsync(editor.Definition.DefinitionId);
        var source = session.Presentation.Source;
        await session.SetTargetAsync(editor.Definition.DefinitionId);
        Assert.Same(source, session.Presentation.Source);
        Assert.Equal(1, gateway.GetEditorCalls);
        await session.SetTargetAsync(null);
        Assert.Null(session.Presentation.Definition);
        Assert.Equal("", session.Presentation.Source!.Name);
        await session.SetTargetAsync(editor.Definition.DefinitionId);
        Assert.Equal(editor.Definition.Name, session.Presentation.Source!.Name);
        Assert.Equal(2, gateway.GetEditorCalls);
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("editor")]
    [InlineData("providers")]
    public async Task Disposal_cancels_owned_read_keeps_token_usable_and_publishes_nothing_late(string lane) {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var editor = CreateEditor();
        CancellationToken token = default;
        async Task Wait(CancellationToken cancellationToken) {
            token = cancellationToken;
            await release.Task;
        }
        var access = new StubAuthorization(true, true) {
            Read = async cancellationToken => {
                if (lane == "authorization") {
                    await Wait(cancellationToken);
                }
                return new(true, true, false);
            }
        };
        var gateway = new StubDefinitionGateway(editor) {
            EditorRead = async (_, cancellationToken) => {
                if (lane == "editor") {
                    await Wait(cancellationToken);
                }
                return LlmChatUiResult<LlmChatDefinitionEditor>.Success(editor);
            }
        };
        var providers = new ControlledProviders(async cancellationToken => {
            if (lane == "providers") {
                await Wait(cancellationToken);
            }
            return LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>.Success([]);
        });
        using var session = CreateSession(gateway, providers, access);
        var publications = 0;
        session.Changed += () => publications++;
        var load = session.SetTargetAsync(editor.Definition.DefinitionId);
        Assert.True(token.CanBeCanceled);
        session.Dispose();
        var before = publications;
        Assert.True(token.IsCancellationRequested);
        using var registration = token.Register(() => { });
        release.SetResult();
        await load;
        Assert.Equal(before, publications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_failure_is_partial_and_never_exposes_gateway_or_exception_text(bool throws) {
        var editor = CreateEditor();
        var providers = new ControlledProviders(_ => throws
            ? Task.FromException<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>>(new InvalidOperationException("private-provider-secret"))
            : Task.FromResult(LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>.Failure(new LlmChatUiFailure("untrusted", "private-provider-secret"))));
        using var session = CreateSession(new StubDefinitionGateway(editor), providers);
        await session.SetTargetAsync(editor.Definition.DefinitionId);
        Assert.Equal(DefinitionEditorPhase.Ready, session.Presentation.Phase);
        Assert.Equal(editor.SystemPrompt, session.Presentation.Source!.SystemPrompt);
        Assert.True(session.Presentation.ProvidersUnavailable);
        Assert.DoesNotContain("private-provider-secret", session.Presentation.ProviderError);
    }

    [Fact]
    public async Task Old_provider_completion_cannot_replace_new_target_options_or_loading() {
        var a = CreateEditor(definitionId: Guid.NewGuid());
        var b = CreateEditor(definitionId: Guid.NewGuid(), name: "B");
        var pending = new TaskCompletionSource<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var providers = new ControlledProviders(_ => ++calls == 1 ? pending.Task : new StubProviderGateway().ListAsync());
        using var session = CreateSession(new StubDefinitionGateway(a, b), providers);
        var first = session.SetTargetAsync(a.Definition.DefinitionId);
        await session.SetTargetAsync(b.Definition.DefinitionId);
        var accepted = session.Presentation;
        pending.SetResult(LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>.Failure(new LlmChatUiFailure("untrusted", "old error")));
        await first;
        Assert.Same(accepted, session.Presentation);
        Assert.False(session.Presentation.ProvidersUnavailable);
    }

    [Fact]
    public async Task Conflict_reload_stays_on_its_target_and_cannot_replace_a_newer_editor() {
        var a = CreateEditor(definitionId: Guid.NewGuid());
        var b = CreateEditor(name: "B", definitionId: Guid.NewGuid());
        var gateway = new StubDefinitionGateway(a) { UpdateFailure = new(LlmChatErrorCodes.DefinitionConcurrencyConflict, "private conflict") };
        using var session = CreateSession(gateway);
        await session.SetTargetAsync(a.Definition.DefinitionId);
        await session.SubmitAsync(new(session.Presentation.Generation, DefinitionEditorAction.Save, session.Presentation.Source));
        Assert.Equal(DefinitionEditorFailure.Conflict, session.Presentation.Failure);
        var pending = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionEditor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.EditorHandler = id => id == a.Definition.DefinitionId ? pending.Task : Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(b));
        var reload = session.ReloadAsync(session.Presentation.Generation);
        Assert.Equal(a.Definition.DefinitionId, gateway.EditorTargets[^1]);
        await session.SetTargetAsync(b.Definition.DefinitionId);
        pending.SetResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(a));
        await reload;
        Assert.Equal("B", session.Presentation.Source!.Name);
        Assert.Null(session.Presentation.Failure);
    }

    [Fact]
    public async Task Save_captures_independent_values_and_obsolete_completion_never_invokes_host_callback() {
        var a = CreateEditor(definitionId: Guid.NewGuid());
        var b = CreateEditor(name: "B", definitionId: Guid.NewGuid());
        var pending = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionEditor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubDefinitionGateway(a, b) { SaveHandler = (_, _) => pending.Task };
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var saved = new List<LlmChatDefinitionListItem>();
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.DefinitionId, a.Definition.DefinitionId).Add(x => x.Saved, value => saved.Add(value)));
        var surface = cut.FindComponent<LlmChatDefinitionEditorSurface>();
        var values = surface.Instance.Presentation.Source! with { Name = "Captured name" };
        var save = cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new(surface.Instance.Presentation.Generation, DefinitionEditorAction.Save, values)));
        cut.WaitForAssertion(() => Assert.NotNull(gateway.UpdatedMutation));
        values = values with { Name = "Later draft" };
        Assert.Equal("Captured name", gateway.UpdatedMutation!.Name);
        cut.Render(p => p.Add(x => x.DefinitionId, b.Definition.DefinitionId));
        pending.SetResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(a));
        await save;
        Assert.Empty(saved);
        Assert.Equal("B", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
    }

    [Fact]
    public async Task Obsolete_status_completion_and_old_finally_cannot_clear_new_mutation() {
        var a = CreateEditor(definitionId: Guid.NewGuid());
        var b = CreateEditor(name: "B", definitionId: Guid.NewGuid());
        var status = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionEditor>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubDefinitionGateway(a, b) { StatusHandler = _ => status.Task, SaveHandler = (_, _) => save.Task };
        using var session = CreateSession(gateway);
        await session.SetTargetAsync(a.Definition.DefinitionId);
        var old = session.SubmitAsync(new(session.Presentation.Generation, DefinitionEditorAction.ChangeStatus, Status: LlmChatDefinitionStatusFilter.Active));
        await session.SetTargetAsync(b.Definition.DefinitionId);
        var current = session.SubmitAsync(new(session.Presentation.Generation, DefinitionEditorAction.Save, session.Presentation.Source));
        status.SetResult(LlmChatUiResult<LlmChatDefinitionListItem>.Success(a.Definition));
        Assert.Null(await old);
        Assert.True(session.Presentation.IsSaving);
        save.SetResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(b));
        Assert.Equal(b.Definition.DefinitionId, (await current)!.DefinitionId);
        Assert.False(session.Presentation.IsSaving);
        Assert.Null(await session.SubmitAsync(new(session.Presentation.Generation, DefinitionEditorAction.Save, session.Presentation.Source)));
    }

    [Fact]
    public async Task Invalid_target_denial_and_core_read_failure_are_fail_closed() {
        var editor = CreateEditor();
        var gateway = new StubDefinitionGateway(editor);
        using var invalid = CreateSession(gateway);
        await invalid.SetTargetAsync(Guid.Empty);
        Assert.Equal(DefinitionEditorFailure.InvalidTarget, invalid.Presentation.Failure);
        Assert.Equal(0, gateway.GetEditorCalls);
        using var denied = CreateSession(gateway, access: new StubAuthorization(true, false));
        await denied.SetTargetAsync(editor.Definition.DefinitionId);
        Assert.Equal(DefinitionEditorPhase.Denied, denied.Presentation.Phase);
        Assert.Equal(0, gateway.GetEditorCalls);
        gateway.EditorHandler = _ => Task.FromException<LlmChatUiResult<LlmChatDefinitionEditor>>(new InvalidOperationException("private details"));
        using var failed = CreateSession(gateway);
        await failed.SetTargetAsync(editor.Definition.DefinitionId);
        Assert.Equal(DefinitionEditorPhase.Failed, failed.Presentation.Phase);
        Assert.Null(failed.Presentation.Source);
        Assert.DoesNotContain("private details", failed.Presentation.Error);
    }

    [Fact]
    public async Task Explicit_cancel_publishes_once_and_disposal_does_not_invoke_cancel_callback() {
        using var context = CreateContext(new StubDefinitionGateway(CreateEditor()), new StubProviderGateway(), new StubAuthorization(true, true));
        var cancellations = 0;
        var cut = context.Render<LlmChatDefinitionEditorDialog>(p => p.Add(x => x.Cancelled, () => cancellations++));
        var surface = cut.FindComponent<LlmChatDefinitionEditorSurface>().Instance;
        var intent = new DefinitionEditorIntent(surface.Presentation.Generation, DefinitionEditorAction.Cancel);
        await cut.InvokeAsync(() => surface.Intent.InvokeAsync(intent));
        await cut.InvokeAsync(() => surface.Intent.InvokeAsync(intent));
        Assert.Equal(1, cancellations);
        await context.DisposeRenderedComponentsAsync();
        Assert.Equal(1, cancellations);
    }

    private static LlmChatDefinitionEditorSession CreateSession(ILlmChatDefinitionUiGateway gateway,
        ILlmChatProviderUiGateway? providers = null, ILlmChatUiAuthorizationFacade? access = null)
        => new(gateway, providers ?? new StubProviderGateway(), access ?? new StubAuthorization(true, true), NullLogger<LlmChatDefinitionEditorSession>.Instance);

    private sealed class ControlledProviders(Func<CancellationToken, Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>>> read) : ILlmChatProviderUiGateway {
        public Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>> ListAsync(CancellationToken cancellationToken = default) => read(cancellationToken);
    }
}
