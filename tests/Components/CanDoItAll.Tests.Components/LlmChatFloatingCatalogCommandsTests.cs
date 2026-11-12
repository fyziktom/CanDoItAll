using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatConversationWorkspaceTests;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class LlmChatFloatingCatalogCommandsTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_command_cannot_replay_before_explicit_authorized_history_review(bool archive) {
        var original = CreateConversation();
        var gateway = new StubConversationGateway();
        gateway.Create = _ => Task.FromException<LlmChatUiResult<LlmChatConversationView>>(new IOException("Private failure"));
        gateway.Archive = gateway.Create;
        await using var commands = CreateCommands(gateway, new StubConversationEditorAccess());
        await commands.OpenAsync();
        Task<LlmChatUiResult<LlmChatConversationView>> Write() => archive
            ? commands.ArchiveAsync(original, CancellationToken.None)
            : commands.CreateAsync(original.DefinitionId, "New conversation", CancellationToken.None);

        Assert.Equal(LlmChatUiFailureCodes.OutcomeUnconfirmed, Assert.Single((await Write()).Failures).Code);
        Assert.Equal(LlmChatUiFailureCodes.OutcomeUnconfirmed, Assert.Single((await Write()).Failures).Code);
        Assert.Equal(1, archive ? gateway.ArchiveCalls : gateway.CreateCalls);
        Assert.DoesNotContain("Private failure", (await Write()).Failures[0].Message, StringComparison.Ordinal);
        await commands.AcknowledgeHistoryAsync(original.DefinitionId, [original.ConversationId], CancellationToken.None);
        var accepted = LlmChatUiResult<LlmChatConversationView>.Success(CreateView(original, []), "Saved; secondary processing needs attention.");
        gateway.Create = _ => Task.FromResult(accepted);
        gateway.Archive = gateway.Create;

        Assert.Same(accepted, await Write());
        Assert.Equal(2, archive ? gateway.ArchiveCalls : gateway.CreateCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actor_or_dataset_retirement_cannot_rebind_a_floating_catalog_command(bool archive) {
        var original = CreateConversation();
        var gateway = new StubConversationGateway { CreatedView = CreateView(original, []), ArchivedView = CreateView(original, []) };
        var access = new StubConversationEditorAccess();
        var retired = 0;
        await using var commands = CreateCommands(gateway, access, () => retired++);
        await commands.OpenAsync();
        access.Epoch++;

        var result = archive ? await commands.ArchiveAsync(original, CancellationToken.None)
            : await commands.CreateAsync(original.DefinitionId, "New conversation", CancellationToken.None);

        Assert.Equal(LlmChatUiFailureCodes.Forbidden, Assert.Single(result.Failures).Code);
        Assert.Equal(0, gateway.CreateCalls + gateway.ArchiveCalls);
        Assert.Equal(1, retired);
    }

    [Fact]
    public async Task Pending_create_blocks_another_command_and_keeps_the_exact_accepted_identity_and_warning() {
        var original = CreateConversation();
        var reply = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubConversationGateway { Create = _ => reply.Task };
        await using var commands = CreateCommands(gateway, new StubConversationEditorAccess());
        await commands.OpenAsync();
        var pending = commands.CreateAsync(original.DefinitionId, "Submitted title", CancellationToken.None);
        Assert.True(commands.IsPending);
        var rejected = await commands.ArchiveAsync(original, CancellationToken.None);
        var accepted = LlmChatUiResult<LlmChatConversationView>.Success(CreateView(original, []), "Committed with a secondary warning.");
        reply.SetResult(accepted);

        Assert.Same(accepted, await pending);
        Assert.Equal(LlmChatUiFailureCodes.CommandPending, Assert.Single(rejected.Failures).Code);
        Assert.Equal(1, gateway.CreateCalls);
        Assert.Equal(0, gateway.ArchiveCalls);
        Assert.False(commands.IsPending);
    }

    [Fact]
    public async Task Reviewing_one_conversation_never_acknowledges_an_unseen_unknown_archive() {
        var first = CreateConversation();
        var second = CreateConversation(Guid.NewGuid());
        var gateway = new StubConversationGateway {
            Archive = _ => Task.FromException<LlmChatUiResult<LlmChatConversationView>>(new IOException("Unknown transport outcome"))
        };
        await using var commands = CreateCommands(gateway, new StubConversationEditorAccess());
        await commands.OpenAsync();
        await commands.ArchiveAsync(first, CancellationToken.None);
        await commands.ArchiveAsync(second, CancellationToken.None);

        await commands.AcknowledgeHistoryAsync(first.DefinitionId, [first.ConversationId], CancellationToken.None);
        gateway.Archive = _ => Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(CreateView(first, [])));
        Assert.True((await commands.ArchiveAsync(first, CancellationToken.None)).IsSuccess);
        Assert.Equal(LlmChatUiFailureCodes.OutcomeUnconfirmed,
            Assert.Single((await commands.ArchiveAsync(second, CancellationToken.None)).Failures).Code);
        Assert.Equal(3, gateway.ArchiveCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_after_dispatch_blocks_replay_while_preflight_cancellation_performs_no_write(bool afterDispatch) {
        var original = CreateConversation();
        using var cancellation = new CancellationTokenSource();
        var gateway = new StubConversationGateway { CreatedView = CreateView(original, []) };
        if (afterDispatch) {
            gateway.Create = _ => {
                cancellation.Cancel();
                return Task.FromCanceled<LlmChatUiResult<LlmChatConversationView>>(cancellation.Token);
            };
        } else {
            cancellation.Cancel();
        }
        await using var commands = CreateCommands(gateway, new StubConversationEditorAccess());
        await commands.OpenAsync();

        var result = await commands.CreateAsync(original.DefinitionId, "New conversation", cancellation.Token);

        Assert.Equal(afterDispatch ? LlmChatUiFailureCodes.OutcomeUnconfirmed : LlmChatUiFailureCodes.RequestFailed,
            Assert.Single(result.Failures).Code);
        Assert.Equal(afterDispatch ? 1 : 0, gateway.CreateCalls);
        gateway.Create = null;
        var next = await commands.CreateAsync(original.DefinitionId, "Explicit new command", CancellationToken.None);
        Assert.Equal(!afterDispatch, next.IsSuccess);
        Assert.Equal(1, gateway.CreateCalls);
    }

    private static LlmChatFloatingCatalogCommands CreateCommands(StubConversationGateway gateway, StubConversationEditorAccess access,
        Action? retired = null) => new(gateway, access, () => Task.CompletedTask, () => {
            retired?.Invoke();
            return Task.CompletedTask;
        }, NullLogger.Instance, () => { });
}
