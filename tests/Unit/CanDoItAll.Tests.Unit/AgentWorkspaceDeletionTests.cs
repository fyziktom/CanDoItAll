using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentWorkspaceDeletionTests
{
    [Fact]
    public async Task Deletion_cascades_owned_catalog_and_execution_data_without_reading_unrelated_run_details()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-deletion-cascade");
        try
        {
            var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
            var store = CreateStore(
                rootPath,
                new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));
            var target = await CreateAgentAsync(store, "Deletion target");
            var unrelated = await CreateAgentAsync(store, "Deletion survivor");
            var targetDetail = CreateRunDetail(
                target,
                DateTimeOffset.UtcNow.AddMinutes(-2));
            var unrelatedDetail = CreateRunDetail(
                unrelated,
                DateTimeOffset.UtcNow.AddMinutes(-1));
            await store.SaveExecutionRunDetailAsync(targetDetail);
            await store.SaveExecutionRunDetailAsync(unrelatedDetail);
            var summaryBeforeDeletion = await store.LoadExecutionSummaryAsync();
            var teamId = Guid.NewGuid();
            await store.UpdateCatalogAsync(catalog => catalog with
            {
                Memory = catalog.Memory
                    .Append(new AgentMemoryRecord(
                        Guid.NewGuid(),
                        target.Id,
                        MemoryKind.Context,
                        "Delete me",
                        "Target memory",
                        "test",
                        Importance: 5,
                        MetadataJson: "{}",
                        CreatedAtUtc: DateTimeOffset.UtcNow))
                    .ToList(),
                AgentExternalBindings = catalog.AgentExternalBindings
                    .Append(new AgentExternalBindingRecord(
                        "test",
                        target.Id.ToString("N"),
                        target.Id,
                        "1",
                        IsArchived: false,
                        UpdatedAtUtc: DateTimeOffset.UtcNow))
                    .ToList(),
                AgentTeams = catalog.AgentTeams
                    .Append(new AgentTeamDefinition(
                        teamId,
                        "Deletion team",
                        "Cascade test",
                        [target.Id, unrelated.Id],
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow))
                    .ToList()
            });
            reads.Clear();

            var result = await ((ISandboxWorkspaceAgentDeletionStore)store)
                .DeleteAgentWorkspaceDataAsync(target.Id);
            var deletionReads = reads.ToList();

            Assert.True(result.Deleted);
            Assert.Equal(1, result.DeletedChatSessionCount);
            Assert.Equal(1, result.DeletedExecutionRunCount);
            var catalog = await store.LoadCatalogAsync();
            Assert.DoesNotContain(catalog.Agents, item => item.Id == target.Id);
            Assert.DoesNotContain(catalog.Memory, item => item.AgentId == target.Id);
            Assert.DoesNotContain(
                catalog.AgentExternalBindings,
                item => item.AgentId == target.Id);
            Assert.Equal(
                [unrelated.Id],
                Assert.Single(catalog.AgentTeams, item => item.Id == teamId).AgentIds);
            Assert.Null(await store.GetExecutionRunDetailAsync(targetDetail.Run.Id));
            Assert.Null(await store.GetChatSessionAsync(targetDetail.ChatSession!.Id));
            Assert.NotNull(await store.GetExecutionRunDetailAsync(unrelatedDetail.Run.Id));
            var usage = await store.LoadUsageProjectionAsync();
            Assert.DoesNotContain(usage.Agents, item => item.AgentId == target.Id);
            Assert.Contains(usage.Agents, item => item.AgentId == unrelated.Id);
            var summary = await store.LoadExecutionSummaryAsync();
            Assert.Equal(
                summaryBeforeDeletion.SessionCount - 1,
                summary.SessionCount);
            var unrelatedRoot = Path.GetFullPath(
                new FileSandboxWorkspaceStorageLayout(rootPath)
                    .RunRoot(unrelatedDetail.Run.Id));
            Assert.DoesNotContain(
                deletionReads,
                read => read.FullPath.StartsWith(
                    unrelatedRoot,
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Deletion_reconciles_a_stale_session_count_before_building_the_deletion_plan()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-deletion-session-count-reconciliation");
        try
        {
            var store = new FileSandboxWorkspaceStore(rootPath);
            var target = await CreateAgentAsync(store, "Session count target");
            var detail = CreateRunDetail(target, DateTimeOffset.UtcNow);
            await store.SaveExecutionRunDetailAsync(detail);

            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var index = Assert.IsType<ExecutionStorageIndex>(
                await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
                    layout.ExecutionIndexPath,
                    CancellationToken.None));
            var persistedSessionCount = Directory
                .EnumerateFiles(layout.ExecutionSessionsRoot, "*.json")
                .Count();
            var persistedRunCount = Directory
                .EnumerateDirectories(layout.ExecutionRunsRoot)
                .Count(directory => File.Exists(
                    Path.Combine(directory, "run.json")));
            Assert.Equal(persistedSessionCount, index.SessionCount);
            Assert.Equal(persistedRunCount, index.RunCount);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionIndexPath,
                index with
                {
                    SessionCount = index.SessionCount + 1
                },
                CancellationToken.None);

            var result = await store.DeleteAgentWorkspaceDataAsync(target.Id);

            Assert.True(result.Deleted);
            Assert.Equal(1, result.DeletedChatSessionCount);
            Assert.Equal(1, result.DeletedExecutionRunCount);
            Assert.DoesNotContain(
                (await store.LoadCatalogAsync()).Agents,
                item => item.Id == target.Id);
            Assert.Null(await store.GetExecutionRunDetailAsync(detail.Run.Id));
            Assert.Null(await store.GetChatSessionAsync(detail.ChatSession!.Id));
            var repairedIndex = Assert.IsType<ExecutionStorageIndex>(
                await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(
                    layout.ExecutionIndexPath,
                    CancellationToken.None));
            Assert.Equal(
                persistedSessionCount - 1,
                repairedIndex.SessionCount);
            Assert.Equal(
                persistedRunCount - 1,
                repairedIndex.RunCount);
            Assert.Equal(
                repairedIndex.SessionCount,
                Directory
                    .EnumerateFiles(layout.ExecutionSessionsRoot, "*.json")
                    .Count());
            Assert.Equal(
                repairedIndex.RunCount,
                Directory
                    .EnumerateDirectories(layout.ExecutionRunsRoot)
                    .Count(directory => File.Exists(
                        Path.Combine(directory, "run.json"))));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Deleting_agent_without_history_does_not_enumerate_unrelated_run_payloads()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-deletion-indexed");
        try
        {
            var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
            var store = CreateStore(
                rootPath,
                new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));
            var target = await CreateAgentAsync(store, "No history");
            for (var index = 0; index < 6; index++)
            {
                var unrelated = await CreateAgentAsync(store, $"Survivor {index}");
                await store.SaveExecutionRunDetailAsync(
                    CreateRunDetail(
                        unrelated,
                        DateTimeOffset.UtcNow.AddMinutes(index)));
            }

            reads.Clear();

            await ((ISandboxWorkspaceAgentDeletionStore)store)
                .DeleteAgentWorkspaceDataAsync(target.Id);

            var runsRoot = Path.GetFullPath(
                new FileSandboxWorkspaceStorageLayout(rootPath).ExecutionRunsRoot);
            Assert.DoesNotContain(
                reads,
                read => read.FullPath.StartsWith(
                    runsRoot,
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Deletion_cascades_run_referenced_by_owned_session_when_run_agent_differs()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-deletion-session-cascade");
        try
        {
            var store = new FileSandboxWorkspaceStore(rootPath);
            var target = await CreateAgentAsync(store, "Session owner");
            var runOwner = await CreateAgentAsync(store, "Run owner");
            var detail = CreateRunDetail(runOwner, DateTimeOffset.UtcNow);
            await store.SaveExecutionRunDetailAsync(detail);
            var layout = new FileSandboxWorkspaceStorageLayout(rootPath);
            var jsonStore = new FileSandboxWorkspaceJsonStore();
            var session = detail.ChatSession! with
            {
                AgentId = target.Id
            };
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.SessionPath(session.Id),
                session,
                CancellationToken.None);
            var chatIndex = Assert.IsType<ExecutionChatIndex>(
                await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    CancellationToken.None));
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.ExecutionChatIndexPath,
                chatIndex with
                {
                    SessionSummaries = chatIndex.SessionSummaries
                        .Select(item => item.Id == session.Id
                            ? item with
                            {
                                AgentId = target.Id
                            }
                            : item)
                        .ToList()
                },
                CancellationToken.None);

            var result = await store.DeleteAgentWorkspaceDataAsync(target.Id);

            Assert.Equal(1, result.DeletedChatSessionCount);
            Assert.Equal(1, result.DeletedExecutionRunCount);
            Assert.Null(await store.GetExecutionRunDetailAsync(detail.Run.Id));
            Assert.Null(await store.GetChatSessionAsync(session.Id));
            Assert.Contains(
                (await store.LoadCatalogAsync()).Agents,
                item => item.Id == runOwner.Id);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Managed_seed_agent_deletion_is_rejected_before_any_mutation()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-deletion-seed");
        try
        {
            var store = new FileSandboxWorkspaceStore(rootPath);
            var seed = (await store.LoadCatalogAsync()).Agents.First(
                ManagedSeedProviderFallbacks.IsManagedSeedAgent);

            var exception = await Assert.ThrowsAsync<AgentDeletionConflictException>(
                () => ((ISandboxWorkspaceAgentDeletionStore)store)
                    .DeleteAgentWorkspaceDataAsync(seed.Id));

            Assert.Equal(AgentDeletionConflictKind.ManagedSeedAgent, exception.Kind);
            Assert.Contains(
                (await store.LoadCatalogAsync()).Agents,
                item => item.Id == seed.Id);
            Assert.False(File.Exists(PendingJournalPath(rootPath)));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Managed_seed_clone_is_detached_and_deletable_while_source_remains_managed()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "managed-seed-clone-deletion");
        try
        {
            var store = new FileSandboxWorkspaceStore(rootPath);
            var service = CreateCatalogService(store);
            var source = (await store.LoadCatalogAsync()).Agents.First(
                ManagedSeedProviderFallbacks.IsManagedSeedAgent);

            var cloneId = await service.CloneAgentAsync(
                source.Id,
                $"{source.Name} detached clone");

            var clone = Assert.Single(
                (await store.LoadCatalogAsync()).Agents,
                item => item.Id == cloneId);
            Assert.True(ManagedSeedProviderFallbacks.IsManagedSeedAgent(source));
            Assert.False(ManagedSeedProviderFallbacks.IsManagedSeedAgent(clone));

            await service.DeleteAgentAsync(cloneId);

            var catalog = await store.LoadCatalogAsync();
            Assert.DoesNotContain(catalog.Agents, item => item.Id == cloneId);
            var persistedSource = Assert.Single(
                catalog.Agents,
                item => item.Id == source.Id);
            Assert.True(
                ManagedSeedProviderFallbacks.IsManagedSeedAgent(persistedSource));
            var exception = await Assert.ThrowsAsync<AgentDeletionConflictException>(
                () => service.DeleteAgentAsync(source.Id));
            Assert.Equal(
                AgentDeletionConflictKind.ManagedSeedAgent,
                exception.Kind);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Managed_seed_template_conversion_is_detached_and_deletable_while_source_remains_managed()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "managed-seed-template-deletion");
        try
        {
            var store = new FileSandboxWorkspaceStore(rootPath);
            var service = CreateCatalogService(store);
            var source = (await store.LoadCatalogAsync()).Agents.First(
                ManagedSeedProviderFallbacks.IsManagedSeedAgent);

            var templateId = await service.ConvertToTemplateAsync(
                source.Id,
                $"detached-{Guid.NewGuid():N}");

            var template = Assert.Single(
                (await store.LoadCatalogAsync()).Agents,
                item => item.Id == templateId);
            Assert.True(template.IsTemplate);
            Assert.False(ManagedSeedProviderFallbacks.IsManagedSeedAgent(template));

            await service.DeleteAgentAsync(templateId);

            var catalog = await store.LoadCatalogAsync();
            Assert.DoesNotContain(catalog.Agents, item => item.Id == templateId);
            var persistedSource = Assert.Single(
                catalog.Agents,
                item => item.Id == source.Id);
            Assert.True(
                ManagedSeedProviderFallbacks.IsManagedSeedAgent(persistedSource));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Agent_with_active_execution_is_rejected_before_any_mutation()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-deletion-active");
        try
        {
            var store = new FileSandboxWorkspaceStore(rootPath);
            var target = await CreateAgentAsync(store, "Active target");
            var detail = CreateRunDetail(
                target,
                DateTimeOffset.UtcNow,
                ExecutionState.Running);
            await store.SaveExecutionRunDetailAsync(detail);

            var exception = await Assert.ThrowsAsync<AgentDeletionConflictException>(
                () => ((ISandboxWorkspaceAgentDeletionStore)store)
                    .DeleteAgentWorkspaceDataAsync(target.Id));

            Assert.Equal(AgentDeletionConflictKind.ActiveExecution, exception.Kind);
            Assert.Contains(
                (await store.LoadCatalogAsync()).Agents,
                item => item.Id == target.Id);
            Assert.NotNull(await store.GetExecutionRunDetailAsync(detail.Run.Id));
            Assert.False(File.Exists(PendingJournalPath(rootPath)));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Theory]
    [InlineData((int)AgentDeletionCommitStage.ExecutionSlicesPersisted)]
    [InlineData((int)AgentDeletionCommitStage.CatalogPersisted)]
    [InlineData((int)AgentDeletionCommitStage.WorkspaceIndexPersisted)]
    public async Task Pending_deletion_recovers_idempotently_after_each_commit_boundary(
        int failingStageValue)
    {
        var failingStage = (AgentDeletionCommitStage)failingStageValue;
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-deletion-recovery");
        try
        {
            var setupStore = new FileSandboxWorkspaceStore(rootPath);
            var target = await CreateAgentAsync(setupStore, "Recovery target");
            var failingStore = CreateStore(
                rootPath,
                agentDeletionCommitBoundary: stage =>
                {
                    if (stage == failingStage)
                    {
                        throw new InjectedCommitFailureException();
                    }
                });

            await Assert.ThrowsAsync<InjectedCommitFailureException>(
                () => ((ISandboxWorkspaceAgentDeletionStore)failingStore)
                    .DeleteAgentWorkspaceDataAsync(target.Id));
            Assert.True(File.Exists(PendingJournalPath(rootPath)));
            var pending = await new FileSandboxWorkspaceJsonStore()
                .ReadJsonAsync<AgentDeletionCommitJournal>(
                    PendingJournalPath(rootPath),
                    CancellationToken.None);
            Assert.Null(Assert.IsType<AgentDeletionCommitJournal>(pending).SourceChatIndex);
            Assert.Null(pending.TargetChatIndex);

            var recoveredStore = new FileSandboxWorkspaceStore(rootPath);
            Assert.DoesNotContain(
                (await recoveredStore.LoadCatalogAsync()).Agents,
                item => item.Id == target.Id);
            Assert.False(File.Exists(PendingJournalPath(rootPath)));
            var replay = await ((ISandboxWorkspaceAgentDeletionStore)recoveredStore)
                .DeleteAgentWorkspaceDataAsync(target.Id);
            Assert.False(replay.Deleted);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Theory]
    [InlineData((int)AgentDeletionCommitStage.ExecutionSlicesPersisted)]
    [InlineData((int)AgentDeletionCommitStage.CatalogPersisted)]
    [InlineData((int)AgentDeletionCommitStage.WorkspaceIndexPersisted)]
    public async Task Pending_execution_deletion_recovers_all_indexes_after_each_commit_boundary(
        int failingStageValue)
    {
        var failingStage = (AgentDeletionCommitStage)failingStageValue;
        var rootPath = TestFileSystem.CreateTemporaryRoot(
            "agent-execution-deletion-recovery");
        try
        {
            var setupStore = new FileSandboxWorkspaceStore(rootPath);
            var target = await CreateAgentAsync(setupStore, "Run recovery target");
            var detail = CreateRunDetail(target, DateTimeOffset.UtcNow);
            await setupStore.SaveExecutionRunDetailAsync(detail);
            var failingStore = CreateStore(
                rootPath,
                agentDeletionCommitBoundary: stage =>
                {
                    if (stage == failingStage)
                    {
                        throw new InjectedCommitFailureException();
                    }
                });

            await Assert.ThrowsAsync<InjectedCommitFailureException>(
                () => ((ISandboxWorkspaceAgentDeletionStore)failingStore)
                    .DeleteAgentWorkspaceDataAsync(target.Id));
            Assert.True(File.Exists(PendingJournalPath(rootPath)));
            var pending = Assert.IsType<AgentDeletionCommitJournal>(
                await new FileSandboxWorkspaceJsonStore()
                    .ReadJsonAsync<AgentDeletionCommitJournal>(
                        PendingJournalPath(rootPath),
                        CancellationToken.None));
            Assert.NotNull(pending.SourceChatIndex);
            Assert.NotNull(pending.TargetChatIndex);

            var recoveredStore = new FileSandboxWorkspaceStore(rootPath);
            Assert.DoesNotContain(
                (await recoveredStore.LoadCatalogAsync()).Agents,
                item => item.Id == target.Id);
            Assert.Null(await recoveredStore.GetExecutionRunDetailAsync(detail.Run.Id));
            Assert.Null(await recoveredStore.GetChatSessionAsync(detail.ChatSession!.Id));
            Assert.DoesNotContain(
                (await recoveredStore.LoadUsageProjectionAsync()).Agents,
                item => item.AgentId == target.Id);
            Assert.False(File.Exists(PendingJournalPath(rootPath)));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task Deletion_rebuilds_shared_usage_last_used_when_newest_contributor_is_removed()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("agent-deletion-usage");
        try
        {
            var store = new FileSandboxWorkspaceStore(rootPath);
            var survivor = await CreateAgentAsync(store, "Usage survivor");
            var target = await CreateAgentAsync(store, "Usage target");
            var survivorAt = DateTimeOffset.UtcNow.AddHours(-2);
            await store.SaveExecutionRunDetailAsync(
                CreateRunDetail(survivor, survivorAt));
            await store.SaveExecutionRunDetailAsync(
                CreateRunDetail(target, survivorAt.AddHours(1)));

            await ((ISandboxWorkspaceAgentDeletionStore)store)
                .DeleteAgentWorkspaceDataAsync(target.Id);

            var usage = await store.LoadUsageProjectionAsync();
            Assert.Equal(
                survivorAt,
                Assert.Single(usage.Providers).LastUsedAtUtc);
            Assert.Equal(
                survivorAt,
                Assert.Single(usage.Models).LastUsedAtUtc);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    private static FileSandboxWorkspaceStore CreateStore(
        string rootPath,
        FileSandboxWorkspaceJsonReadDiagnostics? diagnostics = null,
        Action<AgentDeletionCommitStage>? agentDeletionCommitBoundary = null)
    {
        return new FileSandboxWorkspaceStore(
            rootPath,
            WorkspaceScopeDescriptor.Sandbox,
            chatBackedRunCommitBoundary: null,
            existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: null,
            diagnostics,
            agentDeletionCommitBoundary);
    }

    private static AgentFrameworkWorkspaceCatalogService CreateCatalogService(
        ISandboxWorkspaceStore store)
    {
        return new AgentFrameworkWorkspaceCatalogService(
            store,
            CreateUnexpectedDependency<IAgentPackageService>(),
            CreateUnexpectedDependency<ICapabilityProofService>(),
            CreateUnexpectedDependency<IProviderProfileService>(),
            CreateUnexpectedDependency<IProviderDiagnosticsService>(),
            CreateUnexpectedDependency<IProviderProfileRegistry>(),
            CreateUnexpectedDependency<IProviderRuntimeProfileSource>());
    }

    private static T CreateUnexpectedDependency<T>()
        where T : class
    {
        return DispatchProxy.Create<T, UnexpectedCallProxy>();
    }

    private static async Task<AgentDefinition> CreateAgentAsync(
        FileSandboxWorkspaceStore store,
        string name)
    {
        var catalog = await store.LoadCatalogAsync();
        var source = catalog.Agents.First();
        var now = DateTimeOffset.UtcNow;
        var agent = source with
        {
            Id = Guid.NewGuid(),
            Name = name,
            RoleTitle = "Test agent",
            ConfigurationJson = "{}",
            IsTemplate = false,
            TemplateKey = $"test-{Guid.NewGuid():N}",
            Capabilities = [],
            Tags = ["test"],
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await store.UpdateCatalogAsync(current => current with
        {
            Agents = current.Agents.Append(agent).ToList()
        });
        return agent;
    }

    private static ExecutionRunDetail CreateRunDetail(
        AgentDefinition agent,
        DateTimeOffset updatedAtUtc,
        ExecutionState state = ExecutionState.Completed)
    {
        var runId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var outcome = state == ExecutionState.Completed
            ? RunOutcome.Succeeded
            : (RunOutcome?)null;
        var run = new ExecutionRunRecord(
            runId,
            agent.Id,
            sessionId,
            "Deletion test run",
            SourceKind: "test",
            SourceId: runId.ToString("N"),
            CorrelationId: runId.ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Delete an agent",
            ResultSummary: outcome.HasValue ? "Done" : string.Empty,
            ProviderName: "test-provider",
            Model: "test-model",
            state,
            outcome,
            CreatedAtUtc: updatedAtUtc.AddMinutes(-1),
            updatedAtUtc,
            StartedAtUtc: updatedAtUtc.AddMinutes(-1),
            CompletedAtUtc: outcome.HasValue ? updatedAtUtc : null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProviderProfileId: agent.ProviderProfileId);
        var session = new ChatSessionRecord(
            sessionId,
            agent.Id,
            "Deletion test session",
            updatedAtUtc.AddMinutes(-1),
            updatedAtUtc,
            Messages: [],
            LatestExecutionRunId: runId);
        var log = new ExecutionLogEntry(
            Guid.NewGuid(),
            agent.Id,
            sessionId,
            updatedAtUtc,
            state,
            "Run",
            outcome.HasValue ? "Done" : "Running")
        {
            ExecutionRunId = runId
        };
        var usage = new ProviderUsageObservation(
            Guid.NewGuid(),
            updatedAtUtc,
            run.ProviderName,
            ProviderKind.OpenAi,
            run.Model,
            ProviderTransportKind.Responses,
            ProviderUsageSourcePhases.AgentRuntime,
            ProviderUsageObservationStatus.Observed,
            InputTokens: 10,
            CachedInputTokens: 0,
            OutputTokens: 5,
            ReasoningTokens: 0,
            TotalTokens: 15,
            ToolCallCount: 0)
        {
            ExecutionRunId = runId,
            AgentId = agent.Id,
            ChatSessionId = sessionId,
            CalculatedCostUsd = 0.01m
        };
        return new ExecutionRunDetail(
            run,
            session,
            ExecutionLog: [log],
            Metrics: [])
        {
            UsageObservations = [usage]
        };
    }

    private static string PendingJournalPath(string rootPath)
    {
        return Path.Combine(
            new FileSandboxWorkspaceStorageLayout(rootPath).ExecutionStorageRoot,
            "pending-agent-deletion.json");
    }

    private sealed class InjectedCommitFailureException : Exception;

    private class UnexpectedCallProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            throw new InvalidOperationException(
                $"Dependency member '{targetMethod?.Name}' was not expected.");
        }
    }
}
