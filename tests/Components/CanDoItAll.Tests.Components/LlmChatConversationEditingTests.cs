using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatConversationWorkspaceTests;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class LlmChatConversationEditingTests {
    [Fact]
    public void Undo_restores_local_title_over_the_reviewed_current_baseline_without_restoring_an_old_token() {
        var original = CreateConversation();
        var peer = original with { Title = "Peer title", ConcurrencyToken = 12, TranscriptRevision = 16 };
        var draft = new LlmChatConversationMetadataDraft(original);
        draft.SetTitle("Local title before review");
        draft.Compare(peer, draft.Revision);
        draft.Comparison!.UseCurrent();
        Assert.True(draft.Apply(false));

        draft.Undo();

        Assert.Same(peer, draft.Original);
        Assert.Equal("Local title before review", draft.Title);
        Assert.True(draft.NeedsReview);
        draft.Compare(peer, draft.Revision);
        Assert.Equal("Peer title", draft.Comparison!.Original);
        Assert.True(draft.Comparison.IsResolved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_focused_reload_covers_its_notice_without_acknowledging_an_unread_main_catalog(bool focused) {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(original with { Title = "Peer title", ConcurrencyToken = 12 }, []));
        using var context = CreateContext(conversations, new StubOperationGateway());
        var access = (StubConversationEditorAccess)context.Services.GetRequiredService<ILlmChatConversationEditorAccess>();
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>(parameters => parameters.Add(component => component.Focused, focused));
        await cut.WaitForElement("[data-testid='llm-chat-prompt']").InputAsync("Unsent input stays local");
        await cut.InvokeAsync(access.Openings.First(opening => opening.Target is null).Changed);
        var surface = cut.FindComponent<LlmChatConversationSurface>();
        Assert.True(surface.Instance.Presentation.UpdatesAvailable);

        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new(surface.Instance.Presentation.Generation,
            ConversationWorkspaceAction.ReloadSelected)));

        Assert.Equal(!focused, surface.Instance.Presentation.UpdatesAvailable);
        Assert.Equal("Unsent input stays local", cut.Find("[data-testid='llm-chat-prompt']").GetAttribute("value"));
        Assert.Equal("Peer title", cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        Assert.Single(conversations.ListQueries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_metadata_refusal_stays_visible_after_a_transcript_refresh(bool archive) {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(original with { TranscriptRevision = 16 }, []));
        var refusal = LlmChatUiResult<LlmChatConversationView>.Failure(new LlmChatUiFailure(
            CanDoItAll.AgentFramework.Llm.SimpleChats.Common.LlmChatErrorCodes.ActiveTurnConflict, "Native refusal"));
        conversations.Rename = _ => Task.FromResult(refusal);
        conversations.Archive = _ => Task.FromResult(refusal);
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-{(archive ? "archive" : "rename")}-{original.ConversationId:D}']").ClickAsync();
        var dialog = cut.FindComponent<LlmChatConversationDialogSurface>();
        var origin = dialog.Instance.Presentation.Origin;
        if (!archive) {
            await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Title retained after refusal");
        }
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(origin, ConversationDialogAction.Confirm)));
        var surface = cut.FindComponent<LlmChatConversationSurface>();
        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new(surface.Instance.Presentation.Generation,
            ConversationWorkspaceAction.ReloadSelected)));

        Assert.Contains("This conversation already has an active turn.", dialog.Markup, StringComparison.Ordinal);
        Assert.True(dialog.Instance.Presentation.NeedsReview);
        Assert.Equal(1, archive ? conversations.ArchiveCalls : conversations.RenameCalls);
    }

    [Fact]
    public async Task A_late_accepted_title_does_not_replace_a_newer_native_read_or_its_later_local_draft() {
        var original = CreateConversation();
        var accepted = original with { Title = "Accepted submitted title", ConcurrencyToken = 12, TranscriptRevision = 16 };
        var peer = original with { Title = "Later peer title", ConcurrencyToken = 13, TranscriptRevision = 17 };
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(peer, []));
        var reply = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>(TaskCreationOptions.RunContinuationsAsynchronously);
        conversations.Rename = _ => reply.Task;
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Accepted submitted title");
        var writing = cut.Find("[data-testid='llm-chat-rename-confirm']").ClickAsync();
        try {
            cut.WaitForAssertion(() => Assert.Equal(1, conversations.RenameCalls));
            await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Later local title");
            var surface = cut.FindComponent<LlmChatConversationSurface>();
            await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new(surface.Instance.Presentation.Generation, ConversationWorkspaceAction.ReloadSelected)));
        } finally {
            reply.TrySetResult(LlmChatUiResult<LlmChatConversationView>.Success(CreateView(accepted, [])));
            await writing;
        }

        Assert.Equal("Later peer title", cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        Assert.Equal("Later local title", cut.Find("[data-testid='llm-chat-rename-title']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='llm-chat-rename-confirm']").HasAttribute("disabled"));
        Assert.Equal(1, conversations.RenameCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submission_captures_the_clicked_message_or_title_before_awaiting_original_authority(bool metadata) {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.RenamedView = CreateView(original with { Title = "Submitted content", ConcurrencyToken = 12 }, []);
        var operations = new StubOperationGateway();
        operations.SendResults.Enqueue(LlmChatUiResult<LlmChatOperationView>.Success(CreateOperationView()));
        using var context = CreateContext(conversations, operations);
        var access = (StubConversationEditorAccess)context.Services.GetRequiredService<ILlmChatConversationEditorAccess>();
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-prompt']");
        if (metadata) {
            await cut.Find($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        }
        var field = metadata ? "[data-testid='llm-chat-rename-title']" : "[data-testid='llm-chat-prompt']";
        await cut.Find(field).InputAsync("Submitted content");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        access.NextValidation = async () => {
            entered.SetResult();
            await gate.Task;
        };
        var writing = cut.Find(metadata ? "[data-testid='llm-chat-rename-confirm']" : "[data-testid='llm-chat-send']").ClickAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cut.Find(field).InputAsync("Later local input");
        } finally {
            gate.TrySetResult();
            await writing;
        }

        Assert.Equal("Submitted content", metadata ? conversations.RenamedTitle : Assert.Single(operations.Sends).Message);
        Assert.Equal("Later local input", cut.Find(field).GetAttribute("value"));
    }

    [Fact]
    public async Task A_queued_composer_callback_cannot_copy_input_to_a_successor_conversation() {
        var original = CreateConversation();
        var other = CreateConversation(Guid.NewGuid(), "Other conversation");
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original, other], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(other, []));
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-prompt']");
        var queued = cut.FindComponent<ConversationComposer>().Instance.DraftPromptChanged;
        var surface = cut.FindComponent<LlmChatConversationSurface>();
        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new(surface.Instance.Presentation.Generation,
            ConversationWorkspaceAction.Select, other.ConversationId)));
        await cut.Find("[data-testid='llm-chat-prompt']").InputAsync("New conversation draft");

        await cut.InvokeAsync(() => queued.InvokeAsync("Retired private input"));

        Assert.Equal(other.Title, cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        Assert.Equal("New conversation draft", cut.Find("[data-testid='llm-chat-prompt']").GetAttribute("value"));
    }

    [Fact]
    public async Task A_queued_cancel_callback_cannot_cancel_a_new_operation_loaded_for_the_same_conversation() {
        var operationId = Guid.NewGuid();
        var nextOperationId = Guid.NewGuid();
        var original = CreateConversation(activeOperationId: operationId);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(original with { ActiveOperationId = nextOperationId }, []));
        var reads = new List<Guid>();
        var operations = new StubOperationGateway { Read = id => {
            reads.Add(id);
            return CreateOperationView(id, CanDoItAll.AgentFramework.Llm.SimpleChats.Operations.LlmChatOperationStatus.Running);
        } };
        using var context = CreateContext(conversations, operations);
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-operation-cancel']");
        var queued = cut.FindComponents<Button>().Single(component => component.Find("button").GetAttribute("data-testid") == "llm-chat-operation-cancel").Instance.Click;
        var surface = cut.FindComponent<LlmChatConversationSurface>();
        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new(surface.Instance.Presentation.Generation, ConversationWorkspaceAction.ReloadSelected)));
        cut.WaitForAssertion(() => Assert.Equal(nextOperationId, reads[^1]));

        await cut.InvokeAsync(() => queued.InvokeAsync());

        Assert.Empty(operations.CancelledIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Local_title_and_composer_input_do_not_await_authority_IO(bool metadata) {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        var operations = new StubOperationGateway();
        using var context = CreateContext(conversations, operations);
        var access = (StubConversationEditorAccess)context.Services.GetRequiredService<ILlmChatConversationEditorAccess>();
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-prompt']");
        if (metadata) {
            await cut.Find($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        }
        var before = access.Validations;
        foreach (var text in new[] { "r", "ra", "raw local text" }) {
            if (metadata) {
                await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync(text);
            } else {
                await cut.Find("[data-testid='llm-chat-prompt']").InputAsync(text);
            }
        }

        Assert.Equal(before, access.Validations);
        Assert.Equal("raw local text", cut.Find(metadata ? "[data-testid='llm-chat-rename-title']" : "[data-testid='llm-chat-prompt']").GetAttribute("value"));
        Assert.Single(conversations.TranscriptQueries);
        Assert.Equal(0, conversations.RenameCalls);
        Assert.Empty(operations.Sends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unconfirmed_metadata_with_unchanged_title_still_requires_explicit_discard(bool archive) {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        Task<LlmChatUiResult<LlmChatConversationView>> Unknown(CancellationToken _) => Task.FromException<LlmChatUiResult<LlmChatConversationView>>(new IOException("Unconfirmed metadata"));
        conversations.Rename = Unknown;
        conversations.Archive = Unknown;
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-{(archive ? "archive" : "rename")}-{original.ConversationId:D}']").ClickAsync();
        var dialog = cut.FindComponent<LlmChatConversationDialogSurface>();
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin, ConversationDialogAction.Confirm)));
        Assert.True(dialog.Instance.Presentation.RequiresAcknowledgement);

        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin, ConversationDialogAction.Close)));

        Assert.True(dialog.Instance.Presentation.ConfirmDiscard);
        Assert.Equal(original.Title, dialog.Instance.Presentation.Title);
        Assert.Equal(1, conversations.RenameCalls + conversations.ArchiveCalls);
        await cut.Find("[data-testid='llm-chat-metadata-discard']").ClickAsync();
        Assert.Equal(ConversationDialogKind.None, dialog.Instance.Presentation.Origin.Kind);
        Assert.Equal(1, conversations.RenameCalls + conversations.ArchiveCalls);
    }

    [Fact]
    public async Task Reviewing_an_archived_peer_shows_its_lifecycle_and_never_enables_title_replacement() {
        var original = CreateConversation();
        var archived = original with { Status = CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations.LlmChatConversationStatus.Archived, ConcurrencyToken = 12 };
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(archived, []));
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Retained local title");
        await cut.Find("[data-testid='llm-chat-metadata-compare']").ClickAsync();
        Assert.Contains("Archived", cut.Find("[data-testid='llm-chat-metadata-state']").TextContent, StringComparison.Ordinal);
        await cut.Find("[data-testid='llm-chat-metadata-apply']").ClickAsync();

        Assert.Equal("Retained local title", cut.Find("[data-testid='llm-chat-rename-title']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='llm-chat-rename-confirm']").HasAttribute("disabled"));
        Assert.Equal(0, conversations.RenameCalls);
        Assert.Contains("Archived", cut.Find("[data-testid='llm-chat-metadata-state']").TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_foreign_comparison_or_acceptance_never_retargets_the_metadata_draft(bool write) {
        var original = CreateConversation();
        var foreign = CreateView(CreateConversation(Guid.NewGuid(), "Private foreign title"), []);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(foreign);
        conversations.RenamedView = foreign;
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Retained local title");
        await cut.Find(write ? "[data-testid='llm-chat-rename-confirm']" : "[data-testid='llm-chat-metadata-compare']").ClickAsync();

        Assert.Equal("Retained local title", cut.Find("[data-testid='llm-chat-rename-title']").GetAttribute("value"));
        Assert.Equal(original.Title, cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        Assert.DoesNotContain("Private foreign title", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='llm-chat-rename-confirm']").HasAttribute("disabled"));
        Assert.Equal(write ? 1 : 0, conversations.RenameCalls);
    }

    [Fact]
    public async Task A_second_peer_hint_retires_an_in_flight_title_comparison_without_discarding_input() {
        var original = CreateConversation();
        var reply = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.Read = (_, _, _) => conversations.TranscriptQueries.Count == 1
            ? Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(CreateView(original, []))) : reply.Task;
        using var context = CreateContext(conversations, new StubOperationGateway());
        var access = (StubConversationEditorAccess)context.Services.GetRequiredService<ILlmChatConversationEditorAccess>();
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Retained local title");
        var reading = cut.Find("[data-testid='llm-chat-metadata-compare']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(2, conversations.TranscriptQueries.Count));
        await cut.InvokeAsync(access.Openings[^1].Changed);
        reply.SetResult(LlmChatUiResult<LlmChatConversationView>.Success(CreateView(original with { Title = "Stale review title", ConcurrencyToken = 12 }, [])));
        await reading;

        Assert.Null(cut.FindComponent<LlmChatConversationDialogSurface>().Instance.Presentation.TitleComparison);
        Assert.Equal("Retained local title", cut.Find("[data-testid='llm-chat-rename-title']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='llm-chat-rename-confirm']").HasAttribute("disabled"));
        Assert.Equal(0, conversations.RenameCalls);
    }

    [Fact]
    public async Task Unknown_metadata_result_requires_explicit_current_review_acknowledgement_before_a_new_command() {
        var original = CreateConversation();
        var current = original with { Title = "Local title", ConcurrencyToken = 12, TranscriptRevision = 16 };
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(current, []));
        conversations.Rename = _ => conversations.RenameCalls == 1
            ? Task.FromException<LlmChatUiResult<LlmChatConversationView>>(new IOException("Unconfirmed transport outcome"))
            : Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(CreateView(current with { ConcurrencyToken = 13 }, [])));
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Local title");
        await cut.Find("[data-testid='llm-chat-rename-confirm']").ClickAsync();
        var dialog = cut.FindComponent<LlmChatConversationDialogSurface>();
        Assert.True(dialog.Instance.Presentation.RequiresAcknowledgement);
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin, ConversationDialogAction.Confirm)));
        Assert.Equal(1, conversations.RenameCalls);
        await cut.Find("[data-testid='llm-chat-metadata-compare']").ClickAsync();
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin, ConversationDialogAction.ApplyComparison)));
        Assert.True(cut.Find("[data-testid='llm-chat-rename-confirm']").HasAttribute("disabled"));
        Assert.Equal(1, conversations.RenameCalls);
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin,
            ConversationDialogAction.ApplyComparison, Acknowledged: true)));
        Assert.Equal(1, conversations.RenameCalls);
        await cut.Find("[data-testid='llm-chat-rename-confirm']").ClickAsync();

        Assert.Equal(2, conversations.RenameCalls);
        Assert.Equal(12, conversations.RenameExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Unknown_create_keeps_the_opening_and_refuses_another_create_until_explicit_discard() {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.Create = _ => Task.FromException<LlmChatUiResult<LlmChatConversationView>>(new IOException("Unconfirmed creation"));
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement("[data-testid='llm-chat-new']").ClickAsync();
        var dialog = cut.FindComponent<LlmChatConversationDialogSurface>();
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin,
            ConversationDialogAction.SelectDefinition, DefinitionId: original.DefinitionId)));
        await cut.Find("[data-testid='llm-chat-start-confirm']").ClickAsync();
        Assert.True(dialog.Instance.Presentation.IsUnconfirmedCreate);
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin, ConversationDialogAction.Confirm)));
        Assert.Equal(1, conversations.CreateCalls);
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin, ConversationDialogAction.Close)));
        Assert.Single(cut.FindAll("[data-testid='llm-chat-start-title']"));
        await cut.Find("[data-testid='llm-chat-metadata-discard']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-start-title']"));
        Assert.Equal(1, conversations.CreateCalls);
    }

    [Fact]
    public async Task Accepted_title_preserves_a_later_title_edit_in_the_same_opening() {
        var original = CreateConversation();
        var reply = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.Rename = _ => reply.Task;
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Submitted title");
        var writing = cut.Find("[data-testid='llm-chat-rename-confirm']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(1, conversations.RenameCalls));
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Later title draft");
        reply.SetResult(LlmChatUiResult<LlmChatConversationView>.Success(CreateView(original with { Title = "Submitted title", ConcurrencyToken = 12 }, [])));
        await writing;

        Assert.Equal("Submitted title", cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        Assert.Equal("Later title draft", cut.Find("[data-testid='llm-chat-rename-title']").GetAttribute("value"));
        Assert.False(cut.Find("[data-testid='llm-chat-rename-confirm']").HasAttribute("disabled"));
        Assert.Equal(1, conversations.RenameCalls);
    }

    [Fact]
    public async Task Original_actor_change_hides_protected_drafts_and_prevents_metadata_dispatch() {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        using var context = CreateContext(conversations, new StubOperationGateway());
        var access = (StubConversationEditorAccess)context.Services.GetRequiredService<ILlmChatConversationEditorAccess>();
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement("[data-testid='llm-chat-prompt']").InputAsync("Private unsent message");
        await cut.Find($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Private title draft");
        access.Epoch++;
        var dialog = cut.FindComponent<LlmChatConversationDialogSurface>();
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin, ConversationDialogAction.Confirm)));

        Assert.Equal(0, conversations.RenameCalls);
        Assert.DoesNotContain("Private unsent message", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Private title draft", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(original.Title, cut.Markup, StringComparison.Ordinal);
        Assert.Single(conversations.TranscriptQueries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selecting_a_thread_preserves_the_composer_until_explicit_discard(bool sameTarget) {
        var original = CreateConversation();
        var other = CreateConversation(Guid.NewGuid(), "Other conversation");
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original, other], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(other, []));
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement("[data-testid='llm-chat-prompt']").InputAsync("Unsent original message");
        var surface = cut.FindComponent<LlmChatConversationSurface>();
        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new(surface.Instance.Presentation.Generation,
            ConversationWorkspaceAction.Select, sameTarget ? original.ConversationId : other.ConversationId)));

        Assert.Equal("Unsent original message", cut.Find("[data-testid='llm-chat-prompt']").GetAttribute("value"));
        Assert.Equal(original.Title, cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        Assert.Single(conversations.TranscriptQueries);
        if (!sameTarget) {
            Assert.Single(cut.FindAll("[data-testid='llm-chat-draft-discard']"));
        }
    }

    [Fact]
    public async Task Metadata_comparison_is_local_and_preserves_an_unsent_message() {
        var original = CreateConversation();
        var peer = original with { Title = "Peer title", ConcurrencyToken = 12, TranscriptRevision = 16 };
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(peer, []));
        conversations.RenamedView = CreateView(peer with { Title = "Local title", ConcurrencyToken = 13 }, []);
        using var context = CreateContext(conversations, new StubOperationGateway());
        var access = (StubConversationEditorAccess)context.Services.GetRequiredService<ILlmChatConversationEditorAccess>();
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = context.Render<LlmChatConversationWorkspace>();
        await cut.WaitForElement("[data-testid='llm-chat-prompt']").InputAsync("Unsent message");
        await cut.Find($"[data-testid='llm-chat-rename-{original.ConversationId:D}']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-rename-title']").InputAsync("Local title");
        await cut.InvokeAsync(access.Openings[^1].Changed);
        Assert.Null(conversations.RenamedTitle);
        Assert.Single(conversations.TranscriptQueries);
        await cut.Find("[data-testid='llm-chat-metadata-compare']").ClickAsync();
        var dialog = cut.FindComponent<LlmChatConversationDialogSurface>();
        var comparison = Assert.IsType<CanDoItAll.Editing.MergeField<string>>(dialog.Instance.Presentation.TitleComparison);
        Assert.Equal(original.Title, comparison.Original);
        Assert.Equal("Local title", comparison.Local);
        Assert.Equal("Peer title", comparison.Current);
        comparison.UseLocal();
        await cut.InvokeAsync(() => dialog.Instance.Intent.InvokeAsync(new(dialog.Instance.Presentation.Origin,
            ConversationDialogAction.ApplyComparison)));
        Assert.Null(conversations.RenamedTitle);
        Assert.Equal("Local title", cut.Find("[data-testid='llm-chat-rename-title']").GetAttribute("value"));
        await cut.Find("[data-testid='llm-chat-rename-confirm']").ClickAsync();

        Assert.Equal(12, conversations.RenameExpectedConcurrencyToken);
        Assert.Equal(16, conversations.RenameExpectedTranscriptRevision);
        Assert.Equal("Unsent message", cut.Find("[data-testid='llm-chat-prompt']").GetAttribute("value"));
    }
}
