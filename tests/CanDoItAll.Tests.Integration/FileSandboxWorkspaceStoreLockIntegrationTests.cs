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
}
