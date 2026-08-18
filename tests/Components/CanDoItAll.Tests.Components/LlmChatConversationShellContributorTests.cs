using Bunit;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class LlmChatConversationShellContributorTests
{
    [Fact]
    public async Task Create_hide_and_reopen_use_durable_conversation_identity_without_ambient_context()
    {
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var definition = new LlmChatDefinitionListItem(
            definitionId,
            "Research assistant",
            "Summarizes research.",
            string.Empty,
            LlmChatDefinitionStatus.Active,
            7,
            3,
            DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            ["research"]);
        var createdConversation = new LlmChatConversationListItem(
            conversationId,
            definitionId,
            7,
            "Research assistant chat",
            definition.Name,
            new LlmConversationProviderSnapshot(
                Guid.NewGuid(),
                "Pinned OpenAI",
                ProviderKind.OpenAi,
                "gpt-5.4-mini"),
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Application,
            4,
            0,
            null,
            DateTimeOffset.Parse("2026-08-17T12:01:00Z"));
        var definitions = new StubDefinitionGateway(definition);
        var conversations = new StubConversationGateway(createdConversation);
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddConversationShell();
        var shell = context.Services.GetRequiredService<IConversationShellCoordinator>();
        var catalogInvalidator = new LlmChatDefinitionCatalogInvalidationHub();
        await using var contributor = new LlmChatConversationShellContributor(
            definitions,
            conversations,
            new AllowAllAuthorization(),
            catalogInvalidator,
            shell,
            context.Services.GetRequiredService<DialogService>(),
            context.Services.GetRequiredService<NotificationService>(),
            NullLogger<LlmChatConversationShellContributor>.Instance);

        await contributor.InitializeAsync();

        var initial = contributor.Snapshot();
        var available = Assert.Single(initial.Available);
        Assert.Equal(ConversationParticipantKind.Chat, available.Kind);
        Assert.Contains(initial.StatusBadges, badge => badge.Text == "No application context");
        Assert.Empty(initial.Windows);

        var createAction = available.Presentation.Actions.Single(action => action.Key.Value == "new-chat");
        await contributor.HandleParticipantActionAsync(
            new(available.Presentation.Participant.Key, createAction.Key));

        Assert.Equal(definitionId, conversations.CreatedDefinitionId);
        var open = contributor.Snapshot();
        var window = Assert.Single(open.Windows);
        Assert.Equal(conversationId, window.Parameters[nameof(LlmChatFloatingConversationContent.ConversationId)]);
        Assert.Single(window.Parameters);
        Assert.Equal(
            new ConversationShellWindowKey(
                LlmChatConversationShellContributor.SourceIdentifier,
                LlmChatConversationShellContributor.BuildWindowId(conversationId)),
            shell.Snapshot().FocusedWindow);

        await contributor.HandleWindowCloseAsync(window.Key.WindowId);

        var hidden = contributor.Snapshot();
        var active = Assert.Single(hidden.Active);
        Assert.Empty(hidden.Windows);
        Assert.Null(shell.Snapshot().FocusedWindow);

        var openAction = active.Presentation.Actions.Single(action => action.Key.Value == "open");
        await contributor.HandleActiveActionAsync(new(active.Presentation.Key, openAction.Key));

        Assert.Single(contributor.Snapshot().Windows);
        Assert.NotNull(shell.Snapshot().FocusedWindow);
    }

    [Fact]
    public async Task Definition_catalog_invalidation_exposes_a_newly_activated_chat_without_reinitializing()
    {
        var definition = new LlmChatDefinitionListItem(
            Guid.NewGuid(),
            "Draft assistant",
            "Becomes available after activation.",
            string.Empty,
            LlmChatDefinitionStatus.Draft,
            1,
            1,
            DateTimeOffset.Parse("2026-08-18T10:00:00Z"),
            ["draft"]);
        var definitions = new StubDefinitionGateway(definition);
        var conversations = new StubConversationGateway(null);
        var catalogInvalidator = new LlmChatDefinitionCatalogInvalidationHub();
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddConversationShell();
        await using var contributor = new LlmChatConversationShellContributor(
            definitions,
            conversations,
            new AllowAllAuthorization(),
            catalogInvalidator,
            context.Services.GetRequiredService<IConversationShellCoordinator>(),
            context.Services.GetRequiredService<DialogService>(),
            context.Services.GetRequiredService<NotificationService>(),
            NullLogger<LlmChatConversationShellContributor>.Instance);

        await contributor.InitializeAsync();
        Assert.Empty(contributor.Snapshot().Available);

        definitions.Definition = definition with
        {
            Status = LlmChatDefinitionStatus.Active,
            ConcurrencyToken = 2,
            UpdatedAtUtc = DateTimeOffset.Parse("2026-08-18T10:01:00Z")
        };
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        contributor.Changed += (_, _) => refreshed.TrySetResult();

        catalogInvalidator.Invalidate(definitions.Definition);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var available = Assert.Single(contributor.Snapshot().Available);
        Assert.Equal(definition.Name, available.Presentation.Participant.DisplayName);
        Assert.Equal(1, definitions.ListCalls);
        Assert.Equal(1, conversations.ListCalls);
    }

    private sealed class AllowAllAuthorization : ILlmChatUiAuthorizationFacade
    {
        public ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new LlmChatUiAuthorizationSnapshot(true, true, true));

        public ValueTask<bool> IsAllowedAsync(
            LlmChatUiPermission permission,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);
    }

    private sealed class StubDefinitionGateway(LlmChatDefinitionListItem definition)
        : ILlmChatDefinitionUiGateway
    {
        public LlmChatDefinitionListItem Definition { get; set; } = definition;

        public int ListCalls { get; private set; }

        public Task<LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>> ListPageAsync(
            LlmChatDefinitionQuery query,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(
                LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>.Success(
                    new([Definition], null)));
        }

        public Task<LlmChatUiResult<LlmChatDefinitionListItem>> GetAsync(
            Guid definitionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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

    private sealed class StubConversationGateway(LlmChatConversationListItem? createdConversation)
        : ILlmChatConversationUiGateway
    {
        public Guid? CreatedDefinitionId { get; private set; }

        public int ListCalls { get; private set; }

        public Task<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>> ListPageAsync(
            LlmChatConversationQuery query,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(
                LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>.Success(
                    new([], null)));
        }

        public Task<LlmChatUiResult<LlmChatConversationView>> GetAsync(
            Guid conversationId,
            LlmChatTranscriptQuery transcriptQuery,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LlmChatUiResult<LlmChatConversationView>> CreateAsync(
            Guid definitionId,
            string title,
            CancellationToken cancellationToken = default)
        {
            CreatedDefinitionId = definitionId;
            if (createdConversation is null)
            {
                throw new NotSupportedException();
            }

            return Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(
                new(createdConversation, [], null)));
        }

        public Task<LlmChatUiResult<LlmChatConversationView>> RenameAsync(
            Guid conversationId,
            string title,
            long expectedConcurrencyToken,
            long expectedTranscriptRevision,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LlmChatUiResult<LlmChatConversationView>> ArchiveAsync(
            Guid conversationId,
            long expectedConcurrencyToken,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
