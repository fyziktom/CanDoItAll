using Bunit;
using System.Threading.Channels;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class LlmChatConversationWorkspaceTests
{
    private static readonly Guid ActiveDefinitionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DraftDefinitionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ConversationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SecondConversationId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Start_chat_picker_exposes_only_active_definitions_and_selects_the_pinned_revision()
    {
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([], null));
        conversations.CreatedView = CreateView(
            CreateConversation(title: "Pinned chat", definitionRevision: 7),
            []);
        using var context = CreateContext(conversations, new StubOperationGateway());

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-new']").Click();

        cut.WaitForElement($"[data-testid='llm-chat-start-definition-{ActiveDefinitionId:D}']");
        Assert.Empty(cut.FindAll($"[data-testid='llm-chat-start-definition-{DraftDefinitionId:D}']"));
        cut.Find($"[data-testid='llm-chat-start-definition-{ActiveDefinitionId:D}']").Click();
        cut.Find("[data-testid='llm-chat-start-title']").Change("Pinned chat");
        cut.Find("[data-testid='llm-chat-start-confirm']").Click();

        cut.WaitForAssertion(() => Assert.Equal(ActiveDefinitionId, conversations.CreatedDefinitionId));
        Assert.Equal("Pinned chat", conversations.CreatedTitle);
        Assert.Contains("Revision 7", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Thread_and_canonical_transcript_pages_are_loaded_by_cursor_without_rendering_system_messages()
    {
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new(
            [CreateConversation(), CreateConversation(SecondConversationId, "Second chat")],
            new(DateTimeOffset.Parse("2026-08-15T12:00:00Z"), new(SecondConversationId))));
        conversations.ListPages.Enqueue(new(
            [CreateConversation(Guid.Parse("55555555-5555-5555-5555-555555555555"), "Third chat")],
            null));
        conversations.TranscriptPages.Enqueue(CreateView(
            CreateConversation(),
            [
                CreateMessage(LlmMessageRole.System, "hidden system prompt"),
                CreateMessage(LlmMessageRole.User, "first visible turn")
            ],
            new(10)));
        conversations.TranscriptPages.Enqueue(CreateView(
            CreateConversation(),
            [CreateMessage(LlmMessageRole.Assistant, "paged assistant answer")],
            null));
        using var context = CreateContext(conversations, new StubOperationGateway());

        var cut = context.Render<LlmChatConversationWorkspace>();

        cut.WaitForAssertion(() => Assert.Contains("first visible turn", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("hidden system prompt", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='llm-chat-transcript-load-more']").Click();
        cut.WaitForAssertion(() => Assert.Contains("paged assistant answer", cut.Markup, StringComparison.Ordinal));
        cut.Find("[data-testid='llm-chat-thread-load-more']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, conversations.ListQueries.Count));
        Assert.NotNull(conversations.ListQueries[1].Cursor);
        Assert.NotNull(conversations.TranscriptQueries[1].Cursor);
        Assert.DoesNotContain("hidden system prompt", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_and_archive_submit_authoritative_concurrency_tokens()
    {
        var conversations = new StubConversationGateway();
        var conversation = CreateConversation(concurrencyToken: 11, transcriptRevision: 15);
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        conversations.RenamedView = CreateView(conversation with { Title = "Renamed chat", ConcurrencyToken = 12 }, []);
        conversations.ArchivedView = CreateView(
            conversation with { Title = "Renamed chat", Status = LlmChatConversationStatus.Archived, ConcurrencyToken = 13 },
            []);
        using var context = CreateContext(conversations, new StubOperationGateway());

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement($"[data-testid='llm-chat-rename-{ConversationId:D}']").Click();
        cut.Find("[data-testid='llm-chat-rename-title']").Change("Renamed chat");
        cut.Find("[data-testid='llm-chat-rename-confirm']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Renamed chat", conversations.RenamedTitle));
        Assert.Equal(11, conversations.RenameExpectedConcurrencyToken);
        Assert.Equal(15, conversations.RenameExpectedTranscriptRevision);

        cut.Find($"[data-testid='llm-chat-archive-{ConversationId:D}']").Click();
        var archiveDialog = cut.FindComponent<DangerActionDialog>();
        archiveDialog.Find("input").Input("Renamed chat");
        archiveDialog.FindAll("button").Last(button => button.TextContent.Contains("Archive", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(12, conversations.ArchiveExpectedConcurrencyToken));
        Assert.Contains("Archived", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Admission_retry_reuses_operation_identity_and_success_renders_one_pending_user_turn()
    {
        var conversations = new StubConversationGateway();
        var conversation = CreateConversation(transcriptRevision: 15);
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        var operations = new StubOperationGateway();
        operations.SendResults.Enqueue(LlmChatUiResult<LlmChatOperationView>.Failure(
            new LlmChatUiFailure(
                LlmChatUiFailureCodes.RequestFailed,
                "The Simple Chat request could not be completed.")));
        operations.SendResults.Enqueue(LlmChatUiResult<LlmChatOperationView>.Success(CreateOperationView()));
        using var context = CreateContext(conversations, operations);

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-prompt']").Change("Explain the result");
        cut.Find("[data-testid='llm-chat-send']").Click();
        cut.WaitForAssertion(() => Assert.Single(operations.Sends));
        cut.Find("[data-testid='llm-chat-send']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, operations.Sends.Count));
        Assert.NotEqual(Guid.Empty, operations.Sends[0].OperationId);
        Assert.Equal(operations.Sends[0].OperationId, operations.Sends[1].OperationId);
        Assert.All(operations.Sends, send => Assert.Equal(15, send.ExpectedTranscriptRevision));
        Assert.All(operations.Sends, send => Assert.Equal("Explain the result", send.Message));
        Assert.Single(cut.FindAll("[data-testid='conversation-message'][data-state='pending']"));
        Assert.DoesNotContain("Add context", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attachment", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("voice", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Oversized_gateway_pages_are_capped_before_rail_or_transcript_materialization()
    {
        var conversationItems = Enumerable.Range(0, 120)
            .Select(index => CreateConversation(
                Guid.NewGuid(),
                $"Bounded chat {index + 1}"))
            .ToArray();
        var messageItems = Enumerable.Range(0, 220)
            .Select(index => CreateMessage(LlmMessageRole.User, $"Bounded message {index + 1}"))
            .ToArray();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new(
            conversationItems,
            new(conversationItems[^1].UpdatedAtUtc, new(conversationItems[^1].ConversationId))));
        conversations.TranscriptPages.Enqueue(CreateView(
            conversationItems[0],
            messageItems,
            new(220)));
        using var context = CreateContext(conversations, new StubOperationGateway());

        var cut = context.Render<LlmChatConversationWorkspace>();

        cut.WaitForAssertion(() => Assert.Equal(96, cut.FindAll("[data-testid='agent-thread-card']").Count));
        Assert.Equal(200, cut.FindAll("[data-testid='conversation-message']").Count);
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-thread-load-more']"));
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-transcript-load-more']"));
    }

    [Fact]
    public void Slow_streaming_coalesces_deltas_and_terminal_evidence_refreshes_canonical_transcript()
    {
        var conversations = new StubConversationGateway();
        var conversation = CreateConversation(transcriptRevision: 15);
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        conversations.TranscriptPages.Enqueue(CreateView(
            conversation with { TranscriptRevision = 17, ActiveOperationId = null },
            [
                CreateMessage(LlmMessageRole.User, "Explain the result"),
                CreateMessage(LlmMessageRole.Assistant, "Slow answer")
            ]));
        var operations = new StubOperationGateway();
        operations.SendResults.Enqueue(LlmChatUiResult<LlmChatOperationView>.Success(CreateOperationView()));
        var eventSessions = new ControlledEventSessionGateway();
        using var context = CreateContext(conversations, operations, eventSessions);

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-prompt']").Change("Explain the result");
        cut.Find("[data-testid='llm-chat-send']").Click();
        cut.WaitForAssertion(() => Assert.Single(operations.Sends));
        var operationId = new LlmChatOperationId(operations.Sends[0].OperationId);
        eventSessions.Session.Publish(new(
            operationId.Value,
            LlmChatOperationStatus.Running,
            false,
            string.Empty,
            [
                new LlmChatOperationAttemptStartedEvent(
                    operationId,
                    1,
                    1,
                    "model-a",
                    LlmStreamingDeliveryMode.Incremental,
                    DateTimeOffset.UtcNow),
                new LlmChatOperationTextDeltaEvent(operationId, 2, 1, "Slow ", DateTimeOffset.UtcNow)
            ],
            1,
            2));
        eventSessions.Session.Publish(new(
            operationId.Value,
            LlmChatOperationStatus.Running,
            false,
            string.Empty,
            [new LlmChatOperationTextDeltaEvent(operationId, 3, 1, "answer", DateTimeOffset.UtcNow)],
            1,
            3));

        cut.WaitForAssertion(() =>
        {
            var streaming = cut.FindAll("[data-testid='conversation-message'][data-state='streaming']");
            Assert.Single(streaming);
            Assert.Contains("Slow answer", streaming[0].TextContent, StringComparison.Ordinal);
        });

        operations.Current = CreateOperationView() with
        {
            OperationId = operationId.Value,
            Status = LlmChatOperationStatus.Succeeded,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ResultingTranscriptRevision = 17,
            LastEventSequence = 4,
            AssistantText = "Slow answer"
        };
        eventSessions.Session.Publish(new(
            operationId.Value,
            LlmChatOperationStatus.Succeeded,
            true,
            string.Empty,
            [
                new LlmChatOperationStateChangedEvent(
                    operationId,
                    4,
                    LlmChatOperationStatus.Succeeded,
                    DateTimeOffset.UtcNow,
                    model: "model-a",
                    usage: new LlmUsage(2, 1))
            ],
            1,
            4));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='conversation-message'][data-state='streaming']"));
            Assert.Empty(cut.FindAll("[data-testid='conversation-message'][data-state='pending']"));
            Assert.Contains("Slow answer", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Component_remount_reopens_the_same_operation_without_cancelling_it()
    {
        var operationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var conversation = CreateConversation(activeOperationId: operationId);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        var operations = new StubOperationGateway
        {
            Current = CreateOperationView(operationId, LlmChatOperationStatus.Running)
        };
        var eventSessions = new ControlledEventSessionGateway();
        using var context = CreateContext(conversations, operations, eventSessions);

        var first = context.Render<LlmChatConversationWorkspace>();
        first.WaitForAssertion(() => Assert.Single(eventSessions.OpenedOperationIds));
        await first.Instance.DisposeAsync();
        first.Dispose();
        var second = context.Render<LlmChatConversationWorkspace>();

        second.WaitForAssertion(() => Assert.Equal(2, eventSessions.OpenedOperationIds.Count));
        Assert.All(eventSessions.OpenedOperationIds, opened => Assert.Equal(operationId, opened));
        Assert.Equal(0, operations.CancelCalls);
        Assert.True(eventSessions.Session.DisposeCount >= 1);
    }

    [Fact]
    public void Retention_gap_discards_partial_text_and_refreshes_canonical_state()
    {
        var operationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var conversation = CreateConversation(activeOperationId: operationId);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        conversations.TranscriptPages.Enqueue(CreateView(
            conversation,
            [CreateMessage(LlmMessageRole.User, "Canonical admitted turn")]));
        var operations = new StubOperationGateway
        {
            Current = CreateOperationView(operationId, LlmChatOperationStatus.Running)
        };
        var eventSessions = new ControlledEventSessionGateway();
        using var context = CreateContext(conversations, operations, eventSessions);

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForAssertion(() => Assert.Single(eventSessions.OpenedOperationIds));
        var typedOperationId = new LlmChatOperationId(operationId);
        eventSessions.Session.Publish(new(
            operationId,
            LlmChatOperationStatus.Running,
            false,
            string.Empty,
            [
                new LlmChatOperationAttemptStartedEvent(
                    typedOperationId,
                    1,
                    1,
                    "model-a",
                    LlmStreamingDeliveryMode.Incremental,
                    DateTimeOffset.UtcNow),
                new LlmChatOperationTextDeltaEvent(
                    typedOperationId,
                    2,
                    1,
                    "stale partial",
                    DateTimeOffset.UtcNow)
            ],
            1,
            2));
        cut.WaitForAssertion(() => Assert.Contains("stale partial", cut.Markup, StringComparison.Ordinal));

        eventSessions.Session.Publish(new(
            operationId,
            LlmChatOperationStatus.Running,
            false,
            string.Empty,
            [new LlmChatOperationTextDeltaEvent(typedOperationId, 5, 1, "ignored", DateTimeOffset.UtcNow)],
            5,
            5));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Canonical admitted turn", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("stale partial", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("ignored", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Explicit_cancel_invokes_cancel_while_component_disposal_does_not()
    {
        var operationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var conversation = CreateConversation(activeOperationId: operationId);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        var operations = new StubOperationGateway
        {
            Current = CreateOperationView(operationId, LlmChatOperationStatus.Running)
        };
        var eventSessions = new ControlledEventSessionGateway();
        using var context = CreateContext(conversations, operations, eventSessions);

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-operation-cancel']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, operations.CancelCalls));
        await cut.Instance.DisposeAsync();
        cut.Dispose();

        Assert.Equal(1, operations.CancelCalls);
        Assert.True(eventSessions.Session.DisposeCount >= 1);
    }

    [Fact]
    public void Abandon_is_hidden_until_reconcile_confirms_recovery_evidence()
    {
        var operationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var conversation = CreateConversation(activeOperationId: operationId);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        conversations.TranscriptPages.Enqueue(CreateView(
            conversation with { ActiveOperationId = null },
            [CreateMessage(LlmMessageRole.User, "Recovered canonical turn")]));
        var recovery = CreateOperationView(operationId, LlmChatOperationStatus.RecoveryRequired);
        var operations = new StubOperationGateway
        {
            Current = recovery,
            ReconcileResult = LlmChatUiResult<LlmChatOperationView>.Success(recovery),
            AbandonResult = LlmChatUiResult<LlmChatOperationView>.Success(recovery with
            {
                Status = LlmChatOperationStatus.Failed,
                CompletedAtUtc = DateTimeOffset.UtcNow
            })
        };
        using var context = CreateContext(conversations, operations);

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForElement("[data-testid='llm-chat-operation-reconcile']");
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-operation-abandon']"));
        cut.Find("[data-testid='llm-chat-operation-reconcile']").Click();

        cut.WaitForElement("[data-testid='llm-chat-operation-abandon']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, operations.AbandonCalls));
        Assert.Equal(1, operations.ReconcileCalls);
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-operation-abandon']"));
        Assert.Contains("Recovered canonical turn", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_lifetime_change_clears_old_projection_and_loads_new_profile_state()
    {
        var operationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var oldConversation = CreateConversation(activeOperationId: operationId);
        var newConversation = CreateConversation(SecondConversationId, "New profile chat");
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([oldConversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(oldConversation, []));
        conversations.ListPages.Enqueue(new([newConversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(newConversation, []));
        var operations = new StubOperationGateway
        {
            Current = CreateOperationView(operationId, LlmChatOperationStatus.Running)
        };
        var eventSessions = new ControlledEventSessionGateway();
        using var context = CreateContext(conversations, operations, eventSessions);

        var cut = context.Render<LlmChatConversationWorkspace>();
        cut.WaitForAssertion(() => Assert.Single(eventSessions.OpenedOperationIds));
        var typedOperationId = new LlmChatOperationId(operationId);
        eventSessions.Session.Publish(new(
            operationId,
            LlmChatOperationStatus.Running,
            false,
            string.Empty,
            [
                new LlmChatOperationAttemptStartedEvent(
                    typedOperationId,
                    1,
                    1,
                    "model-a",
                    LlmStreamingDeliveryMode.Incremental,
                    DateTimeOffset.UtcNow),
                new LlmChatOperationTextDeltaEvent(
                    typedOperationId,
                    2,
                    1,
                    "old profile partial",
                    DateTimeOffset.UtcNow)
            ],
            1,
            2));
        cut.WaitForAssertion(() => Assert.Contains("old profile partial", cut.Markup, StringComparison.Ordinal));

        eventSessions.Session.ChangeProfile();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New profile chat", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("old profile partial", cut.Markup, StringComparison.Ordinal);
        });
        Assert.Equal(0, operations.CancelCalls);
    }

    [Fact]
    public void Terminal_before_subscription_still_refreshes_the_canonical_transcript()
    {
        var operationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var conversation = CreateConversation(activeOperationId: operationId);
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([conversation], null));
        conversations.TranscriptPages.Enqueue(CreateView(conversation, []));
        conversations.TranscriptPages.Enqueue(CreateView(
            conversation with { ActiveOperationId = null, TranscriptRevision = 17 },
            [CreateMessage(LlmMessageRole.Assistant, "Already completed answer")]));
        var operations = new StubOperationGateway
        {
            Current = CreateOperationView(operationId, LlmChatOperationStatus.Succeeded) with
            {
                CompletedAtUtc = DateTimeOffset.UtcNow,
                ResultingTranscriptRevision = 17,
                LastEventSequence = 1,
                AssistantText = "Already completed answer"
            }
        };
        var eventSessions = new ControlledEventSessionGateway();
        var typedOperationId = new LlmChatOperationId(operationId);
        eventSessions.Session.Publish(new(
            operationId,
            LlmChatOperationStatus.Succeeded,
            true,
            string.Empty,
            [
                new LlmChatOperationStateChangedEvent(
                    typedOperationId,
                    1,
                    LlmChatOperationStatus.Succeeded,
                    DateTimeOffset.UtcNow,
                    model: "model-a",
                    usage: new LlmUsage(2, 1))
            ],
            1,
            1));
        using var context = CreateContext(conversations, operations, eventSessions);

        var cut = context.Render<LlmChatConversationWorkspace>();

        cut.WaitForAssertion(() => Assert.Contains("Already completed answer", cut.Markup, StringComparison.Ordinal));
        Assert.Single(eventSessions.OpenedOperationIds);
        Assert.Empty(cut.FindAll("[data-testid='conversation-message'][data-state='streaming']"));
    }

    private static BunitContext CreateContext(
        ILlmChatConversationUiGateway conversations,
        ILlmChatOperationUiGateway operations,
        ILlmChatUiEventSessionGateway? eventSessions = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddLogging();
        context.Services.AddSingleton<ILlmChatDefinitionUiGateway>(new StubDefinitionGateway());
        context.Services.AddSingleton(conversations);
        context.Services.AddSingleton(operations);
        context.Services.AddSingleton(eventSessions ?? new ControlledEventSessionGateway());
        context.Services.AddSingleton<ILlmChatOperationProjectionReducer, LlmChatOperationProjectionReducer>();
        context.Services.AddSingleton<ILlmChatUiAuthorizationFacade>(new StubAuthorization());
        return context;
    }

    private static LlmChatConversationListItem CreateConversation(
        Guid? conversationId = null,
        string title = "Primary chat",
        int definitionRevision = 3,
        long concurrencyToken = 11,
        long transcriptRevision = 15,
        Guid? activeOperationId = null)
        => new(
            conversationId ?? ConversationId,
            ActiveDefinitionId,
            definitionRevision,
            title,
            "Research assistant",
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Application,
            concurrencyToken,
            transcriptRevision,
            activeOperationId,
            DateTimeOffset.Parse("2026-08-16T12:00:00Z"));

    private static LlmChatConversationView CreateView(
        LlmChatConversationListItem conversation,
        IReadOnlyList<LlmChatMessageListItem> messages,
        LlmChatTranscriptCursor? nextCursor = null)
        => new(conversation, messages, nextCursor);

    private static LlmChatMessageListItem CreateMessage(LlmMessageRole role, string text)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            role,
            text,
            DateTimeOffset.Parse("2026-08-16T12:01:00Z"),
            role == LlmMessageRole.Assistant ? "model-a" : string.Empty);

    private static LlmChatOperationView CreateOperationView(
        Guid? operationId = null,
        LlmChatOperationStatus status = LlmChatOperationStatus.Pending)
        => new(
            operationId ?? Guid.Parse("66666666-6666-6666-6666-666666666666"),
            ConversationId,
            status,
            DateTimeOffset.Parse("2026-08-16T12:02:00Z"),
            null,
            null,
            0,
            string.Empty,
            string.Empty,
            null);

    private sealed class StubDefinitionGateway : ILlmChatDefinitionUiGateway
    {
        private static readonly LlmChatDefinitionListItem Active = new(
            ActiveDefinitionId,
            "Research assistant",
            "Summarizes research.",
            string.Empty,
            LlmChatDefinitionStatus.Active,
            7,
            3,
            DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
            ["research"]);

        private static readonly LlmChatDefinitionListItem Draft = Active with
        {
            DefinitionId = DraftDefinitionId,
            Name = "Draft assistant",
            Status = LlmChatDefinitionStatus.Draft
        };

        public Task<LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>> ListPageAsync(
            LlmChatDefinitionQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>.Success(
                new([Active, Draft], null)));

        public Task<LlmChatUiResult<LlmChatDefinitionListItem>> GetAsync(
            Guid definitionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LlmChatUiResult<LlmChatDefinitionListItem>.Success(Active));

        public Task<LlmChatUiResult<LlmChatDefinitionEditor>> GetEditorAsync(
            Guid definitionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LlmChatUiResult<LlmChatDefinitionEditor>> CreateAsync(
            LlmChatDefinitionMutation mutation,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LlmChatUiResult<LlmChatDefinitionEditor>> UpdateAsync(
            Guid definitionId,
            LlmChatDefinitionMutation mutation,
            long expectedConcurrencyToken,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LlmChatUiResult<LlmChatDefinitionListItem>> ChangeStatusAsync(
            Guid definitionId,
            LlmChatDefinitionStatus status,
            long expectedConcurrencyToken,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubConversationGateway : ILlmChatConversationUiGateway
    {
        public Queue<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>> ListPages { get; } = [];

        public Queue<LlmChatConversationView> TranscriptPages { get; } = [];

        public List<LlmChatConversationQuery> ListQueries { get; } = [];

        public List<LlmChatTranscriptQuery> TranscriptQueries { get; } = [];

        public LlmChatConversationView? CreatedView { get; set; }

        public LlmChatConversationView? RenamedView { get; set; }

        public LlmChatConversationView? ArchivedView { get; set; }

        public Guid? CreatedDefinitionId { get; private set; }

        public string? CreatedTitle { get; private set; }

        public string? RenamedTitle { get; private set; }

        public long? RenameExpectedConcurrencyToken { get; private set; }

        public long? RenameExpectedTranscriptRevision { get; private set; }

        public long? ArchiveExpectedConcurrencyToken { get; private set; }

        public Task<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>> ListPageAsync(
            LlmChatConversationQuery query,
            CancellationToken cancellationToken = default)
        {
            ListQueries.Add(query);
            return Task.FromResult(LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>.Success(
                ListPages.Dequeue()));
        }

        public Task<LlmChatUiResult<LlmChatConversationView>> GetAsync(
            Guid conversationId,
            LlmChatTranscriptQuery transcriptQuery,
            CancellationToken cancellationToken = default)
        {
            TranscriptQueries.Add(transcriptQuery);
            return Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(TranscriptPages.Dequeue()));
        }

        public Task<LlmChatUiResult<LlmChatConversationView>> CreateAsync(
            Guid definitionId,
            string title,
            CancellationToken cancellationToken = default)
        {
            CreatedDefinitionId = definitionId;
            CreatedTitle = title;
            return Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(CreatedView!));
        }

        public Task<LlmChatUiResult<LlmChatConversationView>> RenameAsync(
            Guid conversationId,
            string title,
            long expectedConcurrencyToken,
            long expectedTranscriptRevision,
            CancellationToken cancellationToken = default)
        {
            RenamedTitle = title;
            RenameExpectedConcurrencyToken = expectedConcurrencyToken;
            RenameExpectedTranscriptRevision = expectedTranscriptRevision;
            return Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(RenamedView!));
        }

        public Task<LlmChatUiResult<LlmChatConversationView>> ArchiveAsync(
            Guid conversationId,
            long expectedConcurrencyToken,
            CancellationToken cancellationToken = default)
        {
            ArchiveExpectedConcurrencyToken = expectedConcurrencyToken;
            return Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(ArchivedView!));
        }
    }

    private sealed class StubOperationGateway : ILlmChatOperationUiGateway
    {
        public Queue<LlmChatUiResult<LlmChatOperationView>> SendResults { get; } = [];

        public List<SendRequest> Sends { get; } = [];

        public LlmChatOperationView Current { get; set; } = CreateOperationView();

        public LlmChatUiResult<LlmChatOperationView>? ReconcileResult { get; set; }

        public LlmChatUiResult<LlmChatOperationView>? AbandonResult { get; set; }

        public int CancelCalls { get; private set; }

        public int ReconcileCalls { get; private set; }

        public int AbandonCalls { get; private set; }

        public Task<LlmChatUiResult<LlmChatOperationView>> SendAsync(
            Guid operationId,
            Guid conversationId,
            long expectedTranscriptRevision,
            string message,
            CancellationToken cancellationToken = default)
        {
            Sends.Add(new(operationId, conversationId, expectedTranscriptRevision, message));
            var result = SendResults.Dequeue();
            return Task.FromResult(result.IsSuccess
                ? LlmChatUiResult<LlmChatOperationView>.Success(result.Value! with { OperationId = operationId })
                : result);
        }

        public Task<LlmChatUiResult<LlmChatOperationView>> GetAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LlmChatUiResult<LlmChatOperationView>.Success(
                Current with { OperationId = operationId }));

        public Task<LlmChatUiResult<LlmChatOperationView>> CancelAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            CancelCalls++;
            Current = Current with
            {
                OperationId = operationId,
                Status = LlmChatOperationStatus.CancellationRequested
            };
            return Task.FromResult(LlmChatUiResult<LlmChatOperationView>.Success(Current));
        }

        public Task<LlmChatUiResult<LlmChatOperationView>> ReconcileAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ReconcileCalls++;
            return Task.FromResult(ReconcileResult ??
                LlmChatUiResult<LlmChatOperationView>.Success(Current with { OperationId = operationId }));
        }

        public Task<LlmChatUiResult<LlmChatOperationView>> AbandonAsync(
            Guid conversationId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            AbandonCalls++;
            return Task.FromResult(AbandonResult ??
                LlmChatUiResult<LlmChatOperationView>.Success(Current with { OperationId = operationId }));
        }

        public sealed record SendRequest(
            Guid OperationId,
            Guid ConversationId,
            long ExpectedTranscriptRevision,
            string Message);
    }

    private sealed class ControlledEventSessionGateway : ILlmChatUiEventSessionGateway
    {
        public ControlledEventSession Session { get; } = new();

        public List<Guid> OpenedOperationIds { get; } = [];

        public ValueTask<LlmChatUiResult<ILlmChatUiEventSession>> OpenAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            OpenedOperationIds.Add(operationId);
            return ValueTask.FromResult(LlmChatUiResult<ILlmChatUiEventSession>.Success(Session));
        }
    }

    private sealed class ControlledEventSession : ILlmChatUiEventSession
    {
        private readonly Channel<LlmChatUiOperationEventPage> pages = Channel.CreateUnbounded<LlmChatUiOperationEventPage>();
        private readonly CancellationTokenSource profileLifetime = new();

        public CancellationToken ProfileLifetime => profileLifetime.Token;

        public int MaximumPageSize => 50;

        public int DisposeCount { get; private set; }

        public void Publish(LlmChatUiOperationEventPage page)
            => pages.Writer.TryWrite(page);

        public void ChangeProfile()
            => profileLifetime.Cancel();

        public async ValueTask<LlmChatUiOperationEventPage> ReadAsync(
            long afterSequence,
            int take,
            TimeSpan maximumWait,
            CancellationToken cancellationToken = default)
            => await pages.Reader.ReadAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubAuthorization : ILlmChatUiAuthorizationFacade
    {
        public ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new LlmChatUiAuthorizationSnapshot(true, true, true));

        public ValueTask<bool> IsAllowedAsync(
            LlmChatUiPermission permission,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);
    }
}
