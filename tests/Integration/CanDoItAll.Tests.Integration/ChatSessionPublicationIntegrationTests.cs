using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class ChatSessionPublicationIntegrationTests {
    [Fact]
    public async Task Rename_waiting_for_keys_reads_latest_transcript_and_compatibility() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession!;
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var next = AppendMessage(fixture.Detail);

        await InterleaveAsync(layout, FileSandboxWorkspaceWriteSet.Session(session.Id, session.AgentId).LockPaths(layout)[0],
            token => fixture.Store.RenameChatSessionAsync(Change(session, "Reviewed title"), token),
            token => fixture.NewStore().UpdateExecutionRunDetailAsync(fixture.Detail.Run.Id, _ => next, token));

        var saved = (await fixture.NewStore().GetChatSessionAsync(session.Id))!;
        Assert.Equal("Reviewed title", saved.Title);
        Assert.Equal(next.ChatSession!.Messages, saved.Messages);
        Assert.Equal(JsonSerializer.Serialize(next.ChatSession.Compatibility), JsonSerializer.Serialize(saved.Compatibility));
        Assert.Equal(next.ChatSession.LatestExecutionRunId, saved.LatestExecutionRunId);
    }

    [Fact]
    public async Task Creation_waiting_for_keys_survives_an_unrelated_run_publication() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession! with { Id = Guid.NewGuid(), LatestExecutionRunId = null };
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var before = await fixture.Store.LoadSnapshotAsync();
        var next = AppendMessage(fixture.Detail);

        await InterleaveAsync(layout, FileSandboxWorkspaceWriteSet.Session(session.Id, session.AgentId).LockPaths(layout)[0],
            token => fixture.Store.CreateChatSessionAsync(session, token),
            token => fixture.NewStore().UpdateExecutionRunDetailAsync(fixture.Detail.Run.Id, _ => next, token));

        var saved = await fixture.NewStore().LoadSnapshotAsync();
        Assert.Equal(before.Revision + 2, saved.Revision);
        Assert.Equal(before.Document.ChatSessions.Count + 1, saved.Document.ChatSessions.Count);
        Assert.Equal(session.Id, saved.Document.ChatSessions.Single(item => item.Id == session.Id).Id);
        Assert.Equal(next.ChatSession!.Messages, saved.Document.ChatSessions.Single(item => item.Id == next.ChatSession.Id).Messages);
    }

    [Fact]
    public async Task Independent_session_creations_keep_both_index_entries() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession!;
        var first = session with { Id = Guid.NewGuid(), LatestExecutionRunId = null };
        var second = session with { Id = Guid.NewGuid(), LatestExecutionRunId = null };
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var before = await fixture.Store.LoadSnapshotAsync();
        var outcomes = await PreparedPublicationCompetition.RunAsync(layout,
            FileSandboxWorkspaceWriteSet.Session(first.Id, first.AgentId).LockPaths(layout)[0],
            token => fixture.Store.CreateChatSessionAsync(first, token),
            token => fixture.NewStore().CreateChatSessionAsync(second, token));

        Assert.All(outcomes, Assert.Null);
        var reopened = fixture.NewStore();
        var snapshot = await reopened.LoadSnapshotAsync();
        Assert.Equal(before.Revision + 2, snapshot.Revision);
        Assert.Equal(before.Document.ChatSessions.Count + 2, snapshot.Document.ChatSessions.Count);
        var summaries = await reopened.ListChatSessionSummariesAsync(session.AgentId);
        Assert.Equal(snapshot.Document.ChatSessions.Where(item => item.AgentId == session.AgentId).Select(item => item.Id).Order(),
            summaries.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task Competing_renames_have_one_winner_and_one_typed_conflict() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession!;
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var outcomes = await PreparedPublicationCompetition.RunAsync(layout,
            FileSandboxWorkspaceWriteSet.Session(session.Id, session.AgentId).LockPaths(layout)[0],
            token => fixture.Store.RenameChatSessionAsync(Change(session, "First title"), token),
            token => fixture.NewStore().RenameChatSessionAsync(Change(session, "Second title"), token));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Equal(session.Id, Assert.IsType<ChatSessionTitleConflictException>(Assert.Single(outcomes.OfType<Exception>())).SessionId);
        var saved = (await fixture.NewStore().GetChatSessionAsync(session.Id))!;
        Assert.Equal(outcomes[0] is null ? "First title" : "Second title", saved.Title);
        Assert.Equal(session.Messages, saved.Messages);
    }

    [Fact]
    public async Task Duplicate_creation_preserves_the_first_session_and_count() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession! with { Id = Guid.NewGuid(), LatestExecutionRunId = null };
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var before = await fixture.Store.LoadSnapshotAsync();
        var outcomes = await PreparedPublicationCompetition.RunAsync(layout,
            FileSandboxWorkspaceWriteSet.Session(session.Id, session.AgentId).LockPaths(layout)[0],
            token => fixture.Store.CreateChatSessionAsync(session, token),
            token => fixture.NewStore().CreateChatSessionAsync(session with { Title = "Competing title" }, token));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.IsType<SandboxWorkspacePublicationConflictException>(Assert.Single(outcomes.OfType<Exception>()));
        var saved = await fixture.NewStore().LoadSnapshotAsync();
        Assert.Equal(before.Document.ChatSessions.Count + 1, saved.Document.ChatSessions.Count);
        Assert.Equal(outcomes[0] is null ? session.Title : "Competing title",
            saved.Document.ChatSessions.Single(item => item.Id == session.Id).Title);
    }

    [Fact]
    public async Task Pending_recovery_journal_appearing_during_wait_refuses_rename() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession!;
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var before = await File.ReadAllBytesAsync(layout.SessionPath(session.Id));
        var journalPath = Path.Combine(layout.ExecutionStorageRoot, "pending-run-detail-update.json");

        await InterleaveAsync(layout, FileSandboxWorkspaceWriteSet.Session(session.Id, session.AgentId).LockPaths(layout)[0],
            token => Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(() =>
                fixture.Store.RenameChatSessionAsync(Change(session, "Refused title"), token)),
            token => File.WriteAllTextAsync(journalPath, "{}", token));

        Assert.Equal(before, await File.ReadAllBytesAsync(layout.SessionPath(session.Id)));
        Assert.Equal("{}", await File.ReadAllTextAsync(journalPath));
    }

    [Theory]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded)]
    [InlineData(ExecutionState.Failed, RunOutcome.Failed)]
    [InlineData(ExecutionState.Completed, RunOutcome.Cancelled)]
    public async Task Late_terminal_snapshot_keeps_current_title(ExecutionState state, RunOutcome outcome) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession!;
        var renamed = await fixture.Store.RenameChatSessionAsync(Change(session, "Reviewed title"));
        var late = AppendMessage(fixture.Detail);
        late = late with { Run = late.Run with { State = state, Outcome = outcome, CompletedAtUtc = DateTimeOffset.UtcNow } };

        await fixture.NewStore().SaveExecutionRunDetailAsync(late);

        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Detail.Run.Id))!;
        Assert.Equal(state, saved.Run.State);
        Assert.Equal(outcome, saved.Run.Outcome);
        Assert.Equal(renamed.Title, saved.ChatSession!.Title);
        Assert.Equal(late.ChatSession!.Messages, saved.ChatSession.Messages);
        Assert.True(saved.ChatSession.UpdatedAtUtc >= renamed.UpdatedAtUtc);
    }

    [Fact]
    public async Task Conflicting_rename_leaves_all_persisted_state_unchanged() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var session = fixture.Detail.ChatSession!;
        await fixture.Store.RenameChatSessionAsync(Change(session, "First title"));
        var before = JsonSerializer.Serialize(await fixture.Store.LoadSnapshotAsync());

        await Assert.ThrowsAsync<ChatSessionTitleConflictException>(() =>
            fixture.NewStore().RenameChatSessionAsync(Change(session, "Stale title")));

        Assert.Equal(before, JsonSerializer.Serialize(await fixture.NewStore().LoadSnapshotAsync()));
    }

    private static ChatSessionTitleChange Change(ChatSessionRecord session, string title)
        => new(session.AgentId, session.Id, session.Title, title, session.UpdatedAtUtc.AddMinutes(1));

    private static ExecutionRunDetail AppendMessage(ExecutionRunDetail detail) => detail with {
        Run = detail.Run with { Revision = detail.Run.Revision + 1 },
        ChatSession = detail.ChatSession! with {
            Messages = [.. detail.ChatSession!.Messages,
                new(Guid.NewGuid(), ChatMessageRole.Assistant, "Concurrent response", DateTimeOffset.UtcNow, 2)],
            Compatibility = new("current-runtime", "{\"current\":true}", [], true)
        }
    };

    internal static async Task InterleaveAsync(FileSandboxWorkspaceStorageLayout layout, string key,
        Func<CancellationToken, Task> waiting, Func<CancellationToken, Task> intervening) {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var trace = new Activity("chat-session-publication-interleave").SetIdFormat(ActivityIdFormat.W3C).Start();
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => {
                if (activity.TraceId == trace.TraceId && activity.OperationName == "workspace.publication.keys.wait") {
                    prepared.TrySetResult();
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        IAsyncDisposable? held = await writer.AcquireCoordinationAsync(layout.RootPath, key,
            TimeSpan.FromSeconds(5), false, cancellation.Token);
        Task<Exception?>? attempt = null;
        try {
            attempt = Record.ExceptionAsync(() => waiting(cancellation.Token));
            await prepared.Task.WaitAsync(cancellation.Token);
            Assert.False(attempt.IsCompleted);
            await intervening(cancellation.Token);
            Assert.False(attempt.IsCompleted);
            var release = held;
            held = null;
            await release.DisposeAsync();
            Assert.Null(await attempt.WaitAsync(cancellation.Token));
        } finally {
            await cancellation.CancelAsync();
            if (held is not null) {
                await held.DisposeAsync();
            }
            if (attempt is not null) {
                await attempt;
            }
        }
    }
}
