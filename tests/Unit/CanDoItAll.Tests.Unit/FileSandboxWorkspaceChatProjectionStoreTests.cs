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
}
