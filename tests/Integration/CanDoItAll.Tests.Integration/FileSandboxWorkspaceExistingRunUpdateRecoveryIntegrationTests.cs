using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests(
    ITestOutputHelper output)
{
    [Theory]
    [InlineData((int)ExistingRunDetailCommitStage.JournalPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.SessionPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RunPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.ApprovalRecordsPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RemainingRecordsPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.ExecutionIndexPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.UsageIndexPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.WorkspaceIndexPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.ChatIndexPersisted)]
    public async Task UpdateExecutionRunDetailAsync_FailureAfterCommitBoundary_RollsForwardContinuationExactlyOnce(
        int failureStageValue)
    {
        var failureStage =
            (ExistingRunDetailCommitStage)failureStageValue;
        var failureInjected = false;
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (!failureInjected &&
                stage == failureStage)
            {
                failureInjected = true;
                throw new InjectedCommitFailureException(stage);
            }
        });
        ExecutionRunDetail? attemptedTarget = null;

        await Assert.ThrowsAsync<InjectedCommitFailureException>(
            () => ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    scenario.RunId,
                    (_, detail) =>
                    {
                        attemptedTarget = CreateContinuationTarget(detail);
                        return attemptedTarget;
                    }));

        Assert.True(failureInjected);
        Assert.NotNull(attemptedTarget);
        Assert.True(File.Exists(scenario.JournalPath));

        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);
        var recoveredDetail =
            await recoveryStore.GetExecutionRunDetailAsync(
                scenario.RunId);
        var recoveredExecution =
            await recoveryStore.LoadExecutionAsync();
        var recoveredIndex =
            await ReadJsonAsync<ExecutionStorageIndex>(
                scenario.ExecutionIndexPath);
        var recoveredWorkspaceIndex =
            await ReadJsonAsync<WorkspaceStorageIndex>(
                scenario.WorkspaceIndexPath);
        var recoveredChatIndex =
            await ReadJsonAsync<ExecutionChatIndex>(
                scenario.ChatIndexPath);
        var recoveredUsageIndex =
            await ReadJsonAsync<AgentUsageProjection>(
                scenario.UsageIndexPath);

        Assert.NotNull(recoveredDetail);
        Assert.NotNull(recoveredIndex);
        Assert.NotNull(recoveredWorkspaceIndex);
        Assert.NotNull(recoveredChatIndex);
        Assert.NotNull(recoveredUsageIndex);
        Assert.False(File.Exists(scenario.JournalPath));
        Assert.Equal(
            ExecutionState.Running,
            recoveredDetail!.Run.State);
        Assert.Empty(recoveredDetail.Run.PendingApprovals);
        Assert.Equal(
            scenario.BeforeDetail.Run.Revision + 1L,
            recoveredDetail.Run.Revision);
        Assert.Null(recoveredDetail.ChatSession!.Compatibility);
        var approval = Assert.Single(recoveredDetail.Approvals);
        Assert.Equal(
            ExecutionApprovalStatus.Approved,
            approval.Status);
        Assert.NotNull(approval.DecidedAtUtc);
        Assert.Equal(
            "chat-session",
            approval.DecisionSourceKind);
        Assert.Equal(
            scenario.RunId.ToString("N"),
            approval.DecisionSourceId);
        Assert.Equal(
            1,
            recoveredExecution.ExecutionRuns.Count(
                run => run.Id == scenario.RunId));
        Assert.Equal(
            1,
            recoveredExecution.ExecutionApprovals.Count(
                item =>
                    item.ExecutionRunId == scenario.RunId &&
                    item.ApprovalId == scenario.ApprovalId));
        Assert.Equal(
            scenario.BeforeExecutionIndex.Revision + 1L,
            recoveredIndex!.Revision);
        Assert.Equal(
            scenario.BeforeExecutionIndex.SessionCount,
            recoveredIndex.SessionCount);
        Assert.Equal(
            scenario.BeforeExecutionIndex.RunCount,
            recoveredIndex.RunCount);
        Assert.Equal(
            scenario.BeforeExecutionIndex.ApprovalCount,
            recoveredIndex.ApprovalCount);
        Assert.Equal(
            scenario.BeforeWorkspaceIndex.Revision + 1L,
            recoveredWorkspaceIndex!.Revision);
        Assert.Equal(
            recoveredIndex.Revision,
            recoveredChatIndex!.Revision);
        Assert.Equal(
            recoveredIndex.Revision,
            recoveredUsageIndex!.Revision);
        Assert.Equal(
            1,
            recoveredChatIndex.RunSummaries.Count(
                summary =>
                    summary.ExecutionRunId == scenario.RunId));
    }

    [Fact]
    public async Task UpdateExecutionRunDetailAsync_CancellationAfterJournal_CompletesCommittedContinuation()
    {
        using var cancellation = new CancellationTokenSource();
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (stage ==
                ExistingRunDetailCommitStage.JournalPersisted)
            {
                cancellation.Cancel();
            }
        });

        var result =
            await ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    scenario.RunId,
                    (_, detail) =>
                        CreateContinuationTarget(detail),
                    cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(ExecutionState.Running, result.Run.State);
        Assert.Empty(result.Run.PendingApprovals);
        Assert.False(File.Exists(scenario.JournalPath));
        var persisted =
            await scenario.Store.GetExecutionRunDetailAsync(
                scenario.RunId);
        Assert.Equal(ExecutionState.Running, persisted!.Run.State);
        Assert.Equal(
            ExecutionApprovalStatus.Approved,
            Assert.Single(persisted.Approvals).Status);
    }

    [Fact]
    public async Task UpdateExecutionRunDetailAsync_CancellationBeforeJournal_PersistsNothing()
    {
        await using var scenario = await CreateScenarioAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    scenario.RunId,
                    (_, detail) =>
                        CreateContinuationTarget(detail),
                    cancellation.Token));

        Assert.False(File.Exists(scenario.JournalPath));
        var persisted =
            await scenario.Store.GetExecutionRunDetailAsync(
                scenario.RunId);
        Assert.Equal(
            ExecutionState.WaitingOnTool,
            persisted!.Run.State);
        Assert.Single(persisted.Run.PendingApprovals);
        Assert.Equal(
            ExecutionApprovalStatus.Pending,
            Assert.Single(persisted.Approvals).Status);
    }

    [Fact]
    public async Task GetExecutionRunDetailAsync_CorruptPendingUpdateJournal_FailsExplicitlyAndRetainsJournal()
    {
        await using var scenario = await CreateScenarioAsync();
        await File.WriteAllTextAsync(
            scenario.JournalPath,
            "{}");
        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => recoveryStore.GetExecutionRunDetailAsync(
                scenario.RunId));

        Assert.True(File.Exists(scenario.JournalPath));
    }

    [Fact]
    public async Task UpdateCatalogAsync_PendingRunUpdateJournal_RecoversTargetBeforeCatalogMutation()
    {
        var failureInjected = false;
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (!failureInjected &&
                stage ==
                    ExistingRunDetailCommitStage.JournalPersisted)
            {
                failureInjected = true;
                throw new InjectedCommitFailureException(stage);
            }
        });
        var beforeCatalog =
            await scenario.Store.LoadCatalogSnapshotAsync();
        ExecutionRunDetail? attemptedTarget = null;

        await Assert.ThrowsAsync<InjectedCommitFailureException>(
            () => ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    scenario.RunId,
                    (_, detail) =>
                    {
                        attemptedTarget =
                            CreateContinuationTarget(detail);
                        return attemptedTarget;
                    }));
        Assert.True(File.Exists(scenario.JournalPath));

        var notes =
            $"catalog-after-run-recovery-{Guid.NewGuid():N}";
        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope);
        var changedCatalog = await recoveryStore.UpdateCatalogAsync(
            catalog => WithFirstProviderNotes(catalog, notes));
        var savedCatalog =
            await recoveryStore.LoadCatalogSnapshotAsync();
        var recoveredDetail =
            await recoveryStore.GetExecutionRunDetailAsync(
                scenario.RunId);
        var workspaceIndex =
            await ReadJsonAsync<WorkspaceStorageIndex>(
                scenario.WorkspaceIndexPath);

        Assert.NotNull(attemptedTarget);
        Assert.False(File.Exists(scenario.JournalPath));
        Assert.NotNull(recoveredDetail);
        Assert.Equal(
            ExecutionState.Running,
            recoveredDetail!.Run.State);
        Assert.Empty(recoveredDetail.Run.PendingApprovals);
        Assert.Equal(
            ExecutionApprovalStatus.Approved,
            Assert.Single(recoveredDetail.Approvals).Status);
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

    [Fact]
    public async Task UpdateExecutionRunDetailAsync_PausedAfterRunWrite_BlocksSecondStoreReadersUntilCommit()
    {
        var writerPaused = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWriter = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseApplied = false;
        await using var scenario = await CreateScenarioAsync(stage =>
        {
            if (pauseApplied ||
                stage != ExistingRunDetailCommitStage.RunPersisted)
            {
                return;
            }

            pauseApplied = true;
            writerPaused.TrySetResult(true);
            releaseWriter.Task.GetAwaiter().GetResult();
        });
        var writerTask =
            ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    scenario.RunId,
                    (_, detail) =>
                        CreateContinuationTarget(detail));

        ExecutionRunDetail? detail = null;
        ChatWorkspaceProjectionSnapshot? chatProjection = null;
        try
        {
            await writerPaused.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
            var readerStore = new FileSandboxWorkspaceStore(
                scenario.WorkspaceRoot,
                scenario.Scope);
            var detailRead =
                readerStore.GetExecutionRunDetailAsync(
                    scenario.RunId);
            var chatRead =
                readerStore.LoadChatWorkspaceProjectionAsync(
                    scenario.BeforeDetail.Run.AgentId);

            await Task.Delay(250);

            Assert.False(detailRead.IsCompleted);
            Assert.False(chatRead.IsCompleted);
            releaseWriter.TrySetResult(true);
            await writerTask;
            detail = await detailRead;
            chatProjection = await chatRead;
        }
        finally
        {
            releaseWriter.TrySetResult(true);
        }

        Assert.NotNull(detail);
        Assert.NotNull(chatProjection);
        Assert.Equal(ExecutionState.Running, detail!.Run.State);
        Assert.Empty(detail.Run.PendingApprovals);
        Assert.Equal(
            ExecutionApprovalStatus.Approved,
            Assert.Single(detail.Approvals).Status);
        Assert.Equal(
            1,
            chatProjection!.RunSummaries.Count(
                summary =>
                    summary.ExecutionRunId == scenario.RunId));
        Assert.False(File.Exists(scenario.JournalPath));
    }

    [Fact]
    public async Task UpdateExecutionRunDetailAsync_LargeHistory_WalRemainsWithinMeasuredBounds()
    {
        const int historyEntryCount = 400;
        const int historyPayloadLength = 256;
        string? journalPath = null;
        var elapsedToJournal = TimeSpan.Zero;
        var journalBytes = 0L;
        var stopwatch = new Stopwatch();
        await using var scenario = await CreateScenarioAsync(
            stage =>
            {
                if (stage !=
                    ExistingRunDetailCommitStage.JournalPersisted)
                {
                    return;
                }

                elapsedToJournal = stopwatch.Elapsed;
                journalBytes = new FileInfo(journalPath!).Length;
            },
            historyEntryCount,
            historyPayloadLength);
        journalPath = scenario.JournalPath;

        stopwatch.Start();
        var persisted =
            await ((ISandboxWorkspaceExecutionRunMutationStore)scenario.Store)
                .UpdateExecutionRunDetailAsync(
                    scenario.RunId,
                    (_, detail) =>
                        CreateContinuationTarget(detail));
        stopwatch.Stop();

        output.WriteLine(
            $"Existing-run WAL measurement: historyEntries={historyEntryCount}, payloadCharacters={historyPayloadLength}, journalBytes={journalBytes}, elapsedToJournalMs={elapsedToJournal.TotalMilliseconds:F1}, totalCommitMs={stopwatch.Elapsed.TotalMilliseconds:F1}.");
        Assert.Equal(ExecutionState.Running, persisted.Run.State);
        Assert.InRange(
            journalBytes,
            100_000L,
            16L * 1024L * 1024L);
        Assert.True(
            elapsedToJournal < TimeSpan.FromSeconds(20),
            $"WAL preparation and persistence took {elapsedToJournal.TotalMilliseconds:F1} ms.");
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(30),
            $"The complete WAL commit took {stopwatch.Elapsed.TotalMilliseconds:F1} ms.");
        Assert.False(File.Exists(scenario.JournalPath));
    }

    private static async Task<Scenario> CreateScenarioAsync(
        Action<ExistingRunDetailCommitStage>? commitBoundary = null,
        int historyEntryCount = 1,
        int historyPayloadLength = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            historyEntryCount,
            1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            historyPayloadLength,
            1);

        var environment = CanDoItAllTestEnvironment.Create(
            $"run-detail-update-recovery-{Guid.NewGuid():N}");
        try
        {
            var profile =
                environment.CreateInMemoryProfile("primary");
            var scope = WorkspaceScopeDescriptor.Sandbox;
            var setupStore = new FileSandboxWorkspaceStore(
                profile.WorkspaceRootPath,
                scope);
            var catalog =
                await setupStore.LoadCatalogSnapshotAsync();
            var agent = catalog.Catalog.Agents.First(
                candidate =>
                    candidate.ProviderProfileId.HasValue);
            var now = DateTimeOffset.UtcNow;
            var runId = Guid.NewGuid();
            var historyPayload = new string(
                'x',
                historyPayloadLength);
            var messages = Enumerable.Range(0, historyEntryCount)
                .Select(index => new ChatMessageRecord(
                    Guid.NewGuid(),
                    ChatMessageRole.User,
                    $"{index:D4}:{historyPayload}",
                    now.AddTicks(index),
                    Math.Max(1, historyPayloadLength / 4)))
                .ToArray();
            var approval = new PendingToolApprovalRecord(
                "approval-1",
                "call-1",
                "integration.tool",
                "integration",
                "Approve the integration operation.",
                """{"value":"safe"}""");
            var session = new ChatSessionRecord(
                Guid.NewGuid(),
                agent.Id,
                "Recoverable continuation",
                now,
                now.AddTicks(historyEntryCount),
                messages,
                LatestExecutionRunId: runId,
                Compatibility:
                    ChatSessionRuntimeCompatibilityRecord.Create(
                        "runtime-session",
                        """{"state":"waiting"}""",
                        [approval]));
            var run = new ExecutionRunRecord(
                runId,
                agent.Id,
                session.Id,
                session.Title,
                "integration-test",
                session.Id.ToString("N"),
                Guid.NewGuid().ToString("N"),
                string.Empty,
                "integration-test",
                "integration-test",
                "{}",
                "Continue after approval.",
                "Waiting for approval.",
                "Integration test",
                "gpt-5.4-mini",
                ExecutionState.WaitingOnTool,
                null,
                now,
                now,
                now,
                null,
                "runtime-session",
                """{"state":"waiting"}""",
                [approval])
            {
                ProviderProfileId = agent.ProviderProfileId
            };
            var logs = Enumerable.Range(0, historyEntryCount)
                .Select(index => new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agent.Id,
                    session.Id,
                    now.AddTicks(index),
                    ExecutionState.WaitingOnTool,
                    "approval-history",
                    $"{index:D4}:{historyPayload}")
                {
                    ExecutionRunId = runId
                })
                .ToArray();
            var approvalRecord = new ExecutionApprovalRecord(
                approval.ApprovalId,
                runId,
                approval.CallId,
                approval.ToolName,
                approval.ToolKind,
                approval.Details,
                approval.ArgumentsJson,
                ExecutionApprovalStatus.Pending,
                now,
                null,
                string.Empty,
                string.Empty,
                string.Empty);
            var beforeDetail =
                await setupStore.SaveExecutionRunDetailAsync(
                    new ExecutionRunDetail(
                        run,
                        session,
                        logs,
                        [])
                    {
                        Approvals = [approvalRecord]
                    });
            var dataRoot = scope.ResolveDataRoot(
                profile.WorkspaceRootPath);
            var executionRoot = Path.Combine(
                dataRoot,
                "execution");
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
                chatBackedRunCommitBoundary: null,
                existingRunDetailCommitBoundary:
                    commitBoundary);

            return new Scenario(
                environment,
                profile.WorkspaceRootPath,
                scope,
                store,
                runId,
                approval.ApprovalId,
                beforeDetail,
                beforeExecutionIndex,
                beforeWorkspaceIndex,
                Path.Combine(
                    executionRoot,
                    "pending-run-detail-update.json"),
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

    private static ExecutionRunDetail CreateContinuationTarget(
        ExecutionRunDetail detail)
    {
        var decidedAtUtc = DateTimeOffset.UtcNow;
        return detail with
        {
            Run = detail.Run with
            {
                Revision = detail.Run.Revision + 1L,
                UpdatedAtUtc = decidedAtUtc,
                State = ExecutionState.Running,
                Outcome = null,
                PendingApprovals = [],
                ResultSummary =
                    "Resuming execution after approval."
            },
            ChatSession = detail.ChatSession! with
            {
                UpdatedAtUtc = decidedAtUtc,
                Compatibility = null
            },
            Approvals = detail.Approvals
                .Select(approval => approval with
                {
                    Status = ExecutionApprovalStatus.Approved,
                    DecidedAtUtc = decidedAtUtc,
                    DecisionSourceKind = "chat-session",
                    DecisionSourceId =
                        detail.Run.Id.ToString("N"),
                    DecisionNotes =
                        "Approved through execution continuation."
                })
                .ToList()
        };
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

    private static Task<T?> ReadJsonAsync<T>(
        string path)
    {
        return new FileSandboxWorkspaceJsonStore()
            .ReadJsonAsync<T>(
                path,
                CancellationToken.None);
    }

    private sealed class InjectedCommitFailureException(
        ExistingRunDetailCommitStage stage)
        : IOException(
            $"Injected execution-run update failure after '{stage}'.");

    private sealed record Scenario(
        CanDoItAllTestEnvironment Environment,
        string WorkspaceRoot,
        WorkspaceScopeDescriptor Scope,
        FileSandboxWorkspaceStore Store,
        Guid RunId,
        string ApprovalId,
        ExecutionRunDetail BeforeDetail,
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
