using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class FileSandboxWorkspaceStoreLockIntegrationTests
{
    [Fact]
    public async Task LoadCatalogAsync_reads_initialized_catalog_when_workspace_lock_is_held()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();

        _ = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var lockPath = BuildWorkspaceLockPath(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        await using var workspaceLock = OpenExclusiveWorkspaceLock(lockPath);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var catalog = await store.LoadCatalogAsync(cancellationTokenSource.Token);

        Assert.NotEmpty(catalog.Agents);
    }

    [Fact]
    public async Task GetExecutionRunDetailAsync_reads_initialized_run_detail_when_workspace_lock_is_held()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        Assert.NotEmpty(agents);
        var agent = agents[0];

        var executionRunId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;
        await store.SaveExecutionRunDetailAsync(
            new ExecutionRunDetail(
                new ExecutionRunRecord(
                    executionRunId,
                    agent.Id,
                    null,
                    "Lock regression validation",
                    "manual",
                    "lock-regression",
                    Guid.NewGuid().ToString("N"),
                    string.Empty,
                    "integration-test",
                    "integration-test",
                    "{}",
                    "Verify steady-state reads ignore the workspace lock.",
                    "Completed",
                    "OpenAI default",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    string.Empty,
                    null,
                    []),
                null,
                [
                    new ExecutionLogEntry(
                        Guid.NewGuid(),
                        agent.Id,
                        null,
                        createdAtUtc,
                        ExecutionState.Completed,
                        "validation",
                        "Workspace read should not block on an unrelated lock.")
                    {
                        ExecutionRunId = executionRunId
                    }
                ],
                []));

        var lockPath = BuildWorkspaceLockPath(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        await using var workspaceLock = OpenExclusiveWorkspaceLock(lockPath);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var readStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);

        var detail = await readStore.GetExecutionRunDetailAsync(executionRunId, cancellationTokenSource.Token);
        Assert.NotNull(detail);

        var resolvedDetail = detail!;
        Assert.Equal(executionRunId, resolvedDetail.Run.Id);
        Assert.Single(resolvedDetail.ExecutionLog);
    }

    [Fact]
    public async Task ListExecutionRunsAsync_reads_run_summaries_when_workspace_lock_is_held()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        Assert.NotEmpty(agents);
        var agent = agents[0];
        var executionRunId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;

        await store.SaveExecutionRunDetailAsync(
            new ExecutionRunDetail(
                new ExecutionRunRecord(
                    executionRunId,
                    agent.Id,
                    null,
                    "Run summary validation",
                    "manual",
                    "run-list-regression",
                    Guid.NewGuid().ToString("N"),
                    string.Empty,
                    "integration-test",
                    "integration-test",
                    "{}",
                    "Verify run summary listing does not need the full execution state.",
                    "Completed",
                    "OpenAI default",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    string.Empty,
                    null,
                    []),
                null,
                [
                    new ExecutionLogEntry(
                        Guid.NewGuid(),
                        agent.Id,
                        null,
                        createdAtUtc,
                        ExecutionState.Completed,
                        "validation",
                        "Run summary listing should not read the full execution state.")
                    {
                        ExecutionRunId = executionRunId
                    }
                ],
                []));

        var lockPath = BuildWorkspaceLockPath(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        await using var workspaceLock = OpenExclusiveWorkspaceLock(lockPath);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        ISandboxWorkspaceExecutionRunStore readStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);

        var runs = await readStore.ListExecutionRunsAsync(cancellationTokenSource.Token);

        Assert.Contains(runs, run => run.Id == executionRunId);
    }

    [Fact]
    public async Task ChatSessionStore_creates_and_updates_split_session_projection()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false)).First();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var chatSessionStore = (ISandboxWorkspaceChatSessionStore)store;
        var chatQueryStore = (ISandboxWorkspaceChatQueryStore)store;
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            Title: "Projection test",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            Messages: [],
            PendingApprovals: []);

        var createdSession = await chatSessionStore.CreateChatSessionAsync(session);
        var createdSummaries = await chatQueryStore.ListChatSessionSummariesAsync(agent.Id);

        Assert.Equal(session.Id, createdSession.Id);
        Assert.Contains(createdSummaries, summary => summary.Id == session.Id && summary.Title == "Projection test");

        var renamedSession = await chatSessionStore.UpdateChatSessionAsync(createdSession with
        {
            Title = "Projection test renamed",
            UpdatedAtUtc = now.AddMinutes(1)
        });
        var loadedSession = await chatQueryStore.GetChatSessionAsync(session.Id);
        var renamedSummaries = await chatQueryStore.ListChatSessionSummariesAsync(agent.Id);

        Assert.Equal("Projection test renamed", renamedSession.Title);
        Assert.Equal("Projection test renamed", loadedSession?.Title);
        Assert.Contains(renamedSummaries, summary => summary.Id == session.Id && summary.Title == "Projection test renamed");
    }

    [Fact]
    public async Task CreateChatSessionAsync_does_not_read_unrelated_run_file_when_chat_index_exists()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var layout = new FileSandboxWorkspaceStorageLayout(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false)).First();
        var unrelatedRunId = Guid.NewGuid();
        await store.SaveExecutionRunDetailAsync(CreateRunDetail(unrelatedRunId, agent.Id, "Unrelated run"));
        Assert.True(File.Exists(layout.ExecutionChatIndexPath));

        await File.WriteAllTextAsync(
            layout.RunPath(unrelatedRunId),
            "{ this is not valid json",
            CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            Title: "New session without a run",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            Messages: [],
            PendingApprovals: []);

        var createdSession = await ((ISandboxWorkspaceChatSessionStore)store).CreateChatSessionAsync(session);

        Assert.Equal(session.Id, createdSession.Id);
    }

    [Fact]
    public async Task SaveExecutionRunDetailAsync_does_not_load_unrelated_run_slices_in_split_storage()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var layout = new FileSandboxWorkspaceStorageLayout(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        Assert.NotEmpty(agents);
        var agent = agents[0];

        var unrelatedRunId = Guid.NewGuid();
        await store.SaveExecutionRunDetailAsync(CreateRunDetail(unrelatedRunId, agent.Id, "Unrelated run"));

        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var targetRunId = Guid.NewGuid();
        await store.SaveExecutionRunDetailAsync(CreateRunDetail(targetRunId, agent.Id, "Target run"));

        var savedDetail = await store.GetExecutionRunDetailAsync(targetRunId);

        Assert.NotNull(savedDetail);
        Assert.Equal(targetRunId, savedDetail!.Run.Id);
    }

    private static FileStream OpenExclusiveWorkspaceLock(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        return new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.Asynchronous);
    }

    private static string BuildWorkspaceLockPath(string workspaceRoot, WorkspaceScopeDescriptor scope)
    {
        return Path.Combine(
            workspaceRoot,
            scope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.lock");
    }

    private static ExecutionRunDetail CreateRunDetail(Guid executionRunId, Guid agentId, string title)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                executionRunId,
                agentId,
                null,
                title,
                "manual",
                "run-slice-regression",
                Guid.NewGuid().ToString("N"),
                string.Empty,
                "integration-test",
                "integration-test",
                "{}",
                "Verify run-detail saves do not load unrelated run slices.",
                "Completed",
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                string.Empty,
                null,
                []),
            null,
            [
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agentId,
                    null,
                    createdAtUtc,
                    ExecutionState.Completed,
                    "validation",
                    "Run detail should save without inspecting unrelated runs.")
                {
                    ExecutionRunId = executionRunId
                }
            ],
            []);
    }
}
