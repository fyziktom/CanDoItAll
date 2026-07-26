using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class FileSandboxWorkspaceChatProjectionStoreTests
{
    [Fact]
    public async Task Workspace_projection_reads_sessions_and_runs_for_one_agent_from_one_index_snapshot()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-chat-index-projection");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var projectionStore = new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
            var agentId = Guid.NewGuid();
            var otherAgentId = Guid.NewGuid();
            var selectedSession = CreateSessionSummary(agentId, "Selected thread");
            var otherSession = CreateSessionSummary(otherAgentId, "Other thread");
            var selectedRun = CreateRunSummary(agentId, selectedSession.Id);
            var otherRun = CreateRunSummary(otherAgentId, otherSession.Id);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                new ExecutionChatIndex(
                    "1.0",
                    Revision: 1,
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    SessionSummaries: [otherSession, selectedSession],
                    RunSummaries: [otherRun, selectedRun]),
                CancellationToken.None);

            var projection = await projectionStore.LoadChatWorkspaceProjectionAsync(
                agentId,
                CancellationToken.None);

            Assert.Equal(selectedSession, Assert.Single(projection.SessionSummaries));
            Assert.Equal(selectedRun, Assert.Single(projection.RunSummaries));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Missing_chat_index_is_rebuilt_once_from_canonical_records_and_persisted()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-chat-index-recovery");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var store = new FileSandboxWorkspaceStore(rootPath);
            var agentId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var session = new ChatSessionRecord(
                sessionId,
                agentId,
                "Recovered thread",
                now.AddMinutes(-2),
                now,
                Messages: [],
                LatestExecutionRunId: executionRunId);
            var run = CreateRun(
                executionRunId,
                agentId,
                sessionId,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                resultSummary: "Canonical recovery result");
            var historicalLog = new ExecutionLogEntry(
                Guid.NewGuid(),
                agentId,
                sessionId,
                now.AddSeconds(1),
                ExecutionState.Failed,
                "Historical log phase",
                "Historical log message")
            {
                ExecutionRunId = executionRunId
            };

            await jsonStore.WriteJsonAtomicallyAsync(layout.SessionPath(sessionId), session, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(layout.RunPath(executionRunId), run, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(
                Path.Combine(layout.RunLogsRoot(executionRunId), $"{historicalLog.Id:N}.json"),
                historicalLog,
                CancellationToken.None);

            var projections = await Task.WhenAll(
                Enumerable.Range(0, 4)
                    .Select(_ => store.LoadChatWorkspaceProjectionAsync(agentId)));

            Assert.True(File.Exists(layout.ExecutionChatIndexPath));
            var persistedIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                layout.ExecutionChatIndexPath,
                CancellationToken.None);
            var persistedRun = Assert.Single(Assert.IsType<ExecutionChatIndex>(persistedIndex).RunSummaries);
            Assert.Equal(ExecutionState.Completed, persistedRun.State);
            Assert.Equal("Run", persistedRun.Phase);
            Assert.Equal("Canonical recovery result", persistedRun.Message);
            Assert.All(projections, projection =>
            {
                Assert.Single(projection.SessionSummaries);
                Assert.Equal(persistedRun, Assert.Single(projection.RunSummaries));
            });
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Execution_summary_rebuilds_and_persists_missing_index_without_loading_chat_projection()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-execution-index-recovery");
        try
        {
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var store = new FileSandboxWorkspaceStore(rootPath);
            var agentId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var session = new ChatSessionRecord(
                sessionId,
                agentId,
                "Indexed thread",
                now.AddMinutes(-2),
                now,
                Messages: []);
            var activeRun = CreateRun(
                Guid.NewGuid(),
                agentId,
                sessionId,
                ExecutionState.Running,
                outcome: null,
                now);
            var failedRun = CreateRun(
                Guid.NewGuid(),
                agentId,
                sessionId,
                ExecutionState.Completed,
                RunOutcome.Failed,
                now.AddMinutes(-2));

            await jsonStore.WriteJsonAtomicallyAsync(layout.SessionPath(sessionId), session, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(layout.RunPath(activeRun.Id), activeRun, CancellationToken.None);
            await jsonStore.WriteJsonAtomicallyAsync(layout.RunPath(failedRun.Id), failedRun, CancellationToken.None);

            var summaries = await Task.WhenAll(
                Enumerable.Range(0, 4)
                    .Select(_ => store.LoadExecutionSummaryAsync()));

            Assert.All(summaries, summary =>
            {
                Assert.Equal(1, summary.SessionCount);
                Assert.Equal(1, summary.ActiveRuns);
                Assert.Equal(1, summary.FailedRuns);
            });
            Assert.True(File.Exists(layout.ExecutionIndexPath));
            Assert.False(File.Exists(layout.ExecutionChatIndexPath));

            var persistedIndex = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
                layout.ExecutionIndexPath,
                CancellationToken.None);
            var index = Assert.IsType<ExecutionStorageIndex>(persistedIndex);
            Assert.Equal(1, index.SessionCount);
            Assert.Equal(2, index.RunCount);
            Assert.Equal(1, index.ActiveRunCount);
            Assert.Equal(1, index.FailedRunCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    private static ChatSessionSummaryRecord CreateSessionSummary(Guid agentId, string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionSummaryRecord(
            Guid.NewGuid(),
            agentId,
            title,
            now,
            now,
            MessageCount: 0,
            LastMessagePreview: "No messages yet.",
            PendingApprovalCount: 0,
            AutoApprovePendingToolCalls: false);
    }

    private static ChatRunSummaryRecord CreateRunSummary(Guid agentId, Guid sessionId)
        => new(
            Guid.NewGuid(),
            agentId,
            sessionId,
            DateTimeOffset.UtcNow,
            ExecutionState.Completed,
            "Completed",
            "Done",
            RunOutcome.Succeeded);

    private static ExecutionRunRecord CreateRun(
        Guid executionRunId,
        Guid agentId,
        Guid? sessionId,
        ExecutionState state,
        RunOutcome? outcome,
        DateTimeOffset updatedAt,
        string resultSummary = "Done")
    {
        return new ExecutionRunRecord(
            executionRunId,
            agentId,
            sessionId,
            "Recovered run",
            SourceKind: "test",
            SourceId: executionRunId.ToString("N"),
            CorrelationId: executionRunId.ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Recover indexes",
            resultSummary,
            ProviderName: "test",
            Model: "test",
            state,
            outcome,
            CreatedAtUtc: updatedAt.AddMinutes(-1),
            updatedAt,
            StartedAtUtc: updatedAt.AddMinutes(-1),
            CompletedAtUtc: state == ExecutionState.Completed ? updatedAt : null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }
}
