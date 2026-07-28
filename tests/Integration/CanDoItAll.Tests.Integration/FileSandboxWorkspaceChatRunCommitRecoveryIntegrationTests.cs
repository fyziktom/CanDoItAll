using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration;

public sealed class FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests
{
    [Theory]
    [InlineData((int)ChatBackedRunCommitStage.JournalPersisted)]
    [InlineData((int)ChatBackedRunCommitStage.ExecutionSlicesPersisted)]
    [InlineData((int)ChatBackedRunCommitStage.ExecutionIndexesPersisted)]
    [InlineData((int)ChatBackedRunCommitStage.WorkspaceIndexPersisted)]
    [InlineData((int)ChatBackedRunCommitStage.ChatProjectionPersisted)]
    public async Task BeginChatBackedRunAsync_FailureAfterCommitBoundary_RollsForwardExactlyOnce(
        int failureStageValue)
    {
        var failureStage = (ChatBackedRunCommitStage)failureStageValue;
        var failureInjected = false;
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (!failureInjected && stage == failureStage)
            {
                failureInjected = true;
                throw new InjectedCommitFailureException(stage);
            }
        });
        ChatBackedRunStartMutation? attemptedMutation = null;

        await Assert.ThrowsAsync<InjectedCommitFailureException>(
            () => ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
                .BeginChatBackedRunAsync(
                    scenario.Request,
                    context =>
                    {
                        attemptedMutation = CreateMutation(
                            context,
                            $"recover after {failureStage}");
                        return attemptedMutation;
                    }));

        Assert.True(failureInjected);
        Assert.NotNull(attemptedMutation);
        Assert.True(File.Exists(scenario.JournalPath));

        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);
        var recoveredDetail = await recoveryStore.GetExecutionRunDetailAsync(
            attemptedMutation!.Detail.Run.Id);
        var recoveredExecution = await recoveryStore.LoadExecutionAsync();
        var recoveredSession = await recoveryStore.GetChatSessionAsync(
            scenario.Session.Id);
        var recoveredRunSummaries = await recoveryStore.ListChatRunSummariesAsync(
            scenario.Agent.Id,
            scenario.Session.Id);
        var recoveredIndex = await ReadJsonAsync<ExecutionStorageIndex>(
            scenario.ExecutionIndexPath);
        var recoveredWorkspaceIndex = await ReadJsonAsync<WorkspaceStorageIndex>(
            scenario.WorkspaceIndexPath);
        var recoveredChatIndex = await ReadJsonAsync<ExecutionChatIndex>(
            scenario.ChatIndexPath);
        var recoveredUsageIndex = await ReadJsonAsync<AgentUsageProjection>(
            scenario.UsageIndexPath);

        Assert.NotNull(recoveredDetail);
        Assert.NotNull(recoveredSession);
        Assert.NotNull(recoveredIndex);
        Assert.NotNull(recoveredWorkspaceIndex);
        Assert.NotNull(recoveredChatIndex);
        Assert.NotNull(recoveredUsageIndex);
        Assert.False(File.Exists(scenario.JournalPath));
        Assert.Equal(
            1,
            recoveredExecution.ExecutionRuns.Count(
                run => run.Id == attemptedMutation.Detail.Run.Id));
        Assert.Equal(
            1,
            recoveredSession!.Messages.Count(
                message => message.Id == attemptedMutation.UserMessage.Id));
        Assert.Equal(
            scenario.BeforeExecution.ChatSessions.Count,
            recoveredExecution.ChatSessions.Count);
        Assert.Equal(
            scenario.BeforeExecution.ExecutionRuns.Count + 1,
            recoveredExecution.ExecutionRuns.Count);
        Assert.Equal(
            scenario.BeforeExecution.ExecutionLog.Count +
            attemptedMutation.Detail.ExecutionLog.Count,
            recoveredExecution.ExecutionLog.Count);
        Assert.Equal(
            scenario.BeforeExecutionIndex.Revision + 1L,
            recoveredIndex!.Revision);
        Assert.Equal(
            recoveredExecution.ChatSessions.Count,
            recoveredIndex.SessionCount);
        Assert.Equal(
            recoveredExecution.ExecutionRuns.Count,
            recoveredIndex.RunCount);
        Assert.Equal(
            recoveredExecution.ExecutionLog.Count,
            recoveredIndex.LogCount);
        Assert.Equal(
            scenario.BeforeWorkspaceIndex.Revision + 1L,
            recoveredWorkspaceIndex!.Revision);
        Assert.Equal(recoveredIndex.Revision, recoveredChatIndex!.Revision);
        Assert.Equal(recoveredIndex.Revision, recoveredUsageIndex!.Revision);
        Assert.Equal(
            1,
            recoveredRunSummaries.Count(
                summary =>
                    summary.ExecutionRunId ==
                    attemptedMutation.Detail.Run.Id));
    }

    [Fact]
    public async Task BeginChatBackedRunAsync_CancellationAfterJournal_CompletesCommittedRun()
    {
        using var cancellation = new CancellationTokenSource();
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (stage == ChatBackedRunCommitStage.JournalPersisted)
            {
                cancellation.Cancel();
            }
        });
        ChatBackedRunStartMutation? attemptedMutation = null;

        var result = await ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
            .BeginChatBackedRunAsync(
                scenario.Request,
                context =>
                {
                    attemptedMutation = CreateMutation(
                        context,
                        "cancel after journal");
                    return attemptedMutation;
                },
                cancellation.Token);

        var started = Assert.IsType<ChatBackedRunStarted>(result);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.NotNull(attemptedMutation);
        Assert.Equal(attemptedMutation!.Detail.Run.Id, started.Detail.Run.Id);
        Assert.False(File.Exists(scenario.JournalPath));
        Assert.NotNull(
            await scenario.Store.GetExecutionRunDetailAsync(
                attemptedMutation.Detail.Run.Id));
    }

    [Fact]
    public async Task BeginChatBackedRunAsync_CancellationBeforeJournal_PersistsNothing()
    {
        await using var scenario = await CreateScenarioAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
                .BeginChatBackedRunAsync(
                    scenario.Request,
                    context => CreateMutation(
                        context,
                        "cancel before journal"),
                    cancellation.Token));
        var execution = await scenario.Store.LoadExecutionAsync();
        var session = await scenario.Store.GetChatSessionAsync(
            scenario.Session.Id);

        Assert.False(File.Exists(scenario.JournalPath));
        Assert.Equal(
            scenario.BeforeExecution.ExecutionRuns.Count,
            execution.ExecutionRuns.Count);
        Assert.NotNull(session);
        Assert.Equal(scenario.Session.Messages, session!.Messages);
    }

    [Fact]
    public async Task LoadExecutionAsync_CorruptPendingJournal_FailsExplicitlyAndRetainsJournal()
    {
        await using var scenario = await CreateScenarioAsync();
        await File.WriteAllTextAsync(
            scenario.JournalPath,
            "{}");
        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => recoveryStore.LoadExecutionAsync());

        Assert.True(File.Exists(scenario.JournalPath));
    }

    [Fact]
    public async Task UpdateCatalogAsync_PendingChatRunJournal_RecoversRunBeforeCatalogMutation()
    {
        var failureInjected = false;
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (!failureInjected &&
                stage == ChatBackedRunCommitStage.JournalPersisted)
            {
                failureInjected = true;
                throw new InjectedCommitFailureException(stage);
            }
        });
        var beforeCatalog =
            await scenario.Store.LoadCatalogSnapshotAsync();
        ChatBackedRunStartMutation? attemptedMutation = null;

        await Assert.ThrowsAsync<InjectedCommitFailureException>(
            () => ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
                .BeginChatBackedRunAsync(
                    scenario.Request,
                    context =>
                    {
                        attemptedMutation = CreateMutation(
                            context,
                            "recover before catalog mutation");
                        return attemptedMutation;
                    }));
        Assert.True(File.Exists(scenario.JournalPath));

        var notes = $"catalog-after-chat-recovery-{Guid.NewGuid():N}";
        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);
        var changedCatalog = await recoveryStore.UpdateCatalogAsync(
            catalog => WithFirstProviderNotes(catalog, notes));
        var savedCatalog =
            await recoveryStore.LoadCatalogSnapshotAsync();
        var recoveredDetail =
            await recoveryStore.GetExecutionRunDetailAsync(
                attemptedMutation!.Detail.Run.Id);
        var workspaceIndex =
            await ReadJsonAsync<WorkspaceStorageIndex>(
                scenario.WorkspaceIndexPath);

        Assert.False(File.Exists(scenario.JournalPath));
        Assert.NotNull(recoveredDetail);
        Assert.Equal(
            attemptedMutation.Detail.Run.Id,
            recoveredDetail!.Run.Id);
        Assert.Equal(notes, changedCatalog.Providers[0].Notes);
        Assert.Equal(notes, savedCatalog.Catalog.Providers[0].Notes);
        Assert.Equal(
            beforeCatalog.Revision.Next(),
            savedCatalog.Revision);
        Assert.NotNull(workspaceIndex);
        Assert.Equal(
            scenario.BeforeWorkspaceIndex.Revision + 2L,
            workspaceIndex!.Revision);
    }

    [Theory]
    [InlineData("pending-chat-run-start.json")]
    [InlineData("pending-run-detail-update.json")]
    public async Task UpdateCatalogAsync_CorruptExecutionJournal_LeavesCatalogAndWorkspaceIndexUnchanged(
        string journalFileName)
    {
        await using var scenario = await CreateScenarioAsync();
        var beforeCatalog =
            await scenario.Store.LoadCatalogSnapshotAsync();
        var beforeWorkspaceIndex =
            await ReadJsonAsync<WorkspaceStorageIndex>(
                scenario.WorkspaceIndexPath);
        var journalPath = Path.Combine(
            Path.GetDirectoryName(scenario.JournalPath)!,
            journalFileName);
        await File.WriteAllTextAsync(journalPath, "{}");
        var mutationStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => mutationStore.UpdateCatalogAsync(
                catalog => WithFirstProviderNotes(
                    catalog,
                    $"must-not-persist-{Guid.NewGuid():N}")));

        var layout = new FileSandboxWorkspaceStorageLayout(
            scenario.WorkspaceRoot,
            scenario.Scope);
        var afterCatalog =
            await ReadJsonAsync<SandboxWorkspaceCatalog>(
                layout.CatalogPath);
        var afterWorkspaceIndex =
            await ReadJsonAsync<WorkspaceStorageIndex>(
                scenario.WorkspaceIndexPath);
        var json = new FileSandboxWorkspaceJsonStore();

        Assert.True(File.Exists(journalPath));
        Assert.NotNull(afterCatalog);
        Assert.NotNull(beforeWorkspaceIndex);
        Assert.NotNull(afterWorkspaceIndex);
        Assert.False(
            json.RequiresSave(
                beforeCatalog.Catalog,
                afterCatalog!));
        Assert.False(
            json.RequiresSave(
                beforeWorkspaceIndex!,
                afterWorkspaceIndex!));
    }

    [Fact]
    public async Task BeginChatBackedRunAsync_PausedAfterSlices_BlocksSecondStoreReadersUntilCommit()
    {
        var writerPaused = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWriter = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseApplied = false;
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (pauseApplied ||
                stage !=
                    ChatBackedRunCommitStage.ExecutionSlicesPersisted)
            {
                return;
            }

            pauseApplied = true;
            writerPaused.TrySetResult(true);
            releaseWriter.Task.GetAwaiter().GetResult();
        });
        ChatBackedRunStartMutation? mutation = null;
        var writerTask =
            ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
                .BeginChatBackedRunAsync(
                    scenario.Request,
                    context =>
                    {
                        mutation = CreateMutation(
                            context,
                            "reader isolation during chat commit");
                        return mutation;
                    });

        SandboxWorkspaceExecutionState? execution = null;
        ChatSessionRecord? session = null;
        try
        {
            await writerPaused.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
            var readerStore = new FileSandboxWorkspaceStore(
                scenario.WorkspaceRoot,
                scenario.Scope);
            var executionRead = readerStore.LoadExecutionAsync();
            var sessionRead = readerStore.GetChatSessionAsync(
                scenario.Session.Id);

            await Task.Delay(250);

            Assert.False(executionRead.IsCompleted);
            Assert.False(sessionRead.IsCompleted);
            releaseWriter.TrySetResult(true);
            await writerTask;
            execution = await executionRead;
            session = await sessionRead;
        }
        finally
        {
            releaseWriter.TrySetResult(true);
        }

        Assert.NotNull(mutation);
        Assert.NotNull(execution);
        Assert.NotNull(session);
        Assert.Contains(
            execution!.ExecutionRuns,
            run => run.Id == mutation!.Detail.Run.Id);
        Assert.Equal(
            1,
            session!.Messages.Count(
                message =>
                    message.Id == mutation!.UserMessage.Id));
        Assert.False(File.Exists(scenario.JournalPath));
    }

    private static async Task<Scenario> CreateScenarioAsync(
        Action<ChatBackedRunCommitStage>? commitBoundary = null)
    {
        var environment = CanDoItAllTestEnvironment.Create(
            $"chat-run-commit-recovery-{Guid.NewGuid():N}");
        try
        {
            var profile = environment.CreateInMemoryProfile("primary");
            var scope = WorkspaceScopeDescriptor.Sandbox;
            var setupStore = new FileSandboxWorkspaceStore(
                profile.WorkspaceRootPath,
                scope);
            var catalog = await setupStore.LoadCatalogSnapshotAsync();
            var agent = catalog.Catalog.Agents.First(
                candidate => candidate.ProviderProfileId.HasValue);
            var initialMessage = new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                "Existing history",
                DateTimeOffset.UtcNow,
                2);
            var session = await setupStore.CreateChatSessionAsync(
                new ChatSessionRecord(
                    Guid.NewGuid(),
                    agent.Id,
                    "Recoverable chat start",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [initialMessage]));
            var beforeExecution = await setupStore.LoadExecutionAsync();
            var dataRoot = scope.ResolveDataRoot(
                profile.WorkspaceRootPath);
            var executionRoot = Path.Combine(dataRoot, "execution");
            var executionIndexPath = Path.Combine(
                executionRoot,
                "index.json");
            var workspaceIndexPath = Path.Combine(
                dataRoot,
                "workspace.index.json");
            var beforeExecutionIndex =
                await ReadJsonAsync<ExecutionStorageIndex>(
                    executionIndexPath)
                ?? throw new InvalidDataException(
                    "The execution index was not initialized.");
            var beforeWorkspaceIndex =
                await ReadJsonAsync<WorkspaceStorageIndex>(
                    workspaceIndexPath)
                ?? throw new InvalidDataException(
                    "The workspace index was not initialized.");
            var store = new FileSandboxWorkspaceStore(
                profile.WorkspaceRootPath,
                scope,
                commitBoundary);

            return new Scenario(
                environment,
                profile.WorkspaceRootPath,
                scope,
                store,
                agent,
                session,
                new ChatBackedRunStartRequest(
                    agent.Id,
                    agent.ProviderProfileId!.Value,
                    catalog.Revision,
                    session.Id),
                beforeExecution,
                beforeExecutionIndex,
                beforeWorkspaceIndex,
                Path.Combine(
                    executionRoot,
                    "pending-chat-run-start.json"),
                executionIndexPath,
                workspaceIndexPath,
                Path.Combine(
                    executionRoot,
                    "chat-index.json"),
                Path.Combine(
                    executionRoot,
                    "usage-index.json"));
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }

    private static ChatBackedRunStartMutation CreateMutation(
        ChatBackedRunStartContext context,
        string prompt)
    {
        var now = DateTimeOffset.UtcNow;
        var userMessage = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.User,
            prompt,
            now,
            4);
        var session = context.Session
            ?? throw new InvalidOperationException(
                "The test requires an existing chat session.");
        var runId = Guid.NewGuid();
        var updatedSession = session with
        {
            UpdatedAtUtc = now,
            LatestExecutionRunId = runId,
            Messages = [.. session.Messages, userMessage]
        };
        var run = new ExecutionRunRecord(
            runId,
            context.Agent.Id,
            updatedSession.Id,
            updatedSession.Title,
            "integration-test",
            updatedSession.Id.ToString("N"),
            Guid.NewGuid().ToString("N"),
            string.Empty,
            "integration-test",
            "integration-test",
            "{}",
            prompt,
            "Preparing atomically.",
            "Integration test",
            "gpt-5.4-mini",
            ExecutionState.Preparing,
            null,
            now,
            now,
            now,
            null,
            string.Empty,
            null,
            [])
        {
            ProviderProfileId = context.Agent.ProviderProfileId
        };
        var log = new ExecutionLogEntry(
            Guid.NewGuid(),
            context.Agent.Id,
            updatedSession.Id,
            now,
            ExecutionState.Preparing,
            "admission",
            "The recoverable chat run was admitted.")
        {
            ExecutionRunId = runId
        };

        return new ChatBackedRunStartMutation(
            new ExecutionRunDetail(
                run,
                updatedSession,
                [log],
                []),
            userMessage);
    }

    private static Task<T?> ReadJsonAsync<T>(
        string path)
    {
        return new FileSandboxWorkspaceJsonStore()
            .ReadJsonAsync<T>(
                path,
                CancellationToken.None);
    }

    private static SandboxWorkspaceCatalog WithFirstProviderNotes(
        SandboxWorkspaceCatalog catalog,
        string notes)
    {
        Assert.NotEmpty(catalog.Providers);
        return catalog with
        {
            Providers =
            [
                catalog.Providers[0] with
                {
                    Notes = notes
                },
                .. catalog.Providers.Skip(1)
            ]
        };
    }

    private sealed class InjectedCommitFailureException(
        ChatBackedRunCommitStage stage)
        : IOException(
            $"Injected chat-run commit failure after '{stage}'.");

    private sealed record Scenario(
        CanDoItAllTestEnvironment Environment,
        string WorkspaceRoot,
        WorkspaceScopeDescriptor Scope,
        FileSandboxWorkspaceStore Store,
        AgentDefinition Agent,
        ChatSessionRecord Session,
        ChatBackedRunStartRequest Request,
        SandboxWorkspaceExecutionState BeforeExecution,
        ExecutionStorageIndex BeforeExecutionIndex,
        WorkspaceStorageIndex BeforeWorkspaceIndex,
        string JournalPath,
        string ExecutionIndexPath,
        string WorkspaceIndexPath,
        string ChatIndexPath,
        string UsageIndexPath) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return Environment.DisposeAsync();
        }
    }
}
